import apiClient from './client';
import type { SucursalDTO, CrearSucursalDTO, ActualizarSucursalDTO, PaginatedResult } from '@/types/api';

export const sucursalesApi = {
  /** Obtener sucursales activas de una empresa específica (usado en el selector inicial) */
  getByEmpresa: async (empresaId: number) => {
    const response = await apiClient.get<PaginatedResult<SucursalDTO>>('/sucursales', {
      headers: { 'X-Empresa-Id': String(empresaId) },
    });
    return response.data.items;
  },

  getAll: async (incluirInactivas?: boolean) => {
    const response = await apiClient.get<PaginatedResult<SucursalDTO>>('/sucursales', {
      params: { incluirInactivas },
    });
    return response.data.items;
  },

  listar: async (incluirInactivas?: boolean) => {
    const response = await apiClient.get<PaginatedResult<SucursalDTO>>('/sucursales', {
      params: { incluirInactivas },
    });
    return response.data.items;
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

  activate: async (id: number) => {
    await apiClient.patch(`/sucursales/${id}/activar`);
  },
};
