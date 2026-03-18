import { create } from 'zustand';

export type SyncStatus = 'idle' | 'syncing' | 'success' | 'error';

interface OfflineState {
  isOnline: boolean;
  pendingCount: number;
  failedCount: number;
  isSyncing: boolean;
  syncStatus: SyncStatus;
  lastSyncAt: string | null;
  lastSyncError: string | null;

  setOnline: (online: boolean) => void;
  setPendingCount: (n: number) => void;
  setFailedCount: (n: number) => void;
  setSyncing: (syncing: boolean) => void;
  setSyncSuccess: (at: string) => void;
  setSyncError: (error: string) => void;
  resetSyncStatus: () => void;
}

export const useOfflineStore = create<OfflineState>((set) => ({
  isOnline: typeof navigator !== 'undefined' ? navigator.onLine : true,
  pendingCount: 0,
  failedCount: 0,
  isSyncing: false,
  syncStatus: 'idle',
  lastSyncAt: null,
  lastSyncError: null,

  setOnline: (online) => set({ isOnline: online }),
  setPendingCount: (n) => set({ pendingCount: n }),
  setFailedCount: (n) => set({ failedCount: n }),
  setSyncing: (syncing) => set({ isSyncing: syncing, syncStatus: syncing ? 'syncing' : 'idle' }),
  setSyncSuccess: (at) =>
    set({ isSyncing: false, syncStatus: 'success', lastSyncAt: at, lastSyncError: null }),
  setSyncError: (error) =>
    set({ isSyncing: false, syncStatus: 'error', lastSyncError: error }),
  resetSyncStatus: () => set({ syncStatus: 'idle', lastSyncError: null }),
}));
