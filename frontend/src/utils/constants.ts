export const APP_NAME = 'SincoPos';
export const APP_VERSION = '1.0.0';

export const METODOS_PAGO = [
  { value: 'Efectivo', label: 'Efectivo' },
  { value: 'Tarjeta', label: 'Tarjeta' },
  { value: 'Transferencia', label: 'Transferencia' },
] as const;

export const ESTADOS_VENTA = [
  { value: 'Completada', label: 'Completada', color: 'success' },
  { value: 'Cancelada', label: 'Cancelada', color: 'error' },
  { value: 'DevueltaParcial', label: 'Devuelta Parcial', color: 'warning' },
  { value: 'DevueltaTotal', label: 'Devuelta Total', color: 'error' },
] as const;

export const ESTADOS_ORDEN_COMPRA = [
  { value: 'Pendiente', label: 'Pendiente', color: 'warning' },
  { value: 'Aprobada', label: 'Aprobada', color: 'info' },
  { value: 'Rechazada', label: 'Rechazada', color: 'error' },
  { value: 'RecibidaParcial', label: 'Recibida Parcial', color: 'primary' },
  { value: 'RecibidaTotal', label: 'Recibida Total', color: 'success' },
  { value: 'Cancelada', label: 'Cancelada', color: 'default' },
] as const;

export const ESTADOS_TRASLADO = [
  { value: 'Pendiente', label: 'Pendiente', color: 'warning' },
  { value: 'EnTransito', label: 'En Tránsito', color: 'info' },
  { value: 'Recibido', label: 'Recibido', color: 'success' },
  { value: 'Rechazado', label: 'Rechazado', color: 'error' },
  { value: 'Cancelado', label: 'Cancelado', color: 'default' },
] as const;

export const ESTADOS_CAJA = [
  { value: 'Abierta', label: 'Abierta', color: 'success' },
  { value: 'Cerrada', label: 'Cerrada', color: 'default' },
] as const;

export const DEFAULT_PAGE_SIZE = 25;
export const PAGE_SIZE_OPTIONS = [10, 25, 50, 100];
