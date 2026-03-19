import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import AuditoriaPage from '../pages/AuditoriaPage';
import type { PaginatedResult, ActivityLogFullDTO } from '@/types/api';

vi.mock('@/hooks/useUiConfig', () => ({
  useUiConfig: () => ({
    showAuditLog: true,
    showSugerencias: true,
    showBusinessRadar: true,
  }),
}));

vi.mock('@/api/activityLogs', () => ({
  activityLogsApi: {
    getLogs: vi.fn(),
    getDashboard: vi.fn(),
  },
}));

const makeLog = (overrides: Partial<ActivityLogFullDTO> = {}): ActivityLogFullDTO => ({
  id: 1,
  fechaHora: '2026-03-18T10:00:00Z',
  usuarioEmail: 'admin@empresa.com',
  accion: 'CrearVenta',
  tipo: 2,
  tipoNombre: 'Venta',
  exitosa: true,
  sucursalId: 1,
  nombreSucursal: 'Principal',
  tipoEntidad: 'Venta',
  entidadId: '42',
  entidadNombre: 'V-000042',
  descripcion: null,
  datosAnteriores: null,
  datosNuevos: null,
  mensajeError: null,
  ipAddress: null,
  ...overrides,
});

const makePage = (items: ActivityLogFullDTO[]): PaginatedResult<ActivityLogFullDTO> => ({
  items,
  totalCount: items.length,
  pageNumber: 1,
  pageSize: 50,
  totalPages: 1,
});

describe('AuditoriaPage', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    const { activityLogsApi } = await import('@/api/activityLogs');
    vi.mocked(activityLogsApi.getLogs).mockResolvedValue(makePage([makeLog()]));
    vi.mocked(activityLogsApi.getDashboard).mockResolvedValue(null as never);
  });

  it('muestra el encabezado "Auditoría de Actividad"', async () => {
    renderWithProviders(<AuditoriaPage />);
    expect(await screen.findByText('Auditoría de Actividad')).toBeInTheDocument();
  });

  it('muestra el botón "Actualizar"', async () => {
    renderWithProviders(<AuditoriaPage />);
    expect(await screen.findByRole('button', { name: /actualizar/i })).toBeInTheDocument();
  });

  it('muestra el campo de usuario para filtrar', async () => {
    renderWithProviders(<AuditoriaPage />);
    expect(await screen.findByLabelText(/usuario \(email\)/i)).toBeInTheDocument();
  });

  it('muestra los encabezados de la tabla de logs', async () => {
    renderWithProviders(<AuditoriaPage />);
    expect(await screen.findByText('Fecha/Hora')).toBeInTheDocument();
    // "Usuario" aparece en cabecera de tabla Y en campo de filtro
    expect(screen.getAllByText(/usuario/i).length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('Acción')).toBeInTheDocument();
    expect(screen.getByText('Sucursal')).toBeInTheDocument();
  });

  it('muestra las filas de actividad en la tabla', async () => {
    renderWithProviders(<AuditoriaPage />);
    expect(await screen.findByText('admin@empresa.com')).toBeInTheDocument();
    // "CrearVenta" puede aparecer en tabla principal y en timeline reciente
    expect(screen.getAllByText('CrearVenta').length).toBeGreaterThanOrEqual(1);
  });

  it('muestra el chip del tipo de actividad "Venta"', async () => {
    renderWithProviders(<AuditoriaPage />);
    expect(await screen.findByText('Venta')).toBeInTheDocument();
  });

  it('sigue renderizando si la API falla', async () => {
    const { activityLogsApi } = await import('@/api/activityLogs');
    vi.mocked(activityLogsApi.getLogs).mockRejectedValue(new Error('Error red'));

    renderWithProviders(<AuditoriaPage />);
    expect(await screen.findByText('Auditoría de Actividad')).toBeInTheDocument();
  });
});
