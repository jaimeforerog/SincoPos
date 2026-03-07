import apiClient from './client';
import type { SucursalDTO, CrearSucursalDTO, ActualizarSucursalDTO } from '@/types/api';

export const sucursalesApi = {
  getAll: async (incluirInactivas?: boolean) => {
    const response = await apiClient.get<SucursalDTO[]>('/sucursales', {
      params: { incluirInactivas },
    });
    return response.data;
  },

  listar: async (incluirInactivas?: boolean) => {
    const response = await apiClient.get<SucursalDTO[]>('/sucursales', {
      params: { incluirInactivas },
    });
    return response.data;
  },

  getById: async (id: number) => {
    const response = await apiClient.get<SucursalDTO>(`/sucursales/${id}`);
    return response.data;
  },

  create: async (data: CrearSucursalDTO) => {
    const response = await apiClient.post<SucursalDTO>('/sucursales', data);
    return response.data;
  },

  update: async (id: number, data: ActualizarSucursalDTO) => {
    await apiClient.put(`/sucursales/${id}`, data);
  },

  delete: async (id: number) => {
    await apiClient.delete(`/sucursales/${id}`);
  },
};
