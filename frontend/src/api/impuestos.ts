import apiClient from './client';
import type { ImpuestoDTO, RetencionReglaDTO, CrearImpuestoDTO, EditarImpuestoDTO, CrearRetencionDTO } from '@/types/api';

export const impuestosApi = {
  getAll: async (pais?: string): Promise<ImpuestoDTO[]> => {
    const params = pais ? { pais } : {};
    const response = await apiClient.get<ImpuestoDTO[]>('/api/impuestos', { params });
    return response.data;
  },

  getById: async (id: number): Promise<ImpuestoDTO> => {
    const response = await apiClient.get<ImpuestoDTO>(`/api/impuestos/${id}`);
    return response.data;
  },

  getTipos: async (): Promise<{ valor: number; nombre: string }[]> => {
    const response = await apiClient.get('/api/impuestos/tipos');
    return response.data;
  },

  create: async (dto: CrearImpuestoDTO): Promise<ImpuestoDTO> => {
    const response = await apiClient.post<ImpuestoDTO>('/api/impuestos', dto);
    return response.data;
  },

  update: async (id: number, dto: EditarImpuestoDTO): Promise<void> => {
    await apiClient.put(`/api/impuestos/${id}`, dto);
  },

  deactivate: async (id: number): Promise<void> => {
    await apiClient.delete(`/api/impuestos/${id}`);
  },
};

export const retencionesApi = {
  getAll: async (): Promise<RetencionReglaDTO[]> => {
    const response = await apiClient.get<RetencionReglaDTO[]>('/api/retenciones');
    return response.data;
  },

  create: async (dto: CrearRetencionDTO): Promise<void> => {
    await apiClient.post('/api/retenciones', dto);
  },

  update: async (id: number, dto: CrearRetencionDTO): Promise<void> => {
    await apiClient.put(`/api/retenciones/${id}`, dto);
  },

  deactivate: async (id: number): Promise<void> => {
    await apiClient.delete(`/api/retenciones/${id}`);
  },
};
