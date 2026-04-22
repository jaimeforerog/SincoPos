import apiClient from './client';
import type { VentaDTO, CrearVentaDTO, PaginatedResult } from '@/types/api';

export const ventasApi = {
  getAll: async (params?: {
    sucursalId?: number;
    cajaId?: number;
    clienteId?: number;
    desde?: string;
    hasta?: string;
    estado?: string;
    page?: number;
    pageSize?: number;
  }) => {
    const response = await apiClient.get<PaginatedResult<VentaDTO>>('/ventas', {
      params,
    });
    return response.data;
  },

  getById: async (id: number) => {
    const response = await apiClient.get<VentaDTO>(`/ventas/${id}`);
    return response.data;
  },

  create: async (data: CrearVentaDTO) => {
    const response = await apiClient.post<VentaDTO>('/ventas', data);
    return response.data;
  },

  anular: async (id: number, motivo?: string) => {
    const response = await apiClient.post<{ mensaje: string; stockRevertido: boolean }>(
      `/ventas/${id}/anular`,
      null,
      { params: { motivo } }
    );
    return response.data;
  },

  getErpPendientesCount: async (sucursalId?: number) => {
    const response = await apiClient.get<{ pendientes: number }>('/ventas/erp/pendientes-count', {
      params: sucursalId ? { sucursalId } : undefined,
    });
    return response.data.pendientes;
  },
};
