import { apiClient } from './client';
import type { LoteDTO, AlertaLoteDTO, ActualizarLoteDTO, TrazabilidadLoteDTO, ReporteLotesDTO } from '@/types/api';

export const lotesApi = {
  /**
   * Obtener lotes de un producto en una sucursal (ordenados FEFO)
   */
  obtenerLotes: async (productoId: string, sucursalId: number, soloVigentes = true) => {
    const response = await apiClient.get<LoteDTO[]>('/lotes', {
      params: { productoId, sucursalId, soloVigentes },
    });
    return response.data;
  },

  /**
   * Lotes próximos a vencer en una sucursal
   */
  proximosAVencer: async (sucursalId: number, diasAnticipacion = 30) => {
    const response = await apiClient.get<AlertaLoteDTO[]>('/lotes/proximos-vencer', {
      params: { sucursalId, diasAnticipacion },
    });
    return response.data;
  },

  /**
   * Todas las alertas de vencimiento (todas las sucursales)
   */
  obtenerAlertas: async () => {
    const response = await apiClient.get<AlertaLoteDTO[]>('/lotes/alertas');
    return response.data;
  },

  /**
   * Actualizar número de lote y/o fecha de vencimiento
   */
  actualizar: async (id: number, dto: ActualizarLoteDTO) => {
    const response = await apiClient.put<LoteDTO>(`/lotes/${id}`, dto);
    return response.data;
  },

  trazabilidad: async (id: number) => {
    const response = await apiClient.get<TrazabilidadLoteDTO>(`/lotes/${id}/trazabilidad`);
    return response.data;
  },

  reporte: async (params?: {
    sucursalId?: number;
    productoId?: string;
    soloConStock?: boolean;
    estadoVencimiento?: string;
    fechaVencimientoDesde?: string;
    fechaVencimientoHasta?: string;
    page?: number;
    pageSize?: number;
  }) => {
    const response = await apiClient.get<ReporteLotesDTO>('/lotes/reporte', { params });
    return response.data;
  },
};
