import { describe, it, expect, beforeEach, vi } from 'vitest';
import { screen, waitFor, act } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { POSPage } from '../pages/POSPage';
import { useAuthStore } from '@/stores/auth.store';
import { useCartStore } from '@/stores/cart.store';
import { useOfflineStore } from '@/stores/offline.store';
import type { UserInfo } from '@/types/api';

// ── Estado offline controlable por tests ──────────────────────────────────
const offlineState = {
  isOnline: true, pendingCount: 0, failedCount: 0,
  isSyncing: false, syncStatus: 'idle' as const, lastSyncAt: null as string | null, lastSyncError: null as string | null,
  syncNow: vi.fn(),
};

vi.mock('@/offline/useOfflineSync', () => ({
  useOfflineSync: () => offlineState,
}));
vi.mock('@/offline/offlineQueue.service', () => ({
  enqueueVenta:              vi.fn().mockResolvedValue('offline-test-123'),
  syncPending:               vi.fn().mockResolvedValue({ synced: 0, failed: 0, tokenExpired: false, errors: [] }),
  initOfflineCounts:         vi.fn().mockResolvedValue(undefined),
  discardFailedVentas:       vi.fn().mockResolvedValue(undefined),
  getFailedVentasForDisplay: vi.fn().mockResolvedValue([]),
  retryVenta:                vi.fn().mockResolvedValue(undefined),
  deleteFailedVenta:         vi.fn().mockResolvedValue(undefined),
}));

// Mock @microsoft/signalr para evitar conexiones reales en tests
vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: vi.fn(() => ({
    withUrl: vi.fn().mockReturnThis(),
    withAutomaticReconnect: vi.fn().mockReturnThis(),
    build: vi.fn(() => ({
      on: vi.fn(),
      start: vi.fn().mockResolvedValue(undefined),
      stop: vi.fn().mockResolvedValue(undefined),
      invoke: vi.fn().mockResolvedValue(undefined),
      state: 'Disconnected',
    })),
  })),
  HttpTransportType: { WebSockets: 1 },
  LogLevel: { Warning: 2 },
}));

const makeCajeroUser = (): UserInfo => ({
  id: 'u1',
  username: 'cajero1',
  email: 'cajero@sincopos.com',
  nombre: 'Cajero Test',
  roles: ['cajero'],
  sucursalId: 1,
  sucursalNombre: 'Principal',
  sucursalesDisponibles: [{ id: 1, nombre: 'Principal' }],
});

function setOnlineState(partial: Partial<typeof offlineState>) {
  Object.assign(offlineState, partial);
  // OfflineStatusBanner lee de useOfflineStore (Zustand), no de useOfflineSync
  useOfflineStore.setState({
    isOnline:       offlineState.isOnline,
    pendingCount:   offlineState.pendingCount,
    failedCount:    offlineState.failedCount,
    isSyncing:      offlineState.isSyncing,
    syncStatus:     offlineState.syncStatus,
    lastSyncAt:     offlineState.lastSyncAt,
    lastSyncError:  offlineState.lastSyncError,
  });
}

beforeEach(() => {
  setOnlineState({
    isOnline: true, pendingCount: 0, failedCount: 0,
    isSyncing: false, syncStatus: 'idle', lastSyncAt: null, lastSyncError: null,
  });

  useAuthStore.setState({
    user: null,
    isAuthenticated: false,
    isLoading: false,
    activeSucursalId: undefined,
  });
  useCartStore.setState({ items: [] });
});

describe('POSPage — acceso', () => {
  it('muestra error de permisos si el usuario no es cajero', () => {
    useAuthStore.setState({
      user: { ...makeCajeroUser(), roles: [] }, // Sin roles
      isAuthenticated: true,
      isLoading: false,
      activeSucursalId: 1,
    });

    renderWithProviders(<POSPage />);

    expect(
      screen.getByText(/no tienes permisos para acceder al punto de venta/i)
    ).toBeInTheDocument();
  });
});

