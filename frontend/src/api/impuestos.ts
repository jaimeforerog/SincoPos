import apiClient from './client';
import type {
  ImpuestoDTO, RetencionReglaDTO, CrearImpuestoDTO, EditarImpuestoDTO, CrearRetencionDTO,
  ConceptoRetencionDTO, CrearConceptoRetencionDTO, EditarConceptoRetencionDTO,
} from '@/types/api';

export const impuestosApi = {
  getAll: async (pais?: string): Promise<ImpuestoDTO[]> => {
    const params = pais ? { pais } : {};
    const response = await apiClient.get<ImpuestoDTO[]>('/impuestos', { params });
    return response.data;
  },

  getById: async (id: number): Promise<ImpuestoDTO> => {
    const response = await apiClient.get<ImpuestoDTO>(`/impuestos/${id}`);
    return response.data;
  },

  getTipos: async (): Promise<{ valor: number; nombre: string }[]> => {
    const response = await apiClient.get('/impuestos/tipos');
    return response.data;
  },

  create: async (dto: CrearImpuestoDTO): Promise<ImpuestoDTO> => {
    const response = await apiClient.post<ImpuestoDTO>('/impuestos', dto);
    return response.data;
  },

  update: async (id: number, dto: EditarImpuestoDTO): Promise<void> => {
    await apiClient.put(`/impuestos/${id}`, dto);
  },

  deactivate: async (id: number): Promise<void> => {
    await apiClient.delete(`/impuestos/${id}`);
  },
};

export const retencionesApi = {
  getAll: async (): Promise<RetencionReglaDTO[]> => {
    const response = await apiClient.get<RetencionReglaDTO[]>('/retenciones');
    return response.data;
  },

  create: async (dto: CrearRetencionDTO): Promise<void> => {
    await apiClient.post('/retenciones', dto);
  },

  update: async (id: number, dto: CrearRetencionDTO): Promise<void> => {
    await apiClient.put(`/retenciones/${id}`, dto);
  },

  deactivate: async (id: number): Promise<void> => {
    await apiClient.delete(`/retenciones/${id}`);
  },
};

export const conceptosRetencionApi = {
  getAll: async (): Promise<ConceptoRetencionDTO[]> => {
    const response = await apiClient.get<ConceptoRetencionDTO[]>('/impuestos/conceptos-retencion');
    return response.data;
  },

  create: async (dto: CrearConceptoRetencionDTO): Promise<ConceptoRetencionDTO> => {
    const response = await apiClient.post<ConceptoRetencionDTO>('/impuestos/conceptos-retencion', dto);
    return response.data;
  },

  update: async (id: number, dto: EditarConceptoRetencionDTO): Promise<void> => {
    await apiClient.put(`/impuestos/conceptos-retencion/${id}`, dto);
  },

  deactivate: async (id: number): Promise<void> => {
    await apiClient.delete(`/impuestos/conceptos-retencion/${id}`);
  },
};
