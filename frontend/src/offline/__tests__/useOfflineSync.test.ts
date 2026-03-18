import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';

// ── Mocks ────────────────────────────────────────────────────────────────────
vi.mock('../offlineQueue.service', () => ({
  syncPending: vi.fn().mockResolvedValue({
    synced: 0, failed: 0, tokenExpired: false, errors: [],
  }),
  initOfflineCounts: vi.fn().mockResolvedValue(undefined),
}));

// Store real de Zustand (no mockeado para probar la integración)
import { useOfflineStore } from '@/stores/offline.store';
import { useOfflineSync } from '../useOfflineSync';
import * as offlineQueueService from '../offlineQueue.service';

// ── Suite ────────────────────────────────────────────────────────────────────
describe('useOfflineSync', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Resetear store a estado inicial
    useOfflineStore.setState({
      isOnline: true,
      pendingCount: 0,
      failedCount: 0,
      isSyncing: false,
      syncStatus: 'idle',
      lastSyncAt: null,
      lastSyncError: null,
    });
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('isOnline refleja navigator.onLine al montar', () => {
    Object.defineProperty(navigator, 'onLine', { value: true, configurable: true });
    const { result } = renderHook(() => useOfflineSync());
    expect(result.current.isOnline).toBe(true);
  });

  it('llama initOfflineCounts al montar', () => {
    renderHook(() => useOfflineSync());
    expect(offlineQueueService.initOfflineCounts).toHaveBeenCalledTimes(1);
  });

  it('setOnline(false) al disparar evento offline', () => {
    const { result } = renderHook(() => useOfflineSync());

    act(() => {
      window.dispatchEvent(new Event('offline'));
    });

    expect(result.current.isOnline).toBe(false);
  });

  it('setOnline(true) al disparar evento online', () => {
    useOfflineStore.setState({ isOnline: false });
    const { result } = renderHook(() => useOfflineSync());

    act(() => {
      window.dispatchEvent(new Event('online'));
    });

    expect(result.current.isOnline).toBe(true);
  });

  it('dispara syncPending al reconectar con ventas pendientes', async () => {
    useOfflineStore.setState({ isOnline: false, pendingCount: 2 });
    renderHook(() => useOfflineSync());

    await act(async () => {
      useOfflineStore.setState({ isOnline: true });
    });

    expect(offlineQueueService.syncPending).toHaveBeenCalledTimes(1);
  });

  it('no sincroniza si pendingCount es 0 al reconectar', async () => {
    useOfflineStore.setState({ isOnline: false, pendingCount: 0 });
    renderHook(() => useOfflineSync());

    await act(async () => {
      useOfflineStore.setState({ isOnline: true });
    });

    expect(offlineQueueService.syncPending).not.toHaveBeenCalled();
  });

  it('no sincroniza si isSyncing es true', async () => {
    useOfflineStore.setState({ isOnline: true, pendingCount: 3, isSyncing: true });
    renderHook(() => useOfflineSync());

    // No debe disparar sync porque ya está sincronizando
    expect(offlineQueueService.syncPending).not.toHaveBeenCalled();
  });

  it('syncNow llama syncPending cuando no está sincronizando', async () => {
    useOfflineStore.setState({ isSyncing: false });
    const { result } = renderHook(() => useOfflineSync());

    await act(async () => {
      result.current.syncNow();
    });

    expect(offlineQueueService.syncPending).toHaveBeenCalledTimes(1);
  });

  it('syncNow no llama syncPending cuando isSyncing es true', async () => {
    useOfflineStore.setState({ isSyncing: true });
    const { result } = renderHook(() => useOfflineSync());

    await act(async () => {
      result.current.syncNow();
    });

    expect(offlineQueueService.syncPending).not.toHaveBeenCalled();
  });

  it('pendingCount refleja el valor del store', () => {
    useOfflineStore.setState({ pendingCount: 5 });
    const { result } = renderHook(() => useOfflineSync());
    expect(result.current.pendingCount).toBe(5);
  });

  it('desregistra listeners al desmontar (no hay leaks)', () => {
    const addSpy = vi.spyOn(window, 'addEventListener');
    const removeSpy = vi.spyOn(window, 'removeEventListener');

    const { unmount } = renderHook(() => useOfflineSync());
    unmount();

    expect(addSpy).toHaveBeenCalledWith('online', expect.any(Function));
    expect(addSpy).toHaveBeenCalledWith('offline', expect.any(Function));
    expect(removeSpy).toHaveBeenCalledWith('online', expect.any(Function));
    expect(removeSpy).toHaveBeenCalledWith('offline', expect.any(Function));
  });
});
