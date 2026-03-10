import apiClient from './client';
import type {
  OrdenCompraDTO,
  CrearOrdenCompraDTO,
  AprobarOrdenCompraDTO,
  RechazarOrdenCompraDTO,
  RecibirOrdenCompraDTO,
  CancelarOrdenCompraDTO,
  ErpOutboxErrorDTO,
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
    const response = await apiClient.get<OrdenCompraDTO[]>('/compras', { params });
    return response.data;
  },

  /**
   * Obtener detalle de una orden de compra
   */
  getById: async (id: number) => {
    const response = await apiClient.get<OrdenCompraDTO>(`/compras/${id}`);
    return response.data;
  },

  /**
   * Crear nueva orden de compra
   */
  create: async (data: CrearOrdenCompraDTO) => {
    const response = await apiClient.post<OrdenCompraDTO>('/compras', data);
    return response.data;
  },

  /**
   * Aprobar orden de compra (Pendiente → Aprobada)
   */
  aprobar: async (id: number, data?: AprobarOrdenCompraDTO) => {
    const response = await apiClient.post(`/compras/${id}/aprobar`, data || {});
    return response.data;
  },

  /**
   * Rechazar orden de compra (Pendiente → Rechazada)
   */
  rechazar: async (id: number, data: RechazarOrdenCompraDTO) => {
    const response = await apiClient.post(`/compras/${id}/rechazar`, data);
    return response.data;
  },

  /**
   * Recibir mercancía (Aprobada → RecibidaParcial o RecibidaCompleta)
   */
  recibir: async (id: number, data: RecibirOrdenCompraDTO) => {
    const response = await apiClient.post(`/compras/${id}/recibir`, data);
    return response.data;
  },

  /**
   * Cancelar orden de compra
   */
  cancelar: async (id: number, data: CancelarOrdenCompraDTO) => {
    const response = await apiClient.post(`/compras/${id}/cancelar`, data);
    return response.data;
  },

  /**
   * Reintentar sincronización ERP de un mensaje outbox fallido
   */
  reintentarErp: async (outboxId: number) => {
    const response = await apiClient.post(`/integracion-erp/reintentar-outbox/${outboxId}`);
    return response.data;
  },

  /**
   * Obtener mensajes outbox con error
   */
  getErroresErp: async () => {
    const response = await apiClient.get<ErpOutboxErrorDTO[]>('/integracion-erp/outbox/errores');
    return response.data;
  },
};
