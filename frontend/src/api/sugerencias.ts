import apiClient from './client';
import type { AutomaticActionDTO } from '@/types/api';

export const sugerenciasApi = {
  /**
   * Capa 10 — Explicabilidad.
   * Sugerencias de reabastecimiento con reason, dataSource y confidence.
   */
  getReabastecimiento: async (sucursalId: number): Promise<AutomaticActionDTO[]> => {
    const response = await apiClient.get<AutomaticActionDTO[]>(
      '/sugerencias/reabastecimiento',
      { params: { sucursalId } }
    );
    return response.data;
  },
};
