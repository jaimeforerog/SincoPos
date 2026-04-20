import apiClient from './client';
import type {
  OrdenCompraDTO,
  CrearOrdenCompraDTO,
  ActualizarOrdenCompraDTO,
  AprobarOrdenCompraDTO,
  RechazarOrdenCompraDTO,
  RecibirOrdenCompraDTO,
  CancelarOrdenCompraDTO,
  CrearDevolucionCompraDTO,
  DevolucionCompraDTO,
  ErpOutboxErrorDTO,
  PaginatedResult,
} from '@/types/api';

export const comprasApi = {
  /**
   * Listar órdenes de compra con filtros y paginación
   */
  getAll: async (params?: {
    sucursalId?: number;
    proveedorId?: number;
    estado?: string;
    desde?: string;
    hasta?: string;
    page?: number;
    pageSize?: number;
  }) => {
    const response = await apiClient.get<PaginatedResult<OrdenCompraDTO>>('/compras', { params });
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
   * Actualizar una orden de compra en estado Pendiente
   * (campos generales y/o líneas de insumos)
   */
  update: async (id: number, data: ActualizarOrdenCompraDTO) => {
    const response = await apiClient.put<OrdenCompraDTO>(`/compras/${id}`, data);
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

  crearDevolucion: async (id: number, data: CrearDevolucionCompraDTO): Promise<DevolucionCompraDTO> => {
    const response = await apiClient.post<DevolucionCompraDTO>(`/compras/${id}/devolucion`, data);
    return response.data;
  },

  obtenerDevoluciones: async (id: number): Promise<DevolucionCompraDTO[]> => {
    const response = await apiClient.get<DevolucionCompraDTO[]>(`/compras/${id}/devoluciones`);
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
