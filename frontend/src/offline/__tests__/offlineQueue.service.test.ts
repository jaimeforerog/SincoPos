import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import 'fake-indexeddb/auto';

// ── Mocks declarados ANTES de los imports para que vitest los hoiste ──────────
vi.mock('@/api/client', () => ({
  default: { post: vi.fn() },
}));

vi.mock('@/stores/offline.store', () => {
  const store = {
    isOnline: true,
    pendingCount: 0,
    failedCount: 0,
    isSyncing: false,
    syncStatus: 'idle',
    lastSyncAt: null,
    lastSyncError: null,
    setOnline: vi.fn(),
    setPendingCount: vi.fn((n: number) => { store.pendingCount = n; }),
    setFailedCount: vi.fn((n: number) => { store.failedCount = n; }),
    setSyncing: vi.fn(),
    setSyncSuccess: vi.fn(),
    setSyncError: vi.fn(),
    resetSyncStatus: vi.fn(),
  };
  return {
    useOfflineStore: Object.assign(vi.fn(() => store), {
      getState: vi.fn(() => store),
    }),
  };
});

// ── Imports estáticos tras los mocks ─────────────────────────────────────────
import apiClient from '@/api/client';
import { useOfflineStore } from '@/stores/offline.store';
import { openPosDb, resetDbInstance, setDbNameForTest } from '../posIdb';
import {
  getPendingCount,
  getAllQueuedVentas,
  enqueueVenta as cacheEnqueue,
} from '../posCache.service';
import { syncPending, enqueueVenta as enqueueVentaService } from '../offlineQueue.service';
import type { PosDb } from '../posIdb';
import type { CrearVentaDTO, VentaDTO } from '@/types/api';

// ── Tipos helpers ────────────────────────────────────────────────────────────
const apiPost = apiClient.post as ReturnType<typeof vi.fn>;
const offlineStore = useOfflineStore.getState();

const makeVentaPayload = (override?: Partial<CrearVentaDTO>): CrearVentaDTO => ({
  sucursalId: 1,
  cajaId: 1,
  metodoPago: 0,
  montoPagado: 10000,
  lineas: [{ productoId: 'prod-1', cantidad: 2, descuento: 0 }],
  ...override,
});

const makeVentaDTO = (id = 42): VentaDTO =>
  ({
    id,
    sucursalId: 1,
    cajaId: 1,
    estado: 'Completada',
    total: 7000,
    subtotal: 6000,
    totalImpuestos: 1000,
    totalDescuentos: 0,
    metodoPago: 0,
    montoPagado: 10000,
    cambio: 3000,
    fecha: new Date().toISOString(),
    lineas: [],
  } as unknown as VentaDTO);

