import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import { useSnackbar } from 'notistack';
import { useAuthStore } from '@/stores/auth.store';
import type { NotificacionDto } from '@/types/notifications';

const MAX_NOTIFICATIONS = 50;

export function useNotifications() {
  const { enqueueSnackbar } = useSnackbar();
  const { isAuthenticated, activeSucursalId } = useAuthStore();
  const [notifications, setNotifications] = useState<NotificacionDto[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const prevSucursalRef = useRef<number | undefined>(undefined);

  const markAllRead = useCallback(() => setUnreadCount(0), []);

  useEffect(() => {
    if (!isAuthenticated) return;

    const hubBase = import.meta.env.VITE_API_URL ?? '';
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${hubBase}/hubs/notificaciones`, {
        accessTokenFactory: () => sessionStorage.getItem('access_token') ?? '',
      })
      .withAutomaticReconnect()
      .build();

    connection.on('Notificacion', (notif: NotificacionDto) => {
      setNotifications((prev) => [notif, ...prev].slice(0, MAX_NOTIFICATIONS));
      setUnreadCount((c) => c + 1);
      enqueueSnackbar(notif.mensaje, {
        variant: notif.nivel,
        autoHideDuration: notif.nivel === 'error' ? 8000 : 4000,
      });
    });

    connection
      .start()
      .then(() => {
        if (activeSucursalId != null) {
          connection.invoke('JoinSucursal', activeSucursalId).catch(console.error);
          prevSucursalRef.current = activeSucursalId;
        }
      })
      .catch((err) => console.error('SignalR connection error:', err));

    connectionRef.current = connection;

    return () => {
      connection.stop();
      connectionRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAuthenticated]);

  // Handle sucursal changes
  useEffect(() => {
    const conn = connectionRef.current;
    if (!conn || conn.state !== signalR.HubConnectionState.Connected) return;

    const prev = prevSucursalRef.current;
    if (prev === activeSucursalId) return;

    if (prev != null) {
      conn.invoke('LeaveSucursal', prev).catch(console.error);
    }
    if (activeSucursalId != null) {
      conn.invoke('JoinSucursal', activeSucursalId).catch(console.error);
    }
    prevSucursalRef.current = activeSucursalId;
  }, [activeSucursalId]);

  return { notifications, unreadCount, markAllRead };
}
