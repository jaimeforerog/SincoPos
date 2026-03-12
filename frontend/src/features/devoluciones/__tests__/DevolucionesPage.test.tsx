import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/test/test-utils';
import { DevolucionesPage } from '../pages/DevolucionesPage';
import type { VentaDTO, PaginatedResult } from '@/types/api';

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({ activeSucursalId: 1, user: null, isAuthenticated: true }),
}));

vi.mock('@/api/ventas', () => ({
  ventasApi: {
    getAll: vi.fn(),
    getById: vi.fn(),
  },
}));

vi.mock('@/api/devoluciones', () => ({
  devolucionesApi: {
    obtenerPorVenta: vi.fn().mockResolvedValue([]),
    crearDevolucionParcial: vi.fn(),
  },
}));

vi.mock('@/components/common/PageHeader', () => ({
  PageHeader: ({ title }: { title: string }) => <h1>{title}</h1>,
}));

vi.mock('@/utils/format', () => ({
  formatCurrency: (v: number) => `$${v}`,
  formatDate: (s: string) => s,
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
  fechaVenta: new Date().toISOString(), // hoy — dentro del límite 30 días
  requiereFacturaElectronica: false,
  sincronizadoErp: false,
  detalles: [
    {
      id: 1,
      productoId: 'prod-1',
      nombreProducto: 'Café Premium',
      cantidad: 3,
      precioUnitario: 5000,
      costoUnitario: 2500,
      descuento: 0,
      porcentajeImpuesto: 0,
      montoImpuesto: 0,
      subtotal: 15000,
      margenGanancia: 50,
    },
  ],
  ...overrides,
});

const makePage = (items: VentaDTO[]): PaginatedResult<VentaDTO> => ({
  items,
  totalCount: items.length,
  pageNumber: 1,
  pageSize: 100,
  totalPages: 1,
});

describe('DevolucionesPage', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    const { ventasApi } = await import('@/api/ventas');
    vi.mocked(ventasApi.getAll).mockResolvedValue(makePage([]));
    vi.mocked(ventasApi.getById).mockResolvedValue(makeVenta());
    const { devolucionesApi } = await import('@/api/devoluciones');
    vi.mocked(devolucionesApi.obtenerPorVenta).mockResolvedValue([]);
  });

  it('muestra el título "Devoluciones de Ventas"', () => {
    renderWithProviders(<DevolucionesPage />);
    expect(screen.getByRole('heading', { name: /devoluciones de ventas/i })).toBeInTheDocument();
  });

  it('muestra el texto "Buscar Venta para Devolución"', () => {
    renderWithProviders(<DevolucionesPage />);
    expect(screen.getByText(/buscar venta para devolución/i)).toBeInTheDocument();
  });

  it('muestra conteo de ventas encontradas', async () => {
    const { ventasApi } = await import('@/api/ventas');
    vi.mocked(ventasApi.getAll).mockResolvedValue(makePage([makeVenta(), makeVenta({ id: 2, numeroVenta: 'V-000002' })]));

    renderWithProviders(<DevolucionesPage />);

    expect(await screen.findByText(/2 venta\(s\) completada\(s\) encontradas/i)).toBeInTheDocument();
  });

  it('muestra "0 venta(s)" cuando la API retorna lista vacía', async () => {
    renderWithProviders(<DevolucionesPage />);
    expect(await screen.findByText(/0 venta\(s\) completada\(s\) encontradas/i)).toBeInTheDocument();
  });

  it('llama a ventasApi con sucursalId del usuario y estado Completada', async () => {
    const { ventasApi } = await import('@/api/ventas');

    renderWithProviders(<DevolucionesPage />);

    await waitFor(() => {
      expect(ventasApi.getAll).toHaveBeenCalledWith(
        expect.objectContaining({
          sucursalId: 1,
          estado: 'Completada',
        })
      );
    });
  });

  it('no muestra la sección de venta cuando no hay venta seleccionada', () => {
    renderWithProviders(<DevolucionesPage />);
    expect(screen.queryByText(/productos de la venta/i)).not.toBeInTheDocument();
  });

  it('muestra detalles de la venta cuando se selecciona una via getById', async () => {
    const { ventasApi } = await import('@/api/ventas');
    const venta = makeVenta({ numeroVenta: 'V-000099' });
    vi.mocked(ventasApi.getAll).mockResolvedValue(makePage([venta]));
    vi.mocked(ventasApi.getById).mockResolvedValue(venta);

    renderWithProviders(<DevolucionesPage />);
    await screen.findByText(/0 venta\(s\)|1 venta\(s\)/i);

    // Abrir el Autocomplete y seleccionar la opción
    const input = screen.getByRole('combobox');
    await userEvent.click(input);
    const opcion = await screen.findByText('V-000099');
    await userEvent.click(opcion);

    expect(await screen.findByText(/productos de la venta/i)).toBeInTheDocument();
    expect(await screen.findByText('Café Premium')).toBeInTheDocument();
  });

  it('muestra el botón "Procesar Devolución" deshabilitado cuando cantidad devolver es 0', async () => {
    const { ventasApi } = await import('@/api/ventas');
    const venta = makeVenta();
    vi.mocked(ventasApi.getAll).mockResolvedValue(makePage([venta]));
    vi.mocked(ventasApi.getById).mockResolvedValue(venta);

    renderWithProviders(<DevolucionesPage />);

    const input = screen.getByRole('combobox');
    await userEvent.click(input);
    const opcion = await screen.findByText('V-000001');
    await userEvent.click(opcion);

    await screen.findByText(/productos de la venta/i);
    const btn = await screen.findByRole('button', { name: /procesar devolución/i });
    expect(btn).toBeDisabled();
  });
});
