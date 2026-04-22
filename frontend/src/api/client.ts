import axios, { AxiosError } from 'axios';
import type { InternalAxiosRequestConfig } from 'axios';
import type { ApiError } from '@/types/api';
import { useAuthStore } from '@/stores/auth.store';
import { getWorkosToken, getRefreshToken, setRefreshToken, clearRefreshToken } from '@/api/tokenRef';

// Use empty string (relative paths) when VITE_API_URL is not set so requests
// go through the Vite dev proxy (same-origin, no CORS).
// In production VITE_API_URL is set to the backend origin.
const API_URL = import.meta.env.VITE_API_URL ?? '';
const API_VERSION = import.meta.env.VITE_API_VERSION || 'v1';

export const apiClient = axios.create({
  baseURL: `${API_URL}/api/${API_VERSION}`,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor to add auth token and empresa context
apiClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    const token = localStorage.getItem('access_token');
    if (token && config.headers) {
      config.headers.Authorization = `Bearer ${token}`;
    }

    const { activeEmpresaId } = useAuthStore.getState();
    if (activeEmpresaId != null && config.headers) {
      config.headers['X-Empresa-Id'] = String(activeEmpresaId);
    }

    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

// Token refresh state — shared across concurrent requests
let isRefreshing = false;
let refreshSubscribers: Array<(token: string) => void> = [];

function subscribeTokenRefresh(cb: (token: string) => void) {
  refreshSubscribers.push(cb);
}
function onTokenRefreshed(token: string) {
  refreshSubscribers.forEach(cb => cb(token));
  refreshSubscribers = [];
}

async function tryRefreshViaBackend(): Promise<string | null> {
  const refreshToken = getRefreshToken();
  if (!refreshToken) return null;
  try {
    const API_URL = import.meta.env.VITE_API_URL ?? '';
    const API_VERSION = import.meta.env.VITE_API_VERSION || 'v1';
    const { data } = await axios.post<{ accessToken: string; refreshToken?: string }>(
      `${API_URL}/api/${API_VERSION}/auth/refresh`,
      { refreshToken }
    );
    localStorage.setItem('access_token', data.accessToken);
    if (data.refreshToken) setRefreshToken(data.refreshToken);
    return data.accessToken;
  } catch {
    return null;
  }
}

// Response interceptor to handle errors and auto-refresh tokens
apiClient.interceptors.response.use(
  (response) => {
    return response;
  },
  async (error: AxiosError<ApiError>) => {
    const originalRequest = error.config as InternalAxiosRequestConfig & { _retry?: boolean };

    // Si el servidor responde 401, intentar renovar el token antes de rendirse.
    // Orden: 1) SDK de WorkOS  2) refresh_token almacenado via backend
    if (
      error.response?.status === 401 &&
      !originalRequest._retry
    ) {
      originalRequest._retry = true;

      if (!isRefreshing) {
        isRefreshing = true;
        try {
          // Intento 1: SDK de WorkOS (funciona si la cookie de sesión sigue vigente)
          let freshToken = getWorkosToken ? await getWorkosToken() : null;

          // Intento 2: refresh_token almacenado (funciona aunque la cookie haya expirado)
          if (!freshToken) {
            freshToken = await tryRefreshViaBackend();
          }

          if (freshToken) {
            localStorage.setItem('access_token', freshToken);
            isRefreshing = false;
            onTokenRefreshed(freshToken);

            if (originalRequest.headers) {
              originalRequest.headers.Authorization = `Bearer ${freshToken}`;
            }
            return apiClient(originalRequest);
          } else {
            throw new Error('No se pudo renovar la sesión');
          }
        } catch {
          isRefreshing = false;
          refreshSubscribers = [];
          // Sin opciones de renovación — cerrar sesión
          localStorage.removeItem('access_token');
          clearRefreshToken();
          useAuthStore.getState().setUser(null);
          return Promise.reject(error);
        }
      } else {
        // Otro request ya está renovando — encolar éste
        return new Promise((resolve) => {
          subscribeTokenRefresh((newToken: string) => {
            if (originalRequest.headers) {
              originalRequest.headers.Authorization = `Bearer ${newToken}`;
            }
            resolve(apiClient(originalRequest));
          });
        });
      }
    }

    if (error.response) {
      // Server responded with error
      const data = error.response.data as any;
      let message = 'An error occurred';
      let errors = data?.errors;

      if (typeof data === 'string') {
        message = data;
      } else if (Array.isArray(data)) {
        // FluentValidation returns array of error strings
        message = data.join('. ');
        errors = data;
      } else if (data?.detail) {
        // ASP.NET ProblemDetails RFC 7807: detail is the human-readable explanation
        message = data.detail;
        errors = data.errors;
      } else if (data?.error) {
        message = data.error;
      } else if (data?.message) {
        message = data.message;
      } else if (data?.title) {
        // ASP.NET ProblemDetails format
        message = data.title;
        errors = data.errors;
      }

      const apiError: ApiError = {
        message,
        errors,
        statusCode: error.response.status,
      };

      return Promise.reject(apiError);
    } else if (error.request) {
      // Request made but no response
      const apiError: ApiError = {
        message: 'No response from server. Please check your connection.',
        statusCode: 0,
      };
      return Promise.reject(apiError);
    } else {
      // Something else happened
      const apiError: ApiError = {
        message: error.message || 'An unexpected error occurred',
        statusCode: 0,
      };
      return Promise.reject(apiError);
    }
  }
);

export default apiClient;

