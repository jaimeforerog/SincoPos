import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { ReportesHomePage } from '../pages/ReportesHomePage';
import type { DashboardDTO } from '@/types/api';

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({
    activeSucursalId: 1,
    user: { email: 'admin@test.com', role: 'admin' },
    isAuthenticated: true,
    hasAnyRole: () => true,
  }),
}));

vi.mock('@/api/reportes', () => ({
  reportesApi: {
    dashboard: vi.fn(),
  },
}));

const makeDashboard = (overrides: Partial<DashboardDTO> = {}): DashboardDTO => ({
  metricasDelDia: {
    ventasTotales: 1_500_000,
    ventasAyer: 1_200_000,
    porcentajeCambio: 25.0,
    cantidadVentas: 50,
    productosVendidos: 200,
    clientesAtendidos: 45,
    ticketPromedio: 30_000,
    utilidadDelDia: 450_000,
    margenPromedio: 30.0,
  },
  ventasPorHora: [{ hora: 10, total: 200_000, cantidad: 8 }],
  topProductos: [],
  alertasStock: [],
  ...overrides,
});

describe('ReportesHomePage', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    const { reportesApi } = await import('@/api/reportes');
    vi.mocked(reportesApi.dashboard).mockResolvedValue(makeDashboard());
  });

  it('muestra el título "Centro de Reportes"', async () => {
    renderWithProviders(<ReportesHomePage />);
    expect(await screen.findByText('Centro de Reportes')).toBeInTheDocument();
  });

  it('muestra el chip HOY en el hero', async () => {
    renderWithProviders(<ReportesHomePage />);
    expect(await screen.findByText('HOY')).toBeInTheDocument();
  });

  it('muestra los KPI labels del hero', async () => {
    renderWithProviders(<ReportesHomePage />);
    await screen.findByText('Centro de Reportes');
    expect(screen.getByText('Ventas hoy')).toBeInTheDocument();
    expect(screen.getByText('Transacciones')).toBeInTheDocument();
    expect(screen.getByText('Ticket promedio')).toBeInTheDocument();
    expect(screen.getByText('Margen')).toBeInTheDocument();
  });

  it('muestra las tarjetas de módulos de reporte', async () => {
    renderWithProviders(<ReportesHomePage />);
    expect(await screen.findByText('Reporte de Ventas')).toBeInTheDocument();
    expect(screen.getByText('Inventario Valorizado')).toBeInTheDocument();
    expect(screen.getByText('Kardex de Inventario')).toBeInTheDocument();
    expect(screen.getByText('Auditoría de Actividad')).toBeInTheDocument();
  });

  it('muestra la cantidad de módulos disponibles', async () => {
    renderWithProviders(<ReportesHomePage />);
    expect(await screen.findByText(/módulos? disponibles/i)).toBeInTheDocument();
  });

  it('llama a reportesApi.dashboard con sucursalId', async () => {
    const { reportesApi } = await import('@/api/reportes');
    renderWithProviders(<ReportesHomePage />);
    await screen.findByText('Centro de Reportes');
    expect(vi.mocked(reportesApi.dashboard)).toHaveBeenCalledWith({ sucursalId: 1 });
  });

  it('sigue renderizando si el dashboard falla', async () => {
    const { reportesApi } = await import('@/api/reportes');
    vi.mocked(reportesApi.dashboard).mockRejectedValue(new Error('Error red'));
    renderWithProviders(<ReportesHomePage />);
    await waitFor(() => {
      expect(screen.getByText('Centro de Reportes')).toBeInTheDocument();
    });
  });
});
