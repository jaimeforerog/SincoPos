import { apiClient } from './client';

export interface ReglaEticaDto {
  id: number;
  empresaId: number | null;
  nombre: string;
  contexto: string;
  condicion: string;
  valorLimite: number;
  accion: string;
  mensaje: string | null;
  activo: boolean;
  fechaCreacion: string;
}

export interface CrearReglaEticaDto {
  nombre: string;
  contexto: string;
  condicion: string;
  valorLimite: number;
  accion: string;
  mensaje?: string;
  activo: boolean;
}

export interface ActivacionReglaEticaDto {
  id: number;
  reglaEticaId: number;
  nombreRegla: string;
  ventaId: number | null;
  sucursalId: number | null;
  detalle: string | null;
  accionTomada: string;
  fechaActivacion: string;
}

export const eticasApi = {
  getAll: () =>
    apiClient.get<ReglaEticaDto[]>('/eticas').then((r) => r.data),

  getById: (id: number) =>
    apiClient.get<ReglaEticaDto>(`/eticas/${id}`).then((r) => r.data),

  create: (dto: CrearReglaEticaDto) =>
    apiClient.post<ReglaEticaDto>('/eticas', dto).then((r) => r.data),

  update: (id: number, dto: CrearReglaEticaDto) =>
    apiClient.put<ReglaEticaDto>(`/eticas/${id}`, dto).then((r) => r.data),

  delete: (id: number) =>
    apiClient.delete(`/eticas/${id}`),

  getActivaciones: (reglaId?: number, take = 50) =>
    apiClient
      .get<ActivacionReglaEticaDto[]>('/eticas/activaciones', { params: { reglaId, take } })
      .then((r) => r.data),
};
