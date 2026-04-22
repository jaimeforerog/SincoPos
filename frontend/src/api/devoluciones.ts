import { apiClient } from './client';
import type { DevolucionVentaDTO, CrearDevolucionParcialDTO, TerceroDTO } from '@/types/api';

export const devolucionesApi = {
  /**
   * Clientes que tienen al menos una venta Completada en la sucursal.
   * Exclusivo para el flujo de devoluciones — no usar desde otros módulos.
   */
  getClientesConVentas: async (params: { sucursalId?: number; q?: string }) => {
    const response = await apiClient.get<TerceroDTO[]>('/ventas/clientes-con-ventas', { params });
    return response.data;
  },

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
