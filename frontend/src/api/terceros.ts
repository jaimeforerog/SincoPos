import apiClient from './client';
import type {
  TerceroDTO,
  CrearTerceroDTO,
  ActualizarTerceroDTO,
  TerceroActividadDTO,
  AgregarActividadDTO,
  ResultadoImportacionTercerosDTO,
  PaginatedResult,
  ClienteHistorialDTO,
} from '@/types/api';

export const tercerosApi = {
  getAll: async (params?: {
    q?: string;
    tipoTercero?: string;
    incluirInactivos?: boolean;
    esCliente?: boolean;
    esProveedor?: boolean;
    activo?: boolean;
    page?: number;
    pageSize?: number;
  }) => {
    const response = await apiClient.get<PaginatedResult<TerceroDTO>>('/terceros', { params });
    return response.data;
  },

  getById: async (id: number) => {
    const response = await apiClient.get<TerceroDTO>(`/terceros/${id}`);
    return response.data;
  },

  create: async (dto: CrearTerceroDTO) => {
    const response = await apiClient.post<TerceroDTO>('/terceros', dto);
    return response.data;
  },

  update: async (id: number, dto: ActualizarTerceroDTO) => {
    await apiClient.put(`/terceros/${id}`, dto);
  },

  deactivate: async (id: number) => {
    await apiClient.delete(`/terceros/${id}`);
  },

  activate: async (id: number) => {
    await apiClient.patch(`/terceros/${id}/activar`);
  },

  calcularDV: async (nit: string): Promise<{ dv: string }> => {
    const response = await apiClient.get<{ dv: string }>('/terceros/calcular-dv', {
      params: { nit },
    });
    return response.data;
  },

  agregarActividad: async (id: number, dto: AgregarActividadDTO) => {
    const response = await apiClient.post<TerceroActividadDTO>(
      `/terceros/${id}/actividades`,
      dto,
    );
    return response.data;
  },

  eliminarActividad: async (id: number, actividadId: number) => {
    await apiClient.delete(`/terceros/${id}/actividades/${actividadId}`);
  },

  establecerPrincipal: async (id: number, actividadId: number) => {
    await apiClient.patch(`/terceros/${id}/actividades/${actividadId}/principal`);
  },

  descargarPlantilla: async () => {
    const response = await apiClient.get('/terceros/plantilla', {
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
      '/terceros/importar',
      formData,
      { headers: { 'Content-Type': 'multipart/form-data' } },
    );
    return response.data;
  },

  /** Capa 4 — historial de compras acumulado de un cliente */
  getHistorial: async (clienteId: number): Promise<ClienteHistorialDTO | null> => {
    const response = await apiClient.get<ClienteHistorialDTO>(
      `/terceros/${clienteId}/historial`,
    );
    // 204 No Content → sin historial aún
    return response.status === 204 ? null : response.data;
  },
};
