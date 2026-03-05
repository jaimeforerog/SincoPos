import apiClient from './client';
import type {
  TerceroDTO,
  CrearTerceroDTO,
  ActualizarTerceroDTO,
  TerceroActividadDTO,
  AgregarActividadDTO,
  ResultadoImportacionTercerosDTO,
} from '@/types/api';

export const tercerosApi = {
  getAll: async (params?: {
    q?: string;
    tipoTercero?: string;
    incluirInactivos?: boolean;
  }) => {
    const response = await apiClient.get<TerceroDTO[]>('/api/terceros', { params });
    return response.data;
  },

  getById: async (id: number) => {
    const response = await apiClient.get<TerceroDTO>(`/api/terceros/${id}`);
    return response.data;
  },

  create: async (dto: CrearTerceroDTO) => {
    const response = await apiClient.post<TerceroDTO>('/api/terceros', dto);
    return response.data;
  },

  update: async (id: number, dto: ActualizarTerceroDTO) => {
    await apiClient.put(`/api/terceros/${id}`, dto);
  },

  deactivate: async (id: number) => {
    await apiClient.delete(`/api/terceros/${id}`);
  },

  calcularDV: async (nit: string): Promise<{ dv: string }> => {
    const response = await apiClient.get<{ dv: string }>('/api/terceros/calcular-dv', {
      params: { nit },
    });
    return response.data;
  },

  agregarActividad: async (id: number, dto: AgregarActividadDTO) => {
    const response = await apiClient.post<TerceroActividadDTO>(
      `/api/terceros/${id}/actividades`,
      dto,
    );
    return response.data;
  },

  eliminarActividad: async (id: number, actividadId: number) => {
    await apiClient.delete(`/api/terceros/${id}/actividades/${actividadId}`);
  },

  establecerPrincipal: async (id: number, actividadId: number) => {
    await apiClient.patch(`/api/terceros/${id}/actividades/${actividadId}/principal`);
  },

  descargarPlantilla: async () => {
    const response = await apiClient.get('/api/terceros/plantilla', {
      responseType: 'blob',
    });
    const url = URL.createObjectURL(response.data);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'plantilla_terceros.xlsx';
    a.click();
    URL.revokeObjectURL(url);
  },

  importarExcel: async (archivo: File): Promise<ResultadoImportacionTercerosDTO> => {
    const formData = new FormData();
    formData.append('archivo', archivo);
    const response = await apiClient.post<ResultadoImportacionTercerosDTO>(
      '/api/terceros/importar',
      formData,
      { headers: { 'Content-Type': 'multipart/form-data' } },
    );
    return response.data;
  },
};