// ── Suite ────────────────────────────────────────────────────────────────────
describe('offlineQueue.service', () => {
  let db: PosDb;
  let testCounter = 0;

  beforeEach(async () => {
    testCounter++;
    setDbNameForTest(`sincopos-queue-test-${testCounter}`);
    resetDbInstance();
    db = await openPosDb();

    vi.clearAllMocks();
    offlineStore.pendingCount = 0;
    offlineStore.failedCount = 0;

    sessionStorage.setItem('access_token', 'test-token-valid');
  });

  afterEach(() => {
    sessionStorage.removeItem('access_token');
  });

  // ─── enqueueVenta ─────────────────────────────────────────────────────────

  describe('enqueueVenta', () => {
    it('guarda la venta en IndexedDB y retorna localId con prefijo offline-', async () => {
      const localId = await enqueueVentaService(makeVentaPayload());
      expect(localId).toMatch(/^offline-/);

      const count = await getPendingCount(db);
      expect(count).toBe(1);
    });

    it('actualiza el pendingCount en el store', async () => {
      await enqueueVentaService(makeVentaPayload());
      expect(offlineStore.setPendingCount).toHaveBeenCalledWith(1);
    });

    it('IDs son únicos para múltiples ventas', async () => {
      const id1 = await enqueueVentaService(makeVentaPayload());
      const id2 = await enqueueVentaService(makeVentaPayload());
      expect(id1).not.toBe(id2);
    });
  });

  // ─── syncPending ──────────────────────────────────────────────────────────

  describe('syncPending', () => {
    it('retorna synced=0 si no hay ventas pendientes', async () => {
      const result = await syncPending();
      expect(result.synced).toBe(0);
      expect(result.failed).toBe(0);
      expect(apiPost).not.toHaveBeenCalled();
    });

    it('no sincroniza si no hay token en sessionStorage', async () => {
      sessionStorage.removeItem('access_token');
      await cacheEnqueue(db, makeVentaPayload());

      const result = await syncPending();
      expect(result.tokenExpired).toBe(true);
      expect(apiPost).not.toHaveBeenCalled();
    });

    it('envía POST y marca como synced cuando el servidor responde 200', async () => {
      apiPost.mockResolvedValueOnce({ data: makeVentaDTO(99) });
      await cacheEnqueue(db, makeVentaPayload());

      const result = await syncPending();

      expect(result.synced).toBe(1);
      expect(result.failed).toBe(0);
      expect(apiPost).toHaveBeenCalledWith('/ventas', expect.any(Object));

      const todas = await getAllQueuedVentas(db);
      expect(todas[0].estado).toBe('synced');
      expect(todas[0].ventaServerId).toBe(99);
    });

    it('marca como failed con 400 (stock insuficiente) sin reintentar', async () => {
      apiPost.mockRejectedValueOnce({ statusCode: 400, message: 'Stock insuficiente' });
      await cacheEnqueue(db, makeVentaPayload());

      const result = await syncPending();

      expect(result.failed).toBe(1);
      expect(result.errors[0].mensaje).toBe('Stock insuficiente');
      expect(apiPost).toHaveBeenCalledTimes(1);

      const todas = await getAllQueuedVentas(db);
      expect(todas[0].estado).toBe('failed');
    });

    it('detiene la sync y marca tokenExpired con 401', async () => {
      apiPost.mockRejectedValueOnce({ statusCode: 401, message: 'Unauthorized' });
      await cacheEnqueue(db, makeVentaPayload());
      await cacheEnqueue(db, makeVentaPayload());

      const result = await syncPending();

      expect(result.tokenExpired).toBe(true);
      expect(apiPost).toHaveBeenCalledTimes(1);
      expect(offlineStore.setSyncError).toHaveBeenCalledWith(
        expect.stringContaining('sesión')
      );
    });

    it('con error de red deja como pending si quedan intentos (intentos=1)', async () => {
      apiPost.mockRejectedValueOnce({ statusCode: 0, message: 'Network Error' });
      await cacheEnqueue(db, makeVentaPayload());

      await syncPending();

      const todas = await getAllQueuedVentas(db);
      expect(todas[0].estado).toBe('pending');
      expect(todas[0].intentos).toBe(1);
    });

    it('marca como failed definitivo al alcanzar MAX_INTENTOS (3)', async () => {
      apiPost.mockRejectedValue({ statusCode: 0, message: 'Network Error' });
      await cacheEnqueue(db, makeVentaPayload());

      await syncPending();
      await syncPending();
      await syncPending();

      const todas = await getAllQueuedVentas(db);
      expect(todas[0].estado).toBe('failed');
    });

    it('sincroniza múltiples ventas y reporta totales correctos', async () => {
      apiPost
        .mockResolvedValueOnce({ data: makeVentaDTO(1) })
        .mockRejectedValueOnce({ statusCode: 400, message: 'Producto inactivo' })
        .mockResolvedValueOnce({ data: makeVentaDTO(3) });

      await cacheEnqueue(db, makeVentaPayload());
      await cacheEnqueue(db, makeVentaPayload());
      await cacheEnqueue(db, makeVentaPayload());

      const result = await syncPending();

      expect(result.synced).toBe(2);
      expect(result.failed).toBe(1);
    });

    it('llama setSyncSuccess cuando todas se sincronizan sin error', async () => {
      apiPost.mockResolvedValueOnce({ data: makeVentaDTO(1) });
      await cacheEnqueue(db, makeVentaPayload());

      await syncPending();

      expect(offlineStore.setSyncSuccess).toHaveBeenCalledWith(expect.any(String));
    });

    it('llama setSyncError cuando hay al menos una fallida', async () => {
      apiPost.mockRejectedValueOnce({ statusCode: 400, message: 'Error' });
      await cacheEnqueue(db, makeVentaPayload());

      await syncPending();

      expect(offlineStore.setSyncError).toHaveBeenCalled();
    });
  });
});
