import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/test/test-utils';
import { TrasladosPage } from '../pages/TrasladosPage';
import type { TrasladoDTO, PaginatedResult } from '@/types/api';

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({ user: null, activeSucursalId: 1, isAuthenticated: true }),
}));

vi.mock('@/api/traslados', () => ({
  trasladosApi: {
    listar: vi.fn(),
  },
}));

vi.mock('../components/CrearTrasladoDialog', () => ({
  CrearTrasladoDialog: ({ open }: { open: boolean }) =>
    open ? <div data-testid="crear-traslado-dialog" /> : null,
}));

vi.mock('../components/DetallesTrasladoDialog', () => ({
  DetallesTrasladoDialog: ({ open, trasladoId }: { open: boolean; trasladoId: number }) =>
    open ? <div data-testid="detalles-traslado-dialog">{trasladoId}</div> : null,
}));

const makeTraslado = (overrides: Partial<TrasladoDTO> = {}): TrasladoDTO => ({
  id: 1,
  numeroTraslado: 'TRN-000001',
  sucursalOrigenId: 1,
  nombreSucursalOrigen: 'Central',
  sucursalDestinoId: 2,
  nombreSucursalDestino: 'Norte',
  estado: 'Pendiente',
  fechaTraslado: '2026-03-12T10:00:00Z',
  detalles: [],
  ...overrides,
});

const makePage = (items: TrasladoDTO[]): PaginatedResult<TrasladoDTO> => ({
  items,
  totalCount: items.length,
  pageNumber: 1,
  pageSize: 50,
  totalPages: 1,
});

describe('TrasladosPage', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    const { trasladosApi } = await import('@/api/traslados');
    vi.mocked(trasladosApi.listar).mockResolvedValue(makePage([]));
  });

  it('muestra el título "Traslados entre Sucursales"', () => {
    renderWithProviders(<TrasladosPage />);
    expect(screen.getByRole('heading', { name: /traslados entre sucursales/i })).toBeInTheDocument();
  });

  it('muestra el botón "Nuevo Traslado"', () => {
    renderWithProviders(<TrasladosPage />);
    expect(screen.getByRole('button', { name: /nuevo traslado/i })).toBeInTheDocument();
  });

  it('muestra "No hay traslados registrados" cuando la lista está vacía', async () => {
    renderWithProviders(<TrasladosPage />);
    expect(await screen.findByText(/no hay traslados registrados/i)).toBeInTheDocument();
  });

  it('muestra filas de traslados cuando la API retorna datos', async () => {
    const { trasladosApi } = await import('@/api/traslados');
    vi.mocked(trasladosApi.listar).mockResolvedValue(
      makePage([makeTraslado({
        numeroTraslado: 'TRN-000099',
        nombreSucursalOrigen: 'Central',
        nombreSucursalDestino: 'Norte',
      })])
    );

    renderWithProviders(<TrasladosPage />);

    expect(await screen.findByText('TRN-000099')).toBeInTheDocument();
    expect(await screen.findByText('Central')).toBeInTheDocument();
    expect(await screen.findByText('Norte')).toBeInTheDocument();
  });

  it('muestra el conteo de productos en cada traslado', async () => {
    const { trasladosApi } = await import('@/api/traslados');
    vi.mocked(trasladosApi.listar).mockResolvedValue(
      makePage([makeTraslado({
        detalles: [
          { id: 1, productoId: 'p1', nombreProducto: 'A', codigoBarras: '1', cantidadSolicitada: 2, cantidadRecibida: 0, costoUnitario: 100 },
          { id: 2, productoId: 'p2', nombreProducto: 'B', codigoBarras: '2', cantidadSolicitada: 1, cantidadRecibida: 0, costoUnitario: 200 },
        ],
      })])
    );

    renderWithProviders(<TrasladosPage />);
    expect(await screen.findByText('2 producto(s)')).toBeInTheDocument();
  });

  it('muestra chip "Pendiente" para traslados en estado Pendiente', async () => {
    const { trasladosApi } = await import('@/api/traslados');
    vi.mocked(trasladosApi.listar).mockResolvedValue(
      makePage([makeTraslado({ estado: 'Pendiente' })])
    );

    renderWithProviders(<TrasladosPage />);
    expect(await screen.findByText('Pendiente')).toBeInTheDocument();
  });

  it('muestra chip "Recibido" para traslados completados', async () => {
    const { trasladosApi } = await import('@/api/traslados');
    vi.mocked(trasladosApi.listar).mockResolvedValue(
      makePage([makeTraslado({ estado: 'Recibido' })])
    );

    renderWithProviders(<TrasladosPage />);
    expect(await screen.findByText('Recibido')).toBeInTheDocument();
  });

  it('abre CrearTrasladoDialog al hacer clic en "Nuevo Traslado"', async () => {
    renderWithProviders(<TrasladosPage />);
    await userEvent.click(screen.getByRole('button', { name: /nuevo traslado/i }));
    expect(await screen.findByTestId('crear-traslado-dialog')).toBeInTheDocument();
  });

  it('abre DetallesTrasladoDialog al hacer clic en el ícono de ver', async () => {
    const { trasladosApi } = await import('@/api/traslados');
    vi.mocked(trasladosApi.listar).mockResolvedValue(
      makePage([makeTraslado({ id: 42 })])
    );

    renderWithProviders(<TrasladosPage />);
    await screen.findByText('TRN-000001');

    await userEvent.click(screen.getByRole('button', { name: /ver detalles/i }));
    expect(await screen.findByTestId('detalles-traslado-dialog')).toBeInTheDocument();
    expect(screen.getByTestId('detalles-traslado-dialog')).toHaveTextContent('42');
  });

  it('muestra Alert de error cuando la API falla', async () => {
    const { trasladosApi } = await import('@/api/traslados');
    vi.mocked(trasladosApi.listar).mockRejectedValue(new Error('Error de red'));

    renderWithProviders(<TrasladosPage />);

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });
    expect(screen.getByText(/error al cargar los traslados/i)).toBeInTheDocument();
  });
});
