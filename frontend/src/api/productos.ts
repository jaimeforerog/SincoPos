import apiClient from './client';
import type {
  ProductoDTO,
  CrearProductoDTO,
  ActualizarProductoDTO,
} from '@/types/api';

export const productosApi = {
  getAll: async (params?: {
    query?: string;
    categoriaId?: number;
    incluirInactivos?: boolean;
  }) => {
    const response = await apiClient.get<ProductoDTO[]>('/api/productos', {
      params,
    });
    return response.data;
  },

  listar: async (params?: {
    query?: string;
    categoriaId?: number;
    incluirInactivos?: boolean;
  }) => {
    const response = await apiClient.get<ProductoDTO[]>('/api/productos', {
      params,
    });
    return response.data;
  },

  getById: async (id: string) => {
    const response = await apiClient.get<ProductoDTO>(`/api/productos/${id}`);
    return response.data;
  },

  create: async (data: CrearProductoDTO) => {
    const response = await apiClient.post<ProductoDTO>('/api/productos', data);
    return response.data;
  },

  update: async (id: string, data: ActualizarProductoDTO) => {
    const response = await apiClient.put<ProductoDTO>(
      `/api/productos/${id}`,
      data
    );
    return response.data;
  },

  delete: async (id: string) => {
    await apiClient.delete(`/api/productos/${id}`);
  },
};
