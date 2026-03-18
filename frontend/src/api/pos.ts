import apiClient from './client';
import type { TurnContextDTO } from '@/types/api';

export const posApi = {
  /**
   * Capa 3 — Repetición cero.
   * Retorna clientes recientes y órdenes pendientes para precargar el contexto del turno.
   */
  getContexto: async (sucursalId: number): Promise<TurnContextDTO> => {
    const response = await apiClient.get<TurnContextDTO>('/pos/contexto', {
      params: { sucursalId },
    });
    return response.data;
  },
};
