import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { DashboardPage } from '../pages/DashboardPage';
import type { DashboardDTO } from '@/types/api';

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({
    activeSucursalId: 1,
    user: null,
    isAuthenticated: true,
  }),
}));

vi.mock('@/api/reportes', () => ({
  reportesApi: {
    dashboard: vi.fn(),
  },
}));

vi.mock('@/api/ventas', () => ({
  ventasApi: {
    getErpPendientesCount: vi.fn().mockResolvedValue(0),
  },
}));

vi.mock('@/api/lotes', () => ({
  lotesApi: {
    proximosAVencer: vi.fn().mockResolvedValue([]),
    obtenerAlertas: vi.fn().mockResolvedValue([]),
  },
}));

// Stub de componentes con librerías pesadas (recharts)
vi.mock('../components/SalesChart', () => ({
  SalesChart: () => <div data-testid="sales-chart" />,
}));
vi.mock('../components/TopProductsTable', () => ({
  TopProductsTable: ({ products }: { products: unknown[] }) => (
    <div data-testid="top-products-table">{products.length} productos</div>
  ),
}));
vi.mock('../components/StockAlertsTable', () => ({
  StockAlertsTable: () => <div data-testid="stock-alerts-table" />,
}));
vi.mock('../components/AlertasVencimientoTable', () => ({
  AlertasVencimientoTable: () => <div data-testid="alertas-vencimiento-table" />,
}));

const makeDashboard = (overrides: Partial<DashboardDTO> = {}): DashboardDTO => ({
  metricasDelDia: {
    ventasTotales: 500000,
    ventasAyer: 420000,
    porcentajeCambio: 19.05,
    cantidadVentas: 42,
    productosVendidos: 130,
    clientesAtendidos: 35,
    ticketPromedio: 11904,
    utilidadDelDia: 150000,
    margenPromedio: 30.0,
  },
  ventasPorHora: [{ hora: 9, total: 50000, cantidad: 5 }],
  topProductos: [
    {
      productoId: 'prod-1',
      codigoBarras: '7701234',
      nombre: 'Café Premium',
      categoria: 'Bebidas',
      cantidadVendida: 20,
      totalVendido: 200000,
    },
  ],
  alertasStock: [],
  ...overrides,
});

describe('DashboardPage', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    const { reportesApi } = await import('@/api/reportes');
    vi.mocked(reportesApi.dashboard).mockResolvedValue(makeDashboard());
    const { ventasApi } = await import('@/api/ventas');
    vi.mocked(ventasApi.getErpPendientesCount).mockResolvedValue(0);
  });

  it('muestra CircularProgress mientras carga', () => {
    renderWithProviders(<DashboardPage />);
    expect(screen.getByRole('progressbar')).toBeInTheDocument();
  });

  it('muestra el título "Dashboard" cuando los datos cargan', async () => {
    renderWithProviders(<DashboardPage />);
    expect(await screen.findByRole('heading', { name: /dashboard/i })).toBeInTheDocument();
  });

  it('muestra las métricas del día', async () => {
    renderWithProviders(<DashboardPage />);

    // Ventas totales formateadas en COP
    expect(await screen.findByText('Ventas Totales')).toBeInTheDocument();
    expect(await screen.findByText('Cantidad de Ventas')).toBeInTheDocument();
    expect(await screen.findByText('Ticket Promedio')).toBeInTheDocument();
    expect(await screen.findByText('Margen Promedio')).toBeInTheDocument();
  });

  it('muestra la MetricCard de Pendiente ERP con valor 0', async () => {
    renderWithProviders(<DashboardPage />);

    expect(await screen.findByText('Pendiente ERP')).toBeInTheDocument();
    expect(await screen.findByText('Todo sincronizado')).toBeInTheDocument();
  });

  it('muestra "Sin sincronizar" cuando hay pendientes ERP', async () => {
    const { ventasApi } = await import('@/api/ventas');
    vi.mocked(ventasApi.getErpPendientesCount).mockResolvedValue(3);

    renderWithProviders(<DashboardPage />);

    expect(await screen.findByText('Sin sincronizar')).toBeInTheDocument();
  });

  it('muestra el SalesChart', async () => {
    renderWithProviders(<DashboardPage />);
    expect(await screen.findByTestId('sales-chart')).toBeInTheDocument();
  });

  it('muestra TopProductsTable con el conteo de productos', async () => {
    renderWithProviders(<DashboardPage />);
    expect(await screen.findByTestId('top-products-table')).toBeInTheDocument();
    expect(screen.getByTestId('top-products-table')).toHaveTextContent('1 productos');
  });

  it('muestra Alert de error cuando la API del dashboard falla', async () => {
    const { reportesApi } = await import('@/api/reportes');
    vi.mocked(reportesApi.dashboard).mockRejectedValue(new Error('Error de red'));

    renderWithProviders(<DashboardPage />);

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });
    expect(screen.getByText(/error al cargar el dashboard/i)).toBeInTheDocument();
  });

  it('no muestra AlertasVencimientoTable cuando no hay alertas', async () => {
    renderWithProviders(<DashboardPage />);
    await screen.findByRole('heading', { name: /dashboard/i });

    expect(screen.queryByTestId('alertas-vencimiento-table')).not.toBeInTheDocument();
  });
});
