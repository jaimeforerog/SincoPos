import { apiClient } from './client';
import type {
  InventarioStockDTO,
  AlertaStockDTO,
  MovimientoInventarioDTO,
  EntradaInventarioDTO,
  AjusteInventarioDTO,
  DevolucionProveedorDTO,
  PaginatedResult,
} from '@/types/api';

export const inventarioApi = {
  /**
   * Obtener stock actual con filtros
   */
  getStock: async (params?: {
    sucursalId?: number;
    productoId?: string;
    soloConStock?: boolean;
  }) => {
    const response = await apiClient.get<InventarioStockDTO[]>('/inventario', { params });
    return response.data;
  },

  /**
   * Stock proyectado a una fecha de corte (para ventas retroactivas).
   * Devuelve las mismas cantidades que getStock pero ajustadas al momento del corte.
   */
  getStockHistorico: async (params: {
    sucursalId: number;
    fecha: string; // ISO UTC string
  }) => {
    const response = await apiClient.get<InventarioStockDTO[]>('/inventario/historico', { params });
    return response.data;
  },

  /**
   * Alias para getStock
   */
  obtenerStock: async (params?: {
    sucursalId?: number;
    productoId?: string;
    soloConStock?: boolean;
  }) => {
    const response = await apiClient.get<InventarioStockDTO[]>('/inventario', { params });
    return response.data;
  },

  /**
   * Obtener alertas de stock bajo
   */
  getAlertas: async (sucursalId?: number) => {
    const response = await apiClient.get<AlertaStockDTO[]>('/inventario/alertas', {
      params: { sucursalId },
    });
    return response.data;
  },

  /**
   * Obtener historial de movimientos
   */
  getMovimientos: async (params?: {
    sucursalId?: number;
    productoId?: string;
    fechaDesde?: string;
    fechaHasta?: string;
    page?: number;
    pageSize?: number;
  }) => {
    const response = await apiClient.get<PaginatedResult<MovimientoInventarioDTO>>(
      '/inventario/movimientos',
      { params }
    );
    return response.data;
  },

  /**
   * Registrar entrada de mercancía
   */
  registrarEntrada: async (dto: EntradaInventarioDTO) => {
    const response = await apiClient.post('/inventario/entrada', dto);
    return response.data;
  },

  /**
   * Ajustar inventario (conteo físico)
   */
  ajustarInventario: async (dto: AjusteInventarioDTO) => {
    const response = await apiClient.post('/inventario/ajuste', dto);
    return response.data;
  },

  /**
   * Registrar devolución a proveedor
   */
  devolucionProveedor: async (dto: DevolucionProveedorDTO) => {
    const response = await apiClient.post('/inventario/devolucion-proveedor', dto);
    return response.data;
  },

  /**
   * Actualizar stock mínimo
   */
  actualizarStockMinimo: async (productoId: string, sucursalId: number, stockMinimo: number) => {
    const response = await apiClient.put('/inventario/stock-minimo', null, {
      params: { productoId, sucursalId, stockMinimo },
    });
    return response.data;
  },
};
