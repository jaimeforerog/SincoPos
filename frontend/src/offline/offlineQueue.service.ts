import apiClient from '@/api/client';
import { openPosDb } from './posIdb';
import {
  getPendingVentas,
  getPendingCount,
  getFailedCount,
  markVentaSyncing,
  markVentaSynced,
  markVentaFailed,
  clearFailedVentas,
} from './posCache.service';
import { useOfflineStore } from '@/stores/offline.store';
import type { VentaDTO } from '@/types/api';

export interface SyncResult {
  synced: number;
  failed: number;
  tokenExpired: boolean;
  errors: Array<{ localId: string; mensaje: string }>;
}

const MAX_INTENTOS = 3;

// Lock a nivel de módulo: evita llamadas concurrentes desde múltiples instancias del hook
let syncInProgress = false;

/** Envía una venta al servidor y retorna el VentaDTO creado */
async function postVenta(payload: unknown): Promise<VentaDTO> {
  const response = await apiClient.post<VentaDTO>('/ventas', payload);
  return response.data;
}

/** Refresca los contadores en el store de Zustand */
async function refreshCounts(): Promise<void> {
  const db = await openPosDb();
  const [pending, failed] = await Promise.all([getPendingCount(db), getFailedCount(db)]);
  useOfflineStore.getState().setPendingCount(pending);
  useOfflineStore.getState().setFailedCount(failed);
}

/**
 * Encola una venta para ser enviada cuando haya conexión.
 * Retorna el localId asignado.
 */
export async function enqueueVenta(
  payload: Parameters<typeof import('./posCache.service').enqueueVenta>[1]
): Promise<string> {
  const db = await openPosDb();
  const { enqueueVenta: enqueue } = await import('./posCache.service');
  const localId = await enqueue(db, payload);
  await refreshCounts();
  return localId;
}

/**
 * Sincroniza todas las ventas pendientes.
 * - Las ventas con token expirado (401) detienen la sync.
 * - Las ventas con error de stock (400) se marcan como fallidas sin reintentar.
 * - Los errores de red incrementan `intentos`; al llegar a MAX_INTENTOS se marcan fallidas.
 */
export async function discardFailedVentas(): Promise<void> {
  const db = await openPosDb();
  await clearFailedVentas(db);
  await refreshCounts();
  useOfflineStore.getState().resetSyncStatus();
}

export async function syncPending(): Promise<SyncResult> {
  if (syncInProgress) return { synced: 0, failed: 0, tokenExpired: false, errors: [] };
  syncInProgress = true;

  try {
    return await _doSync();
  } finally {
    syncInProgress = false;
  }
}

async function _doSync(): Promise<SyncResult> {
  const store = useOfflineStore.getState();
  const token = sessionStorage.getItem('access_token');

  const result: SyncResult = { synced: 0, failed: 0, tokenExpired: false, errors: [] };

  // Sin token no tiene sentido intentar
  if (!token) {
    store.setSyncError('Sin sesión activa. Inicia sesión para sincronizar.');
    return { ...result, tokenExpired: true };
  }

  const db = await openPosDb();
  const pendientes = await getPendingVentas(db);

  if (pendientes.length === 0) {
    return result;
  }

  store.setSyncing(true);

  for (const venta of pendientes) {
    // Lock optimista: marcar como syncing para evitar doble envío desde otra pestaña
    await markVentaSyncing(db, venta.localId);

    try {
      const ventaServidor = await postVenta(venta.payload);
      await markVentaSynced(db, venta.localId, ventaServidor.id);
      result.synced++;
    } catch (err: unknown) {
      const apiError = err as { statusCode?: number; message?: string };
      const status = apiError?.statusCode ?? 0;
      const mensaje = apiError?.message ?? 'Error desconocido';

      if (status === 401) {
        // Token expirado — restaurar a pending y detener sync
        await markVentaFailed(db, venta.localId, 'Sesión expirada al sincronizar');
        result.failed++;
        result.tokenExpired = true;
        result.errors.push({ localId: venta.localId, mensaje: 'Sesión expirada' });
        store.setSyncError('Tu sesión expiró. Inicia sesión para sincronizar las ventas pendientes.');
        break;
      }

      if (status === 400) {
        // Error de negocio (stock insuficiente, producto inválido, etc.) — no reintentar
        await markVentaFailed(db, venta.localId, mensaje);
        result.failed++;
        result.errors.push({ localId: venta.localId, mensaje });
        continue;
      }

      // Error de red o 5xx — incrementar intentos
      const nuevosIntentos = venta.intentos + 1;
      if (nuevosIntentos >= MAX_INTENTOS) {
        await markVentaFailed(db, venta.localId, `${mensaje} (${MAX_INTENTOS} intentos)`);
        result.failed++;
        result.errors.push({ localId: venta.localId, mensaje });
      } else {
        // Dejar como pending para el próximo intento (markVentaFailed incrementa intentos)
        await markVentaFailed(db, venta.localId, mensaje);
        // Restaurar a pending si aún tiene intentos restantes
        const { markVentaPendingRetry } = await import('./posCache.service');
        await markVentaPendingRetry(db, venta.localId);
      }
    }
  }

  await refreshCounts();

  if (!result.tokenExpired) {
    if (result.failed === 0) {
      store.setSyncSuccess(new Date().toISOString());
    } else {
      const detalle = result.errors.map((e) => e.mensaje).join(' | ');
      store.setSyncError(
        `${result.failed} venta(s) no sincronizada(s): ${detalle}`
      );
    }
  }

  return result;
}

/** Inicializa los contadores del store al montar la app */
export async function initOfflineCounts(): Promise<void> {
  try {
    await refreshCounts();
  } catch {
    // IndexedDB puede no estar disponible en SSR/tests — ignorar silenciosamente
  }
}
