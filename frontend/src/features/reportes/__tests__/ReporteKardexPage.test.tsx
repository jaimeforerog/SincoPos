import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { ReporteKardexPage } from '../pages/ReporteKardexPage';

vi.mock('@/api/reportes', () => ({
  reportesApi: {
    kardex: vi.fn(),
  },
}));

vi.mock('@/api/sucursales', () => ({
  sucursalesApi: {
    getAll: vi.fn().mockResolvedValue([
      { id: 1, nombre: 'Principal', activo: true },
    ]),
  },
}));

vi.mock('@/api/productos', () => ({
  productosApi: {
    getAll: vi.fn().mockResolvedValue({
      items: [
        {
          id: 'p1',
          codigoBarras: '7701234',
          nombre: 'Coca-Cola 350ml',
          precioVenta: 3_500,
          precioCosto: 2_000,
          activo: true,
          categoriaId: 1,
        },
      ],
      totalCount: 1,
    }),
  },
}));

vi.mock('@/utils/exportReportes', () => ({
  exportarReporteKardex: vi.fn(),
}));

// Stub de DataGrid de MUI X (requiere licencia en tests)
vi.mock('@mui/x-data-grid', () => ({
  DataGrid: ({ rows }: { rows: unknown[] }) => (
    <div data-testid="kardex-grid">{rows.length} movimientos</div>
  ),
}));

const makeReporteKardex = () => ({
  productoId: 'p1',
  codigoBarras: '7701234',
  nombre: 'Coca-Cola 350ml',
  sucursalId: 1,
  nombreSucursal: 'Principal',
  fechaDesde: '2026-03-01T00:00:00Z',
  fechaHasta: '2026-03-31T00:00:00Z',
  saldoInicial: 10,
  saldoFinal: 25,
  costoPromedioVigente: 2_000,
  movimientos: [
    {
      fecha: '2026-03-05T10:00:00Z',
      tipoMovimiento: 'EntradaCompra',
      referencia: 'OC-000001',
      observaciones: null,
      entrada: 20,
      salida: 0,
      saldoAcumulado: 30,
      costoUnitario: 2_000,
      costoTotalMovimiento: 40_000,
    },
    {
      fecha: '2026-03-10T15:00:00Z',
      tipoMovimiento: 'SalidaVenta',
      referencia: 'V-0001',
      observaciones: null,
      entrada: 0,
      salida: 5,
      saldoAcumulado: 25,
      costoUnitario: 2_000,
      costoTotalMovimiento: 10_000,
    },
  ],
});

describe('ReporteKardexPage', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    const { reportesApi } = await import('@/api/reportes');
    vi.mocked(reportesApi.kardex).mockResolvedValue(makeReporteKardex());
  });

  it('muestra el encabezado "Kardex de Inventario"', async () => {
    renderWithProviders(<ReporteKardexPage />);
    expect(await screen.findByText('Kardex de Inventario')).toBeInTheDocument();
  });

  it('muestra el selector de Sucursal', async () => {
    renderWithProviders(<ReporteKardexPage />);
    await screen.findByText('Kardex de Inventario');
    // Hay al menos un combobox (Sucursal select + Autocomplete de producto)
    expect(screen.getAllByRole('combobox').length).toBeGreaterThanOrEqual(1);
  });

  it('muestra el Autocomplete para seleccionar producto', async () => {
    renderWithProviders(<ReporteKardexPage />);
    expect(await screen.findByPlaceholderText(/producto/i)).toBeInTheDocument();
  });

  it('muestra los campos de fecha', async () => {
    renderWithProviders(<ReporteKardexPage />);
    expect(await screen.findByLabelText(/fecha desde/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/fecha hasta/i)).toBeInTheDocument();
  });

  it('muestra el botón "Consultar"', async () => {
    renderWithProviders(<ReporteKardexPage />);
    expect(await screen.findByRole('button', { name: /consultar/i })).toBeInTheDocument();
  });

  it('muestra el botón "Excel" para exportar', async () => {
    renderWithProviders(<ReporteKardexPage />);
    // El botón dice "Excel" (con icono Download)
    expect(await screen.findByRole('button', { name: /excel/i })).toBeInTheDocument();
  });

  it('no muestra el grid inicialmente (sin parámetros de búsqueda)', async () => {
    renderWithProviders(<ReporteKardexPage />);
    await screen.findByText('Kardex de Inventario');
    expect(screen.queryByTestId('kardex-grid')).not.toBeInTheDocument();
  });
});
