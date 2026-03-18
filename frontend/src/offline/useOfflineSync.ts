import { useEffect, useCallback } from 'react';
import { useOfflineStore } from '@/stores/offline.store';
import { syncPending, initOfflineCounts } from './offlineQueue.service';

/**
 * Hook que:
 * 1. Escucha eventos online/offline del navegador y actualiza el store.
 * 2. Dispara syncPending() automáticamente al recuperar la conexión.
 * 3. Inicializa los contadores pendientes/fallidos desde IndexedDB al montar.
 *
 * Debe montarse una sola vez, en un componente raíz (ej: App.tsx o POSPage.tsx).
 */
export function useOfflineSync() {
  const { isOnline, pendingCount, failedCount, isSyncing, syncStatus, lastSyncAt, lastSyncError } =
    useOfflineStore();

  const { setOnline } = useOfflineStore.getState();

  // Inicializar contadores desde IndexedDB al montar
  useEffect(() => {
    initOfflineCounts();
  }, []);

  // Suscribirse a eventos de red
  useEffect(() => {
    const handleOnline = () => {
      setOnline(true);
    };

    const handleOffline = () => {
      setOnline(false);
    };

    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);

    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, [setOnline]);

  // Sincronizar automáticamente al reconectar si hay pendientes
  useEffect(() => {
    if (isOnline && pendingCount > 0 && !isSyncing) {
      syncPending();
    }
  }, [isOnline, pendingCount, isSyncing]);

  const syncNow = useCallback(() => {
    if (!isSyncing) {
      syncPending();
    }
  }, [isSyncing]);

  return {
    isOnline,
    pendingCount,
    failedCount,
    isSyncing,
    syncStatus,
    lastSyncAt,
    lastSyncError,
    syncNow,
  };
}
