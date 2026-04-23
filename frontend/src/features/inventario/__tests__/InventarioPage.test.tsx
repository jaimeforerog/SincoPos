import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/test/test-utils';
import { InventarioPage } from '../pages/InventarioPage';
import type { InventarioStockDTO, AlertaStockDTO } from '@/types/api';

const mockUseAuth = {
  isSupervisor: vi.fn(() => false),
  isAdmin: vi.fn(() => false),
  isCajero: vi.fn(() => true),
  activeSucursalId: 1 as number | undefined,
  user: null,
  isAuthenticated: true,
};

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => mockUseAuth,
}));

vi.mock('@/api/inventario', () => ({
  inventarioApi: {
    getStock: vi.fn().mockResolvedValue([]),
    getAlertas: vi.fn().mockResolvedValue([]),
    getMovimientos: vi.fn().mockResolvedValue([]),
  },
}));

vi.mock('@/api/sucursales', () => ({
  sucursalesApi: {
    getAll: vi.fn().mockResolvedValue([{ id: 1, nombre: 'Central', activa: true }]),
  },
}));

vi.mock('../components/EntradaInventarioDialog', () => ({
  EntradaInventarioDialog: () => null,
}));
vi.mock('../components/AjusteInventarioDialog', () => ({
  AjusteInventarioDialog: () => null,
}));
vi.mock('../components/DevolucionProveedorDialog', () => ({
  DevolucionProveedorDialog: () => null,
}));
vi.mock('../components/LotesTab', () => ({
  LotesTab: () => <div data-testid="lotes-tab" />,
}));

const stockItem: InventarioStockDTO = {
  id: 1,
  productoId: 'prod-1',
  nombreProducto: 'Café Premium',
  codigoBarras: '7701234',
  sucursalId: 1,
  nombreSucursal: 'Central',
  cantidad: 50,
  stockMinimo: 10,
  costoPromedio: 8000,
  ultimaActualizacion: '2026-03-11T10:00:00Z',
};

const alertaItem: AlertaStockDTO = {
  productoId: 'prod-2',
  nombreProducto: 'Azúcar',
  codigoBarras: '7702345',
  sucursalId: 1,
  nombreSucursal: 'Central',
  cantidadActual: 2,
  stockMinimo: 20,
};

describe('InventarioPage', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    mockUseAuth.isSupervisor.mockReturnValue(false);
    // Reset API mocks to empty defaults
    const { inventarioApi } = await import('@/api/inventario');
    vi.mocked(inventarioApi.getStock).mockResolvedValue([]);
    vi.mocked(inventarioApi.getAlertas).mockResolvedValue([]);
    vi.mocked(inventarioApi.getMovimientos).mockResolvedValue({ items: [], totalCount: 0, pageNumber: 1, pageSize: 50, totalPages: 0 });
  });

  it('muestra el título Gestión de Inventario', () => {
    renderWithProviders(<InventarioPage />);
    expect(screen.getByRole('heading', { name: 'Gestión de Inventario' })).toBeInTheDocument();
  });

  it('muestra 4 tabs con sus etiquetas', () => {
    renderWithProviders(<InventarioPage />);
    expect(screen.getByRole('tab', { name: /stock actual/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /alertas/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /movimientos/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /lotes/i })).toBeInTheDocument();
  });

  it('muestra mensaje vacío cuando no hay stock', async () => {
    renderWithProviders(<InventarioPage />);
    expect(await screen.findByText('No hay productos en inventario')).toBeInTheDocument();
  });

  it('muestra filas de stock cuando la API retorna datos', async () => {
    const { inventarioApi } = await import('@/api/inventario');
    vi.mocked(inventarioApi.getStock).mockResolvedValue([stockItem]);

    renderWithProviders(<InventarioPage />);
    expect(await screen.findByText('Café Premium')).toBeInTheDocument();
    expect(await screen.findByText('7701234')).toBeInTheDocument();
  });

  it('muestra alerta de stock bajo en la tab Alertas', async () => {
    const { inventarioApi } = await import('@/api/inventario');
    vi.mocked(inventarioApi.getAlertas).mockResolvedValue([alertaItem]);

    renderWithProviders(<InventarioPage />);

    // Esperar que cargue y clicar la tab de alertas
    await waitFor(() => expect(screen.getByRole('tab', { name: /alertas \(1\)/i })).toBeInTheDocument());
    await userEvent.click(screen.getByRole('tab', { name: /alertas \(1\)/i }));

    expect(await screen.findByText('Azúcar')).toBeInTheDocument();
  });

  it('oculta botones de acciones para usuario no supervisor', () => {
    renderWithProviders(<InventarioPage />);
    expect(screen.queryByRole('button', { name: /entrada/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /ajuste/i })).not.toBeInTheDocument();
  });

  it('muestra botones de acciones para supervisor', async () => {
    mockUseAuth.isSupervisor.mockReturnValue(true);

    renderWithProviders(<InventarioPage />);
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /entrada/i })).toBeInTheDocument();
    });
  });
});
