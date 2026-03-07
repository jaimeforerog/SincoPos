import { apiClient } from './client';
import type { ActivityLogFullDTO, PaginatedResult, DashboardActivityDTO } from '@/types/api';

export interface ActivityLogFilters {
  fechaDesde?: string;
  fechaHasta?: string;
  usuarioEmail?: string;
  tipo?: number;
  accion?: string;
  sucursalId?: number;
  tipoEntidad?: string;
  entidadId?: string;
  exitosa?: boolean;
  pageNumber?: number;
  pageSize?: number;
}

export const activityLogsApi = {
  getLogs: async (filters: ActivityLogFilters = {}): Promise<PaginatedResult<ActivityLogFullDTO>> => {
    const params = new URLSearchParams();
    if (filters.fechaDesde)   params.set('fechaDesde', filters.fechaDesde);
    if (filters.fechaHasta)   params.set('fechaHasta', filters.fechaHasta);
    if (filters.usuarioEmail) params.set('usuarioEmail', filters.usuarioEmail);
    if (filters.tipo != null) params.set('tipo', String(filters.tipo));
    if (filters.accion)       params.set('accion', filters.accion);
    if (filters.sucursalId)   params.set('sucursalId', String(filters.sucursalId));
    if (filters.tipoEntidad)  params.set('tipoEntidad', filters.tipoEntidad);
    if (filters.entidadId)    params.set('entidadId', filters.entidadId);
    if (filters.exitosa != null) params.set('exitosa', String(filters.exitosa));
    params.set('pageNumber', String(filters.pageNumber ?? 1));
    params.set('pageSize',   String(filters.pageSize   ?? 50));

    const res = await apiClient.get<PaginatedResult<ActivityLogFullDTO>>(
      `/ActivityLogs?${params.toString()}`
    );
    return res.data;
  },

  getDashboard: async (fechaDesde?: string, fechaHasta?: string, sucursalId?: number): Promise<DashboardActivityDTO> => {
    const params = new URLSearchParams();
    if (fechaDesde) params.set('fechaDesde', fechaDesde);
    if (fechaHasta) params.set('fechaHasta', fechaHasta);
    if (sucursalId) params.set('sucursalId', String(sucursalId));
    const res = await apiClient.get<DashboardActivityDTO>(`/ActivityLogs/dashboard?${params.toString()}`);
    return res.data;
  },

  getTipos: async (): Promise<Record<number, string>> => {
    const res = await apiClient.get<Record<number, string>>('/ActivityLogs/tipos');
    return res.data;
  },
};
