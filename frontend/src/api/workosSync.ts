import apiClient from './client';

export interface WorkOsSyncResult {
  empresasSincronizadas: number;
  empresasFallidas: number;
  usuariosSincronizados: number;
}

export const workosSyncApi = {
  sincronizar: () =>
    apiClient.post<WorkOsSyncResult>('/workos-sync').then(r => r.data),
};
