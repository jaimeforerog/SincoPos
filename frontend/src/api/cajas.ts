import apiClient from './client';
import type { CajaDTO, CrearCajaDTO, AbrirCajaDTO, CerrarCajaDTO } from '@/types/api';

export const cajasApi = {
  getAll: async (params?: { sucursalId?: number; estado?: string }) => {
    const response = await apiClient.get<CajaDTO[]>('/cajas', { params });
    return response.data;
  },

  getById: async (id: number) => {
    const response = await apiClient.get<CajaDTO>(`/cajas/${id}`);
    return response.data;
  },

  crear: async (data: CrearCajaDTO) => {
    const response = await apiClient.post<CajaDTO>('/cajas', data);
    return response.data;
  },

  abrir: async (id: number, data: AbrirCajaDTO) => {
    const response = await apiClient.post<CajaDTO>(`/cajas/${id}/abrir`, data);
    return response.data;
  },

  cerrar: async (id: number, data: CerrarCajaDTO) => {
    const response = await apiClient.post<CajaDTO>(
      `/cajas/${id}/cerrar`,
      data
    );
    return response.data;
  },

  getMisAbiertas: async () => {
    const response = await apiClient.get<CajaDTO[]>('/cajas/mis-abiertas');
    return response.data;
  },
};