describe('POSPage — renderizado', () => {
  it('muestra el encabezado "Punto de Venta"', async () => {
    useAuthStore.setState({
      user: makeCajeroUser(),
      isAuthenticated: true,
      isLoading: false,
      activeSucursalId: undefined, // Sin sucursal activa para evitar diálogo
    });

    renderWithProviders(<POSPage />);

    expect(screen.getByText('Punto de Venta')).toBeInTheDocument();
  });

  it('muestra el panel de productos', async () => {
    useAuthStore.setState({
      user: makeCajeroUser(),
      isAuthenticated: true,
      isLoading: false,
      activeSucursalId: undefined,
    });

    renderWithProviders(<POSPage />);

    expect(screen.getByText('Productos')).toBeInTheDocument();
  });

  it('muestra el campo de búsqueda de productos', async () => {
    useAuthStore.setState({
      user: makeCajeroUser(),
      isAuthenticated: true,
      isLoading: false,
      activeSucursalId: undefined,
    });

    renderWithProviders(<POSPage />);

    expect(
      screen.getByPlaceholderText(/Nombre, código/i)
    ).toBeInTheDocument();
  });

  it('muestra info de caja sin seleccionar en el banner', async () => {
    useAuthStore.setState({
      user: makeCajeroUser(),
      isAuthenticated: true,
      isLoading: false,
      activeSucursalId: undefined,
    });

    renderWithProviders(<POSPage />);

    expect(screen.getByText(/Selecciona una caja para comenzar/i)).toBeInTheDocument();
  });
});

describe('POSPage — carrito', () => {
  it('carrito vacío muestra total $0', async () => {
    useAuthStore.setState({
      user: makeCajeroUser(),
      isAuthenticated: true,
      isLoading: false,
      activeSucursalId: undefined,
    });

    renderWithProviders(<POSPage />);

    await waitFor(() => {
      // El total debe ser $0 cuando el carrito está vacío
      const totalesElements = screen.getAllByText(/\$\s*0/i);
      expect(totalesElements.length).toBeGreaterThan(0);
    });
  });
});

describe('POSPage — offline', () => {
  it('muestra el banner de modo offline cuando isOnline=false', () => {
    setOnlineState({ isOnline: false, pendingCount: 0 });

    useAuthStore.setState({
      user: makeCajeroUser(),
      isAuthenticated: true,
      isLoading: false,
      activeSucursalId: undefined,
    });

    renderWithProviders(<POSPage />);

    expect(screen.getByText(/Modo offline/i)).toBeInTheDocument();
  });

  it('muestra conteo de ventas pendientes en el banner offline', () => {
    setOnlineState({ isOnline: false, pendingCount: 3 });

    useAuthStore.setState({
      user: makeCajeroUser(),
      isAuthenticated: true,
      isLoading: false,
      activeSucursalId: undefined,
    });

    renderWithProviders(<POSPage />);

    expect(screen.getByText(/3 pendientes/i)).toBeInTheDocument();
  });

  it('muestra el banner de sincronización cuando isSyncing=true', () => {
    setOnlineState({ isOnline: true, pendingCount: 2, isSyncing: true });

    useAuthStore.setState({
      user: makeCajeroUser(),
      isAuthenticated: true,
      isLoading: false,
      activeSucursalId: undefined,
    });

    renderWithProviders(<POSPage />);

    expect(screen.getByText(/Sincronizando/i)).toBeInTheDocument();
  });

  it('muestra el banner de error cuando hay ventas fallidas', () => {
    setOnlineState({
      isOnline:      true,
      failedCount:   2,
      syncStatus:    'error' as any,
      lastSyncError: 'Stock insuficiente',
    });

    useAuthStore.setState({
      user: makeCajeroUser(),
      isAuthenticated: true,
      isLoading: false,
      activeSucursalId: undefined,
    });

    renderWithProviders(<POSPage />);

    // lastSyncError se muestra como texto directo en el banner
    expect(screen.getByText('Stock insuficiente')).toBeInTheDocument();
  });

  it('no muestra el banner cuando está online sin pendientes ni errores', () => {
    setOnlineState({
      isOnline: true, pendingCount: 0, failedCount: 0, syncStatus: 'idle' as any,
    });

    useAuthStore.setState({
      user: makeCajeroUser(),
      isAuthenticated: true,
      isLoading: false,
      activeSucursalId: undefined,
    });

    renderWithProviders(<POSPage />);

    expect(screen.queryByText(/Modo offline/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/pendiente/i)).not.toBeInTheDocument();
  });

  it('muestra botón "Ver detalles" cuando hay ventas fallidas', () => {
    setOnlineState({
      isOnline:    true,
      failedCount: 1,
      syncStatus:  'error' as any,
    });

    useAuthStore.setState({
      user: makeCajeroUser(),
      isAuthenticated: true,
      isLoading: false,
      activeSucursalId: undefined,
    });

    renderWithProviders(<POSPage />);

    expect(screen.getByText(/ver detalles/i)).toBeInTheDocument();
  });
});
