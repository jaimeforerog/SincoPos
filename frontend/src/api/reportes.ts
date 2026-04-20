import apiClient from './client';
import type {
  ReporteVentasDTO,
  ReporteInventarioValorizadoDTO,
  ReporteCajaDTO,
  ReporteKardexDTO,
  ReporteAuditoriaComprasDTO,
} from '@/types/api';
import type { HistorialEntidadDTO } from '@/types/api';

export const reportesApi = {
  /**
   * Obtener reporte de ventas por período
   */
  ventas: async (params: {
    fechaDesde: string; // formato: YYYY-MM-DD
    fechaHasta: string; // formato: YYYY-MM-DD
    sucursalId?: number;
    metodoPago?: number; // 0=Efectivo, 1=Tarjeta, 2=Transferencia
  }) => {
    const response = await apiClient.get<ReporteVentasDTO>(
      '/reportes/ventas',
      { params }
    );
    return response.data;
  },

  /**
   * Obtener reporte de inventario valorizado
   */
  inventarioValorizado: async (params?: {
    sucursalId?: number;
    categoriaId?: number;
    soloConStock?: boolean;
  }) => {
    const response = await apiClient.get<ReporteInventarioValorizadoDTO>(
      '/reportes/inventario-valorizado',
      { params }
    );
    return response.data;
  },

  /**
   * Obtener reporte de movimientos de caja
   */
  caja: async (
    cajaId: number,
    params?: {
      fechaDesde?: string; // formato: YYYY-MM-DD
      fechaHasta?: string; // formato: YYYY-MM-DD
    }
  ) => {
    const response = await apiClient.get<ReporteCajaDTO>(
      `/reportes/caja/${cajaId}`,
      { params }
    );
    return response.data;
  },

  /**
   * Obtener dashboard con métricas del día
   */
  dashboard: async (params?: { sucursalId?: number }) => {
    const response = await apiClient.get('/reportes/dashboard', { params });
    return response.data;
  },

  /**
   * Obtener top productos más vendidos
   */
  topProductos: async (params: {
    fechaDesde: string; // formato: YYYY-MM-DD
    fechaHasta: string; // formato: YYYY-MM-DD
    sucursalId?: number;
    limite?: number;
  }) => {
    const response = await apiClient.get('/reportes/top-productos', { params });
    return response.data;
  },

  /**
   * Obtener el kardex de un producto específico
   */
  kardex: async (params: {
    productoId: string;
    sucursalId: number;
    fechaDesde: string; // formato: YYYY-MM-DD
    fechaHasta: string; // formato: YYYY-MM-DD
  }) => {
    const response = await apiClient.get<ReporteKardexDTO>('/reportes/kardex', {
      params,
    });
    return response.data;
  },

  auditoriaCompras: async (params: {
    fechaDesde: string;
    fechaHasta: string;
    sucursalId?: number;
    proveedorId?: number;
    usuarioEmail?: string;
    accion?: string;
    soloErrores?: boolean;
    pageNumber?: number;
    pageSize?: number;
  }) => {
    const response = await apiClient.get<ReporteAuditoriaComprasDTO>(
      '/reportes/auditoria-compras',
      { params }
    );
    return response.data;
  },

  historialOrden: async (ordenId: number) => {
    const response = await apiClient.get<HistorialEntidadDTO>(
      `/reportes/auditoria-compras/orden/${ordenId}`
    );
    return response.data;
  },
};
