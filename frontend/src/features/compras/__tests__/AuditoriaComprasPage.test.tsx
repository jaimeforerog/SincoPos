import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/test/test-utils';
import { AuditoriaComprasPage } from '../pages/AuditoriaComprasPage';

vi.mock('@/api/reportes', () => ({
  reportesApi: {
    auditoriaCompras: vi.fn(),
    historialOrden: vi.fn(),
  },
}));

vi.mock('@/features/auditoria/components/AuditTimeline', () => ({
  AuditTimeline: ({ entries }: { entries: unknown[] }) => (
    <div data-testid="audit-timeline">{entries.length} eventos</div>
  ),
}));

const makeAuditoriaResponse = (overrides = {}) => ({
  kpis: {
    totalEventos: 12,
    eventosExitosos: 10,
    eventosFallidos: 2,
    valorTotalComprado: 5_000_000,
    ordenesConErrorErp: 1,
    totalDevoluciones: 0,
  },
  logs: {
    items: [
      {
        id: 1,
        fechaHora: '2026-04-20T10:00:00Z',
        usuarioEmail: 'admin@test.com',
        usuarioNombre: 'Administrador',
        accion: 'CrearOrdenCompra',
        tipoEntidad: 'OrdenCompra',
        entidadId: '42',
        entidadNombre: 'OC-000042',
        nombreSucursal: 'Principal',
        exitosa: true,
        descripcion: 'Orden de compra OC-000042 creada',
        datosAnteriores: null,
        datosNuevos: null,
        mensajeError: null,
      },
    ],
    totalCount: 1,
    page: 1,
    pageSize: 50,
    totalPages: 1,
  },
  ...overrides,
});

describe('AuditoriaComprasPage', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    const { reportesApi } = await import('@/api/reportes');
    vi.mocked(reportesApi.auditoriaCompras).mockResolvedValue(makeAuditoriaResponse());
    vi.mocked(reportesApi.historialOrden).mockResolvedValue({ cambios: [] });
  });

  it('muestra el encabezado "Auditoría de Compras"', async () => {
    renderWithProviders(<AuditoriaComprasPage />);
    // El título se renderiza como <h5>; el breadcrumb usa <span> con el mismo texto
    expect(await screen.findByRole('heading', { name: /auditoría de compras/i })).toBeInTheDocument();
  });

  it('muestra los filtros de fecha, acción y usuario', async () => {
    renderWithProviders(<AuditoriaComprasPage />);
    await screen.findByRole('heading', { name: /auditoría de compras/i });
    expect(screen.getByLabelText(/desde/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/hasta/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/usuario/i)).toBeInTheDocument();
    // El select de acción es un combobox
    expect(screen.getAllByRole('combobox').length).toBeGreaterThanOrEqual(1);
  });

  it('muestra el botón Buscar', async () => {
    renderWithProviders(<AuditoriaComprasPage />);
    await screen.findByRole('heading', { name: /auditoría de compras/i });
    expect(screen.getByRole('button', { name: /buscar/i })).toBeInTheDocument();
  });

  it('muestra los KPIs cuando hay datos', async () => {
    renderWithProviders(<AuditoriaComprasPage />);
    expect(await screen.findByText('Total eventos')).toBeInTheDocument();
    expect(screen.getByText('Exitosos')).toBeInTheDocument();
    expect(screen.getByText('Fallidos')).toBeInTheDocument();
    expect(screen.getByText('Valor comprado')).toBeInTheDocument();
    expect(screen.getByText('Errores ERP')).toBeInTheDocument();
  });

  it('muestra los valores de KPI correctamente', async () => {
    renderWithProviders(<AuditoriaComprasPage />);
    expect(await screen.findByText('12')).toBeInTheDocument();
    expect(screen.getByText('10')).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument();
  });

  it('muestra las columnas de la tabla', async () => {
    renderWithProviders(<AuditoriaComprasPage />);
    await screen.findByRole('heading', { name: /auditoría de compras/i });
    expect(screen.getByText('Fecha/Hora')).toBeInTheDocument();
    expect(screen.getByText('Usuario')).toBeInTheDocument();
    expect(screen.getByText('Orden')).toBeInTheDocument();
    // "Resultado" aparece como label del select de filtro y como columna de la tabla
    expect(screen.getAllByText('Resultado').length).toBeGreaterThanOrEqual(2);
    // "Acción" aparece como label del select de filtro y como columna de la tabla
    expect(screen.getAllByText('Acción').length).toBeGreaterThanOrEqual(2);
    // "Sucursal" aparece en el header de la tabla
    expect(screen.getAllByText('Sucursal').length).toBeGreaterThanOrEqual(1);
  });

  it('muestra la fila del log con la acción como chip', async () => {
    renderWithProviders(<AuditoriaComprasPage />);
    expect(await screen.findByText('CrearOrdenCompra')).toBeInTheDocument();
  });

  it('muestra el nombre de la orden en la fila', async () => {
    renderWithProviders(<AuditoriaComprasPage />);
    expect(await screen.findByText('OC-000042')).toBeInTheDocument();
  });

  it('muestra mensaje vacío cuando no hay eventos', async () => {
    const { reportesApi } = await import('@/api/reportes');
    vi.mocked(reportesApi.auditoriaCompras).mockResolvedValue(
      makeAuditoriaResponse({ logs: { items: [], totalCount: 0, page: 1, pageSize: 50, totalPages: 0 } })
    );
    renderWithProviders(<AuditoriaComprasPage />);
    expect(await screen.findByText(/no se encontraron eventos/i)).toBeInTheDocument();
  });

  it('muestra error cuando la API falla', async () => {
    const { reportesApi } = await import('@/api/reportes');
    vi.mocked(reportesApi.auditoriaCompras).mockRejectedValue(new Error('Network error'));
    renderWithProviders(<AuditoriaComprasPage />);
    expect(await screen.findByText(/error al cargar/i)).toBeInTheDocument();
  });

  it('abre el diálogo de historial al hacer clic en una orden', async () => {
    renderWithProviders(<AuditoriaComprasPage />);
    const chip = await screen.findByText('OC-000042');
    await userEvent.click(chip);
    expect(await screen.findByText(/historial: OC-000042/i)).toBeInTheDocument();
  });
});
