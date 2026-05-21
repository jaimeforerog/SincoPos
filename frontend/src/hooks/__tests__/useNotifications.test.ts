import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import { useNotifications } from '../useNotifications';
import { useAuthStore } from '@/stores/auth.store';

// ---------- Mocks ----------
const enqueueSnackbarMock = vi.fn();

vi.mock('notistack', () => ({
  useSnackbar: () => ({ enqueueSnackbar: enqueueSnackbarMock }),
}));

// Mock de @microsoft/signalr — exponemos un builder controlable y el HubConnection mockeado.
type HandlerMap = Record<string, (...args: unknown[]) => void>;

interface MockHub {
  start: ReturnType<typeof vi.fn>;
  stop: ReturnType<typeof vi.fn>;
  invoke: ReturnType<typeof vi.fn>;
  on: ReturnType<typeof vi.fn>;
  onreconnecting: ReturnType<typeof vi.fn>;
  onreconnected: ReturnType<typeof vi.fn>;
  onclose: ReturnType<typeof vi.fn>;
  state: string;
  __handlers: HandlerMap;
}

let mockHub: MockHub;

function createMockHub(): MockHub {
  const handlers: HandlerMap = {};
  return {
    start: vi.fn().mockResolvedValue(undefined),
    stop: vi.fn().mockResolvedValue(undefined),
    invoke: vi.fn().mockResolvedValue(undefined),
    on: vi.fn((event: string, handler: (...args: unknown[]) => void) => {
      handlers[event] = handler;
    }),
    onreconnecting: vi.fn(),
    onreconnected: vi.fn(),
    onclose: vi.fn(),
    state: 'Connected',
    __handlers: handlers,
  };
}

vi.mock('@microsoft/signalr', () => {
  class HubConnectionBuilder {
    withUrl = vi.fn().mockReturnThis();
    withAutomaticReconnect = vi.fn().mockReturnThis();
    configureLogging = vi.fn().mockReturnThis();
    build = vi.fn(() => mockHub);
  }

  return {
    HubConnectionBuilder,
    HttpTransportType: {
      None: 0,
      WebSockets: 1,
      ServerSentEvents: 2,
      LongPolling: 4,
    },
    HubConnectionState: {
      Connected: 'Connected',
      Disconnected: 'Disconnected',
      Connecting: 'Connecting',
      Reconnecting: 'Reconnecting',
    },
    LogLevel: { None: 6 },
  };
});

// ---------- Helpers ----------
function setAuth(state: { isAuthenticated?: boolean; activeSucursalId?: number }) {
  useAuthStore.setState({
    isAuthenticated: state.isAuthenticated ?? true,
    activeSucursalId: state.activeSucursalId,
  });
}

beforeEach(() => {
  mockHub = createMockHub();
  enqueueSnackbarMock.mockClear();
  sessionStorage.setItem('access_token', 'test-token');
  useAuthStore.setState({
    user: null,
    isAuthenticated: false,
    isLoading: false,
    activeSucursalId: undefined,
  });
});

afterEach(() => {
  sessionStorage.clear();
});

describe('useNotifications', () => {
  it('inicia la conexión SignalR cuando el usuario está autenticado', async () => {
    setAuth({ isAuthenticated: true, activeSucursalId: 1 });

    renderHook(() => useNotifications());

    await waitFor(() => {
      expect(mockHub.start).toHaveBeenCalledTimes(1);
    });
  });

  it('llama a stop() al desmontar el hook', async () => {
    setAuth({ isAuthenticated: true, activeSucursalId: 1 });

    const { unmount } = renderHook(() => useNotifications());

    await waitFor(() => expect(mockHub.start).toHaveBeenCalled());

    unmount();
    expect(mockHub.stop).toHaveBeenCalledTimes(1);
  });

  it('llama JoinSucursal al iniciar con activeSucursalId definido', async () => {
    setAuth({ isAuthenticated: true, activeSucursalId: 5 });

    renderHook(() => useNotifications());

    await waitFor(() => {
      expect(mockHub.invoke).toHaveBeenCalledWith('JoinSucursal', 5);
    });
  });

  it('al cambiar activeSucursalId, llama LeaveSucursal(prev) y JoinSucursal(new)', async () => {
    setAuth({ isAuthenticated: true, activeSucursalId: 1 });

    renderHook(() => useNotifications());

    await waitFor(() => {
      expect(mockHub.invoke).toHaveBeenCalledWith('JoinSucursal', 1);
    });

    // Cambio de sucursal
    act(() => {
      useAuthStore.setState({ activeSucursalId: 2 });
    });

    await waitFor(() => {
      expect(mockHub.invoke).toHaveBeenCalledWith('LeaveSucursal', 1);
      expect(mockHub.invoke).toHaveBeenCalledWith('JoinSucursal', 2);
    });
  });

  it('cuando llega una Notificación, muestra snackbar y actualiza notifications/unreadCount', async () => {
    setAuth({ isAuthenticated: true, activeSucursalId: 1 });

    const { result } = renderHook(() => useNotifications());

    await waitFor(() => {
      expect(mockHub.on).toHaveBeenCalledWith('Notificacion', expect.any(Function));
    });

    const handler = mockHub.__handlers['Notificacion'];
    expect(handler).toBeTypeOf('function');

    const notif = {
      tipo: 'venta',
      titulo: 'Nueva venta',
      mensaje: 'Venta #123 registrada',
      nivel: 'info' as const,
      timestamp: new Date().toISOString(),
    };

    act(() => {
      handler(notif);
    });

    expect(enqueueSnackbarMock).toHaveBeenCalledWith(
      'Venta #123 registrada',
      expect.objectContaining({ variant: 'info' }),
    );
    expect(result.current.notifications).toHaveLength(1);
    expect(result.current.unreadCount).toBe(1);
  });

  it('no se conecta si el usuario no está autenticado', () => {
    setAuth({ isAuthenticated: false });

    renderHook(() => useNotifications());

    expect(mockHub.start).not.toHaveBeenCalled();
  });
});
