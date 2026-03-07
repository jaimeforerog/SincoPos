import { describe, it, expect, beforeEach, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { POSPage } from '../pages/POSPage';
import { useAuthStore } from '@/stores/auth.store';
import { useCartStore } from '@/stores/cart.store';
import type { UserInfo } from '@/types/api';

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

beforeEach(() => {
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
      screen.getByPlaceholderText(/buscar producto/i)
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

    expect(screen.getByText(/sin caja seleccionada/i)).toBeInTheDocument();
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
