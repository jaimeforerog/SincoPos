import { apiClient } from './client';
import type { DevolucionVentaDTO, CrearDevolucionParcialDTO } from '@/types/api';

export const devolucionesApi = {
  /**
   * Crear una devolución parcial de productos de una venta
   */
  crearDevolucionParcial: async (ventaId: number, dto: CrearDevolucionParcialDTO) => {
    const response = await apiClient.post<DevolucionVentaDTO>(
      `/ventas/${ventaId}/devolucion-parcial`,
      dto
    );
    return response.data;
  },

  /**
   * Obtener todas las devoluciones de una venta específica
   */
  obtenerPorVenta: async (ventaId: number) => {
    const response = await apiClient.get<DevolucionVentaDTO[]>(
      `/ventas/${ventaId}/devoluciones`
    );
    return response.data;
  },

  /**
   * Obtener el detalle de una devolución específica
   */
  obtenerPorId: async (devolucionId: number) => {
    const response = await apiClient.get<DevolucionVentaDTO>(
      `/ventas/devoluciones/${devolucionId}`
    );
    return response.data;
  },
};
