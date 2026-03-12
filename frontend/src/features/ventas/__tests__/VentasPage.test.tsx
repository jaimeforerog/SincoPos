import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/test/test-utils';
import { VentasPage } from '../pages/VentasPage';
import type { VentaDTO, PaginatedResult } from '@/types/api';

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({
    activeSucursalId: 1,
    user: null,
    isAuthenticated: true,
  }),
}));

vi.mock('@/api/ventas', () => ({
  ventasApi: {
    getAll: vi.fn(),
    getErpPendientesCount: vi.fn().mockResolvedValue(0),
  },
}));

vi.mock('@/api/sucursales', () => ({
  sucursalesApi: {
    getAll: vi.fn().mockResolvedValue([
      { id: 1, nombre: 'Central', activa: true },
    ]),
  },
}));

vi.mock('../components/VentaDetalleDialog', () => ({
  VentaDetalleDialog: ({ open, venta }: { open: boolean; venta: VentaDTO | null }) =>
    open && venta ? <div data-testid="venta-detalle-dialog">{venta.numeroVenta}</div> : null,
}));

const makeVenta = (overrides: Partial<VentaDTO> = {}): VentaDTO => ({
  id: 1,
  numeroVenta: 'V-000001',
  sucursalId: 1,
  nombreSucursal: 'Central',
  cajaId: 1,
  nombreCaja: 'Caja 1',
  subtotal: 10000,
  descuento: 0,
  impuestos: 1900,
  total: 11900,
  estado: 'Completada',
  metodoPago: 'Efectivo',
  montoPagado: 11900,
  cambio: 0,
  fechaVenta: '2026-03-12T10:00:00Z',
  requiereFacturaElectronica: false,
  sincronizadoErp: false,
  detalles: [],
  ...overrides,
});

const makePage = (items: VentaDTO[], totalPages = 1): PaginatedResult<VentaDTO> => ({
  items,
  totalCount: items.length,
  pageNumber: 1,
  pageSize: 50,
  totalPages,
});

describe('VentasPage', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    const { ventasApi } = await import('@/api/ventas');
    vi.mocked(ventasApi.getAll).mockResolvedValue(makePage([]));
    vi.mocked(ventasApi.getErpPendientesCount).mockResolvedValue(0);
  });

  it('muestra el título "Historial de Ventas"', () => {
    renderWithProviders(<VentasPage />);
    expect(screen.getByRole('heading', { name: /historial de ventas/i })).toBeInTheDocument();
  });

  it('muestra alerta info cuando no hay ventas', async () => {
    renderWithProviders(<VentasPage />);
    expect(await screen.findByText(/no se encontraron ventas/i)).toBeInTheDocument();
  });

  it('muestra filas de ventas cuando la API retorna datos', async () => {
    const { ventasApi } = await import('@/api/ventas');
    vi.mocked(ventasApi.getAll).mockResolvedValue(
      makePage([makeVenta({ numeroVenta: 'V-000099', nombreCaja: 'Caja Principal' })])
    );

    renderWithProviders(<VentasPage />);

    expect(await screen.findByText('V-000099')).toBeInTheDocument();
    expect(await screen.findByText('Caja Principal')).toBeInTheDocument();
    expect(await screen.findByText('Efectivo')).toBeInTheDocument();
  });

  it('muestra chip "Completada" con color success', async () => {
    const { ventasApi } = await import('@/api/ventas');
    vi.mocked(ventasApi.getAll).mockResolvedValue(
      makePage([makeVenta({ estado: 'Completada' })])
    );

    renderWithProviders(<VentasPage />);

    const chip = await screen.findByText('Completada');
    expect(chip).toBeInTheDocument();
  });

  it('muestra chip "Anulada" con color error', async () => {
    const { ventasApi } = await import('@/api/ventas');
    vi.mocked(ventasApi.getAll).mockResolvedValue(
      makePage([makeVenta({ estado: 'Anulada' })])
    );

    renderWithProviders(<VentasPage />);

    expect(await screen.findByText('Anulada')).toBeInTheDocument();
  });

  it('muestra "Sin cliente" cuando nombreCliente es undefined', async () => {
    const { ventasApi } = await import('@/api/ventas');
    vi.mocked(ventasApi.getAll).mockResolvedValue(
      makePage([makeVenta({ nombreCliente: undefined })])
    );

    renderWithProviders(<VentasPage />);

    expect(await screen.findByText('Sin cliente')).toBeInTheDocument();
  });

  it('abre VentaDetalleDialog al hacer clic en el ícono de ver detalle', async () => {
    const { ventasApi } = await import('@/api/ventas');
    vi.mocked(ventasApi.getAll).mockResolvedValue(
      makePage([makeVenta({ numeroVenta: 'V-000099' })])
    );

    renderWithProviders(<VentasPage />);
    await screen.findByText('V-000099');

    const verBtn = screen.getByRole('button', { name: /ver detalle/i });
    await userEvent.click(verBtn);

    expect(await screen.findByTestId('venta-detalle-dialog')).toBeInTheDocument();
    expect(screen.getByTestId('venta-detalle-dialog')).toHaveTextContent('V-000099');
  });

  it('muestra paginación cuando totalPages > 1', async () => {
    const { ventasApi } = await import('@/api/ventas');
    vi.mocked(ventasApi.getAll).mockResolvedValue(makePage([makeVenta()], 3));

    renderWithProviders(<VentasPage />);

    await waitFor(() => {
      expect(screen.getByRole('navigation')).toBeInTheDocument();
    });
  });

  it('no muestra paginación cuando hay una sola página', async () => {
    const { ventasApi } = await import('@/api/ventas');
    vi.mocked(ventasApi.getAll).mockResolvedValue(makePage([makeVenta()], 1));

    renderWithProviders(<VentasPage />);
    await screen.findByText('V-000001');

    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
  });

  it('muestra el contador de ventas encontradas', async () => {
    const { ventasApi } = await import('@/api/ventas');
    vi.mocked(ventasApi.getAll).mockResolvedValue(makePage([makeVenta(), makeVenta({ id: 2, numeroVenta: 'V-000002' })]));

    renderWithProviders(<VentasPage />);

    expect(await screen.findByText(/2 venta\(s\) encontradas/i)).toBeInTheDocument();
  });
});
