import apiClient from './client';
import type {
  OrdenCompraDTO,
  CrearOrdenCompraDTO,
  AprobarOrdenCompraDTO,
  RechazarOrdenCompraDTO,
  RecibirOrdenCompraDTO,
  CancelarOrdenCompraDTO,
} from '@/types/api';

export const comprasApi = {
  /**
   * Listar órdenes de compra con filtros
   */
  getAll: async (params?: {
    sucursalId?: number;
    proveedorId?: number;
    estado?: string;
    desde?: string;
    hasta?: string;
    limite?: number;
  }) => {
    const response = await apiClient.get<OrdenCompraDTO[]>('/api/compras', { params });
    return response.data;
  },

  /**
   * Obtener detalle de una orden de compra
   */
  getById: async (id: number) => {
    const response = await apiClient.get<OrdenCompraDTO>(`/api/compras/${id}`);
    return response.data;
  },

  /**
   * Crear nueva orden de compra
   */
  create: async (data: CrearOrdenCompraDTO) => {
    const response = await apiClient.post<OrdenCompraDTO>('/api/compras', data);
    return response.data;
  },

  /**
   * Aprobar orden de compra (Pendiente → Aprobada)
   */
  aprobar: async (id: number, data?: AprobarOrdenCompraDTO) => {
    const response = await apiClient.post(`/api/compras/${id}/aprobar`, data || {});
    return response.data;
  },

  /**
   * Rechazar orden de compra (Pendiente → Rechazada)
   */
  rechazar: async (id: number, data: RechazarOrdenCompraDTO) => {
    const response = await apiClient.post(`/api/compras/${id}/rechazar`, data);
    return response.data;
  },

  /**
   * Recibir mercancía (Aprobada → RecibidaParcial o RecibidaCompleta)
   */
  recibir: async (id: number, data: RecibirOrdenCompraDTO) => {
    const response = await apiClient.post(`/api/compras/${id}/recibir`, data);
    return response.data;
  },

  /**
   * Cancelar orden de compra
   */
  cancelar: async (id: number, data: CancelarOrdenCompraDTO) => {
    const response = await apiClient.post(`/api/compras/${id}/cancelar`, data);
    return response.data;
  },
};
