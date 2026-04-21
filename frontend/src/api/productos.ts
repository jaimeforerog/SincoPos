import apiClient from './client';
import type {
  ProductoDTO,
  CrearProductoDTO,
  ActualizarProductoDTO,
  PaginatedResult,
} from '@/types/api';

export const productosApi = {
  getAll: async (params?: {
    query?: string;
    categoriaId?: number;
    incluirInactivos?: boolean;
    activo?: boolean;
    page?: number;
    pageSize?: number;
  }) => {
    const response = await apiClient.get<PaginatedResult<ProductoDTO>>('/productos', {
      params,
    });
    return response.data;
  },

  listar: async (params?: {
    query?: string;
    categoriaId?: number;
    incluirInactivos?: boolean;
    activo?: boolean;
    page?: number;
    pageSize?: number;
  }) => {
    const response = await apiClient.get<PaginatedResult<ProductoDTO>>('/productos', {
      params,
    });
    return response.data;
  },

  getById: async (id: string) => {
    const response = await apiClient.get<ProductoDTO>(`/productos/${id}`);
    return response.data;
  },

  create: async (data: CrearProductoDTO) => {
    const response = await apiClient.post<ProductoDTO>('/productos', data);
    return response.data;
  },

  update: async (id: string, data: ActualizarProductoDTO) => {
    const response = await apiClient.put<ProductoDTO>(
      `/productos/${id}`,
      data
    );
    return response.data;
  },

  delete: async (id: string) => {
    await apiClient.delete(`/productos/${id}`);
  },

  activate: async (id: string) => {
    await apiClient.patch(`/productos/${id}/activar`);
  },

  getAnticipated: async (limite = 20): Promise<ProductoDTO[]> => {
    const response = await apiClient.get<ProductoDTO[]>('/productos/anticipados', {
      params: { limite },
    });
    return response.data;
  },
};
