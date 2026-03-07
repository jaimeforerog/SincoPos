import apiClient from './client';
import type {
  TrasladoDTO,
  CrearTrasladoDTO,
  RecibirTrasladoDTO,
  RechazarTrasladoDTO,
  CancelarTrasladoDTO,
} from '@/types/api';

export const trasladosApi = {
  // Listar traslados
  listar: async (params?: {
    sucursalOrigenId?: number;
    sucursalDestinoId?: number;
    estado?: string;
    fechaDesde?: string;
    fechaHasta?: string;
  }) => {
    const response = await apiClient.get<TrasladoDTO[]>('/traslados', { params });
    return response.data;
  },

  // Obtener traslado por ID
  obtener: async (id: number) => {
    const response = await apiClient.get<TrasladoDTO>(`/traslados/${id}`);
    return response.data;
  },

  // Crear traslado
  crear: async (data: CrearTrasladoDTO) => {
    const response = await apiClient.post('/traslados', data);
    return response.data;
  },

  // Enviar traslado
  enviar: async (id: number) => {
    const response = await apiClient.post(`/traslados/${id}/enviar`);
    return response.data;
  },

  // Recibir traslado
  recibir: async (id: number, data: RecibirTrasladoDTO) => {
    const response = await apiClient.post(`/traslados/${id}/recibir`, data);
    return response.data;
  },

  // Rechazar traslado
  rechazar: async (id: number, data: RechazarTrasladoDTO) => {
    const response = await apiClient.post(`/traslados/${id}/rechazar`, data);
    return response.data;
  },

  // Cancelar traslado
  cancelar: async (id: number, data: CancelarTrasladoDTO) => {
    const response = await apiClient.post(`/traslados/${id}/cancelar`, data);
    return response.data;
  },
};
