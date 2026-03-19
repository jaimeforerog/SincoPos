import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { ReporteVentasPage } from '../pages/ReporteVentasPage';

vi.mock('@/api/reportes', () => ({
  reportesApi: {
    ventas: vi.fn(),
  },
}));

vi.mock('@/api/sucursales', () => ({
  sucursalesApi: {
    getAll: vi.fn().mockResolvedValue([
      { id: 1, nombre: 'Principal', activo: true },
      { id: 2, nombre: 'Secundaria', activo: true },
    ]),
  },
}));

vi.mock('@/utils/exportReportes', () => ({
  exportarReporteVentas: vi.fn(),
}));

// Stub de gráficas pesadas
vi.mock('@mui/x-charts/LineChart', () => ({
  LineChart: ({ series }: { series: unknown[] }) => (
    <div data-testid="line-chart" data-series={series.length} />
  ),
}));
vi.mock('@mui/x-charts/PieChart', () => ({
  PieChart: () => <div data-testid="pie-chart" />,
}));

const makeReporteVentas = () => ({
  totalVentas: 2_000_000,
  cantidadVentas: 80,
  ticketPromedio: 25_000,
  costoTotal: 1_400_000,
  utilidadTotal: 600_000,
  margenPromedio: 30.0,
  ventasPorMetodoPago: [
    { metodo: 'Efectivo', total: 1_200_000, cantidad: 50 },
    { metodo: 'Tarjeta', total: 800_000, cantidad: 30 },
  ],
  ventasPorDia: [
    { fecha: '2026-03-01', total: 500_000, cantidad: 20, costoTotal: 350_000, utilidad: 150_000 },
    { fecha: '2026-03-15', total: 600_000, cantidad: 25, costoTotal: 420_000, utilidad: 180_000 },
  ],
});

describe('ReporteVentasPage', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    const { reportesApi } = await import('@/api/reportes');
    vi.mocked(reportesApi.ventas).mockResolvedValue(makeReporteVentas());
  });

  it('muestra el encabezado "Reporte de Ventas"', async () => {
    renderWithProviders(<ReporteVentasPage />);
    expect(await screen.findByText('Reporte de Ventas')).toBeInTheDocument();
  });

  it('muestra el panel de filtros', async () => {
    renderWithProviders(<ReporteVentasPage />);
    expect(await screen.findByText('Filtros de Búsqueda')).toBeInTheDocument();
  });

  it('muestra el botón "Generar Reporte"', async () => {
    renderWithProviders(<ReporteVentasPage />);
    expect(await screen.findByRole('button', { name: /generar reporte/i })).toBeInTheDocument();
  });

  it('muestra las métricas tras cargar el reporte', async () => {
    renderWithProviders(<ReporteVentasPage />);
    // "Total Ventas" y "Costo Total" aparecen en métricas Y en cabecera de tabla
    expect((await screen.findAllByText('Total Ventas')).length).toBeGreaterThanOrEqual(1);
    expect(await screen.findByText('Utilidad Total')).toBeInTheDocument();
    expect(await screen.findByText('Ticket Promedio')).toBeInTheDocument();
    expect((await screen.findAllByText('Costo Total')).length).toBeGreaterThanOrEqual(1);
  });

  it('muestra el botón "Exportar Excel" habilitado cuando hay datos', async () => {
    renderWithProviders(<ReporteVentasPage />);
    const exportBtn = await screen.findByRole('button', { name: /exportar excel/i });
    expect(exportBtn).not.toBeDisabled();
  });

  it('muestra el gráfico de ventas por día (LineChart)', async () => {
    renderWithProviders(<ReporteVentasPage />);
    expect(await screen.findByTestId('line-chart')).toBeInTheDocument();
  });

  it('muestra el gráfico de métodos de pago (PieChart)', async () => {
    renderWithProviders(<ReporteVentasPage />);
    expect(await screen.findByTestId('pie-chart')).toBeInTheDocument();
  });

  it('muestra la sección "Detalle por Día" con encabezados de tabla', async () => {
    renderWithProviders(<ReporteVentasPage />);
    expect(await screen.findByText('Detalle por Día')).toBeInTheDocument();
    expect(screen.getByText('Cantidad')).toBeInTheDocument();
    expect(screen.getByText('Utilidad')).toBeInTheDocument();
  });
});
