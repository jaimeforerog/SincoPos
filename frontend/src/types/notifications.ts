export interface NotificacionDto {
  tipo: string;
  titulo: string;
  mensaje: string;
  nivel: 'info' | 'success' | 'warning' | 'error';
  timestamp: string;
  datos?: unknown;
}
