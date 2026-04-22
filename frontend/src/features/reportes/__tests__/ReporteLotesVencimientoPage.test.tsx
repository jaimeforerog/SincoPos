import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { ReporteLotesVencimientoPage } from '../pages/ReporteLotesVencimientoPage';

vi.mock('@/api/lotes', () => ({
  lotesApi: {
    reporte: vi.fn(),
  },
}));

vi.mock('@/api/sucursales', () => ({
  sucursalesApi: {
    getAll: vi.fn().mockResolvedValue([
      { id: 1, nombre: 'Principal', activo: true },
    ]),
  },
}));

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({ activeSucursalId: 1 }),
}));

vi.mock('@/utils/exportReportes', () => ({
  exportarReporteLotes: vi.fn(),
}));

const makeLotesResponse = (overrides = {}) => ({
  totalLotes: 8,
  totalUnidades: 250,
  valorTotalInventario: 1_200_000,
  lotesVencidos: 1,
  lotesCriticos: 2,
  lotesProximos: 1,
  lotesVigentes: 3,
  lotesSinFecha: 1,
  items: [
    {
      id: 1,
      productoId: 'prod-1',
      nombreProducto: 'Leche entera 1L',
      codigoBarras: '770001',
      sucursalId: 1,
      nombreSucursal: 'Principal',
      numeroLote: 'L-2026-01',
      fechaVencimiento: '2026-05-01',
      fechaEntrada: '2026-01-01T00:00:00Z',
      estadoVencimiento: 'Proximo' as const,
      diasParaVencer: 10,
      cantidadDisponible: 50,
      costoUnitario: 2_500,
      valorTotal: 125_000,
      referencia: 'OC-000001',
    },
    {
      id: 2,
      productoId: 'prod-2',
      nombreProducto: 'Yogurt fresa 200g',
      codigoBarras: '770002',
      sucursalId: 1,
      nombreSucursal: 'Principal',
      numeroLote: 'L-2026-02',
      fechaVencimiento: '2026-04-18',
      fechaEntrada: '2026-01-01T00:00:00Z',
      estadoVencimiento: 'Vencido' as const,
      diasParaVencer: -3,
      cantidadDisponible: 12,
      costoUnitario: 1_800,
      valorTotal: 21_600,
    },
  ],
  ...overrides,
});

describe('ReporteLotesVencimientoPage', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    const { lotesApi } = await import('@/api/lotes');
    vi.mocked(lotesApi.reporte).mockResolvedValue(makeLotesResponse());
  });

  it('muestra el encabezado "Inventario por Lote y Vencimiento"', async () => {
    renderWithProviders(<ReporteLotesVencimientoPage />);
    expect(await screen.findByText('Inventario por Lote y Vencimiento')).toBeInTheDocument();
  });

  it('muestra el filtro de sucursal', async () => {
    renderWithProviders(<ReporteLotesVencimientoPage />);
    await screen.findByRole('heading', { name: /inventario por lote/i });
    // Los selects de MUI se renderizan como combobox
    expect(screen.getAllByRole('combobox').length).toBeGreaterThanOrEqual(1);
  });

  it('muestra el filtro de estado de vencimiento', async () => {
    renderWithProviders(<ReporteLotesVencimientoPage />);
    await screen.findByRole('heading', { name: /inventario por lote/i });
    // Verificar que existen al menos dos filtros select (Sucursal + Estado)
    expect(screen.getAllByRole('combobox').length).toBeGreaterThanOrEqual(2);
  });

  it('muestra el switch "Solo con stock"', async () => {
    renderWithProviders(<ReporteLotesVencimientoPage />);
    await screen.findByText('Inventario por Lote y Vencimiento');
    expect(screen.getByText('Solo con stock')).toBeInTheDocument();
  });

  it('muestra el botón Buscar', async () => {
    renderWithProviders(<ReporteLotesVencimientoPage />);
    await screen.findByText('Inventario por Lote y Vencimiento');
    expect(screen.getByRole('button', { name: /buscar/i })).toBeInTheDocument();
  });

  it('muestra los KPIs cuando hay datos', async () => {
    renderWithProviders(<ReporteLotesVencimientoPage />);
    expect(await screen.findByText('Total lotes')).toBeInTheDocument();
    expect(screen.getByText('Total unidades')).toBeInTheDocument();
    expect(screen.getByText('Valor en inventario')).toBeInTheDocument();
    expect(screen.getByText('Vencidos')).toBeInTheDocument();
    expect(screen.getByText('Críticos (≤7d)')).toBeInTheDocument();
    expect(screen.getByText('Próximos (≤30d)')).toBeInTheDocument();
    expect(screen.getByText('Vigentes')).toBeInTheDocument();
  });

  it('muestra las columnas de la tabla', async () => {
    renderWithProviders(<ReporteLotesVencimientoPage />);
    await screen.findByText('Total lotes');
    expect(screen.getByText('Producto')).toBeInTheDocument();
    expect(screen.getByText('Nº Lote')).toBeInTheDocument();
    expect(screen.getByText('Fecha vence')).toBeInTheDocument();
    expect(screen.getByText('Estado')).toBeInTheDocument();
    expect(screen.getByText('Disponible')).toBeInTheDocument();
    // "Sucursal" aparece en el filtro (InputLabel) y en el encabezado de columna
    expect(screen.getAllByText('Sucursal').length).toBeGreaterThanOrEqual(2);
  });

  it('muestra los productos en la tabla', async () => {
    renderWithProviders(<ReporteLotesVencimientoPage />);
    expect(await screen.findByText('Leche entera 1L')).toBeInTheDocument();
    expect(screen.getByText('Yogurt fresa 200g')).toBeInTheDocument();
  });

  it('muestra los números de lote', async () => {
    renderWithProviders(<ReporteLotesVencimientoPage />);
    expect(await screen.findByText('L-2026-01')).toBeInTheDocument();
    expect(screen.getByText('L-2026-02')).toBeInTheDocument();
  });

  it('muestra el botón Excel cuando hay datos', async () => {
    renderWithProviders(<ReporteLotesVencimientoPage />);
    expect(await screen.findByRole('button', { name: /excel/i })).toBeInTheDocument();
  });

  it('muestra mensaje vacío cuando no hay lotes', async () => {
    const { lotesApi } = await import('@/api/lotes');
    vi.mocked(lotesApi.reporte).mockResolvedValue(
      makeLotesResponse({ items: [], totalLotes: 0, totalUnidades: 0, valorTotalInventario: 0 })
    );
    renderWithProviders(<ReporteLotesVencimientoPage />);
    expect(await screen.findByText(/no se encontraron lotes/i)).toBeInTheDocument();
  });

  it('muestra error cuando la API falla', async () => {
    const { lotesApi } = await import('@/api/lotes');
    vi.mocked(lotesApi.reporte).mockRejectedValue(new Error('Error de red'));
    renderWithProviders(<ReporteLotesVencimientoPage />);
    expect(await screen.findByText(/error al cargar el informe/i)).toBeInTheDocument();
  });
});
