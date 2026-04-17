import axios, { AxiosError } from 'axios';
import type { InternalAxiosRequestConfig } from 'axios';
import type { ApiError } from '@/types/api';
import { useAuthStore } from '@/stores/auth.store';
import { getWorkosToken } from '@/api/tokenRef';

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
    const token = sessionStorage.getItem('access_token');
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

// Response interceptor to handle errors and auto-refresh tokens
apiClient.interceptors.response.use(
  (response) => {
    return response;
  },
  async (error: AxiosError<ApiError>) => {
    const originalRequest = error.config as InternalAxiosRequestConfig & { _retry?: boolean };

    // Si el servidor responde 401, intentar obtener un token fresco del SDK de WorkOS
    // antes de rendirse — esto evita la cascada de 401 cuando el token expiró.
    if (
      error.response?.status === 401 &&
      !originalRequest._retry &&
      getWorkosToken
    ) {
      originalRequest._retry = true;

      if (!isRefreshing) {
        isRefreshing = true;
        try {
          const freshToken = await getWorkosToken();
          if (freshToken) {
            sessionStorage.setItem('access_token', freshToken);
            isRefreshing = false;
            onTokenRefreshed(freshToken);

            if (originalRequest.headers) {
              originalRequest.headers.Authorization = `Bearer ${freshToken}`;
            }
            return apiClient(originalRequest);
          } else {
            throw new Error('WorkOS no pudo obtener un token fresco');
          }
        } catch {
          isRefreshing = false;
          refreshSubscribers = [];
          // Token no renovable — cerrar sesión
          sessionStorage.removeItem('access_token');
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
      const data = error.response.data;
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

