import apiClient from './client';
import type { VentaDTO, CrearVentaDTO } from '@/types/api';

export const ventasApi = {
  getAll: async (params?: {
    sucursalId?: number;
    cajaId?: number;
    desde?: string;
    hasta?: string;
    estado?: string;
    limite?: number;
  }) => {
    const response = await apiClient.get<VentaDTO[]>('/ventas', {
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
};
