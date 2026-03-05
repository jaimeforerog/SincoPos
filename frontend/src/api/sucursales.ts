import apiClient from './client';
import type { SucursalDTO, CrearSucursalDTO, ActualizarSucursalDTO } from '@/types/api';

export const sucursalesApi = {
  getAll: async (incluirInactivas?: boolean) => {
    const response = await apiClient.get<SucursalDTO[]>('/api/sucursales', {
      params: { incluirInactivas },
    });
    return response.data;
  },

  listar: async (incluirInactivas?: boolean) => {
    const response = await apiClient.get<SucursalDTO[]>('/api/sucursales', {
      params: { incluirInactivas },
    });
    return response.data;
  },

  getById: async (id: number) => {
    const response = await apiClient.get<SucursalDTO>(`/api/sucursales/${id}`);
    return response.data;
  },

  create: async (data: CrearSucursalDTO) => {
    const response = await apiClient.post<SucursalDTO>('/api/sucursales', data);
    return response.data;
  },

  update: async (id: number, data: ActualizarSucursalDTO) => {
    await apiClient.put(`/api/sucursales/${id}`, data);
  },

  delete: async (id: number) => {
    await apiClient.delete(`/api/sucursales/${id}`);
  },
};
