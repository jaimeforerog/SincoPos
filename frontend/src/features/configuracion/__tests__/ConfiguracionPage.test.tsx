import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { ConfiguracionPage } from '../pages/ConfiguracionPage';

vi.mock('@/hooks/useAuth', () => ({
  useAuth: vi.fn(() => ({
    user: null,
    isAuthenticated: true,
    isLoading: false,
    activeSucursalId: undefined,
    activeEmpresaId: undefined,
    hasRole: () => true,
    hasAnyRole: () => true,
    isAdmin: () => true,
    isSupervisor: () => true,
    isCajero: () => true,
  })),
}));

describe('ConfiguracionPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('muestra el título "Configuración del Sistema"', async () => {
    renderWithProviders(<ConfiguracionPage />);
    expect(await screen.findByText('Configuración del Sistema')).toBeInTheDocument();
  });

  it('muestra el chip con el número de módulos', async () => {
    renderWithProviders(<ConfiguracionPage />);
    // El chip y los contadores de categoría contienen "módulos" → varios elementos
    expect((await screen.findAllByText(/módulos/i)).length).toBeGreaterThanOrEqual(1);
  });

  it('muestra la sección "Maestros de Negocio"', async () => {
    renderWithProviders(<ConfiguracionPage />);
    expect(await screen.findByText('Maestros de Negocio')).toBeInTheDocument();
  });

  it('muestra la sección "Catálogo de Productos"', async () => {
    renderWithProviders(<ConfiguracionPage />);
    expect(await screen.findByText('Catálogo de Productos')).toBeInTheDocument();
  });

  it('muestra la sección "Configuración Fiscal y Sistema"', async () => {
    renderWithProviders(<ConfiguracionPage />);
    expect(await screen.findByText('Configuración Fiscal y Sistema')).toBeInTheDocument();
  });

  it('muestra la tarjeta de Sucursales', async () => {
    renderWithProviders(<ConfiguracionPage />);
    expect(await screen.findByText('Sucursales')).toBeInTheDocument();
  });

  it('muestra la tarjeta de Usuarios', async () => {
    renderWithProviders(<ConfiguracionPage />);
    expect(await screen.findByText('Usuarios')).toBeInTheDocument();
  });

  it('muestra la tarjeta de Precios Sucursal', async () => {
    renderWithProviders(<ConfiguracionPage />);
    expect(await screen.findByText('Precios Sucursal')).toBeInTheDocument();
  });

  it('supervisor no ve módulos de admin (sin Sucursales)', async () => {
    const { useAuth } = await import('@/hooks/useAuth');
    vi.mocked(useAuth).mockImplementation(() => ({
      user: null,
      isAuthenticated: true,
      isLoading: false,
      activeSucursalId: undefined,
      activeEmpresaId: undefined,
      hasRole: (role: string) => role === 'supervisor',
      hasAnyRole: (roles: string[]) => roles.includes('supervisor'),
      isAdmin: () => false,
      isSupervisor: () => true,
      isCajero: () => true,
    }));

    renderWithProviders(<ConfiguracionPage />);
    // Supervisor SÍ ve Cajas y Productos
    expect(await screen.findByText('Cajas')).toBeInTheDocument();
    // Supervisor NO ve módulos de solo admin
    expect(screen.queryByText('Sucursales')).not.toBeInTheDocument();
    expect(screen.queryByText('Impuestos')).not.toBeInTheDocument();
  });
});
