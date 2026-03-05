import apiClient from './client';
import type {
  ReporteVentasDTO,
  ReporteInventarioValorizadoDTO,
  ReporteCajaDTO,
} from '@/types/api';

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
      '/api/reportes/ventas',
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
      '/api/reportes/inventario-valorizado',
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
      `/api/reportes/caja/${cajaId}`,
      { params }
    );
    return response.data;
  },

  /**
   * Obtener dashboard con métricas del día
   */
  dashboard: async (params?: { sucursalId?: number }) => {
    const response = await apiClient.get('/api/reportes/dashboard', { params });
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
    const response = await apiClient.get('/api/reportes/top-productos', { params });
    return response.data;
  },
};
