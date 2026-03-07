import axios, { AxiosError } from 'axios';
import type { InternalAxiosRequestConfig } from 'axios';
import type { ApiError } from '@/types/api';

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

// Request interceptor to add auth token
apiClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    // Get token from session storage or from OIDC context
    const token = sessionStorage.getItem('access_token');

    if (token && config.headers) {
      config.headers.Authorization = `Bearer ${token}`;
    }

    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

// Response interceptor to handle errors
apiClient.interceptors.response.use(
  (response) => {
    return response;
  },
  (error: AxiosError<ApiError>) => {
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

      // Handle specific status codes
      if (error.response.status === 401) {
        // Unauthorized - redirect to login
        sessionStorage.removeItem('access_token');
        window.location.href = '/login';
      }

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
