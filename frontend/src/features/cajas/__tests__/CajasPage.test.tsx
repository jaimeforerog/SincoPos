import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/test/test-utils';
import { CajasPage } from '../pages/CajasPage';
import type { CajaDTO } from '@/types/api';

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({ activeSucursalId: 1, user: null, isAuthenticated: true }),
}));

vi.mock('@/api/cajas', () => ({
  cajasApi: {
    getAll: vi.fn(),
  },
}));

vi.mock('@/api/sucursales', () => ({
  sucursalesApi: {
    getAll: vi.fn().mockResolvedValue([
      { id: 1, nombre: 'Central', activa: true },
      { id: 2, nombre: 'Norte', activa: true },
    ]),
  },
}));

vi.mock('../components/AbrirCajaDialog', () => ({
  AbrirCajaDialog: ({ open }: { open: boolean }) =>
    open ? <div data-testid="abrir-caja-dialog" /> : null,
}));

vi.mock('../components/CerrarCajaDialog', () => ({
  CerrarCajaDialog: ({ open, caja }: { open: boolean; caja: CajaDTO | null }) =>
    open && caja ? <div data-testid="cerrar-caja-dialog">{caja.nombre}</div> : null,
}));

vi.mock('@/components/common/PageHeader', () => ({
  PageHeader: ({ title, action }: { title: string; action?: React.ReactNode }) => (
    <div>
      <h1>{title}</h1>
      {action}
    </div>
  ),
}));

const makeCaja = (overrides: Partial<CajaDTO> = {}): CajaDTO => ({
  id: 1,
  nombre: 'Caja Principal',
  sucursalId: 1,
  nombreSucursal: 'Central',
  estado: 'Abierta',
  montoApertura: 100000,
  montoActual: 250000,
  activa: true,
  fechaApertura: '2026-03-12T08:00:00Z',
  ...overrides,
});

describe('CajasPage', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    const { cajasApi } = await import('@/api/cajas');
    vi.mocked(cajasApi.getAll).mockResolvedValue([]);
  });

  it('muestra el título "Gestión de Cajas"', () => {
    renderWithProviders(<CajasPage />);
    expect(screen.getByRole('heading', { name: /gestión de cajas/i })).toBeInTheDocument();
  });

  it('muestra el selector de sucursal', () => {
    renderWithProviders(<CajasPage />);
    expect(screen.getByRole('combobox')).toBeInTheDocument();
  });

  it('auto-selecciona la sucursal activa del usuario y carga cajas', async () => {
    const { cajasApi } = await import('@/api/cajas');

    renderWithProviders(<CajasPage />);

    await waitFor(() => {
      expect(cajasApi.getAll).toHaveBeenCalledWith(
        expect.objectContaining({ sucursalId: 1 })
      );
    });
  });

  it('muestra alerta "No hay cajas registradas" cuando la sucursal no tiene cajas', async () => {
    renderWithProviders(<CajasPage />);

    expect(await screen.findByText(/no hay cajas registradas para esta sucursal/i)).toBeInTheDocument();
  });

  it('muestra sección "Cajas Abiertas" con la caja correcta', async () => {
    const { cajasApi } = await import('@/api/cajas');
    vi.mocked(cajasApi.getAll).mockResolvedValue([
      makeCaja({ nombre: 'Caja A', estado: 'Abierta' }),
    ]);

    renderWithProviders(<CajasPage />);

    expect(await screen.findByText(/cajas abiertas \(1\)/i)).toBeInTheDocument();
    expect(await screen.findByText('Caja A')).toBeInTheDocument();
  });

  it('muestra sección "Cajas Cerradas" con la caja correcta', async () => {
    const { cajasApi } = await import('@/api/cajas');
    vi.mocked(cajasApi.getAll).mockResolvedValue([
      makeCaja({ nombre: 'Caja B', estado: 'Cerrada', fechaCierre: '2026-03-12T18:00:00Z' }),
    ]);

    renderWithProviders(<CajasPage />);

    expect(await screen.findByText(/cajas cerradas \(1\)/i)).toBeInTheDocument();
    expect(await screen.findByText('Caja B')).toBeInTheDocument();
  });

  it('separa correctamente cajas abiertas y cerradas', async () => {
    const { cajasApi } = await import('@/api/cajas');
    vi.mocked(cajasApi.getAll).mockResolvedValue([
      makeCaja({ id: 1, nombre: 'Caja Abierta', estado: 'Abierta' }),
      makeCaja({ id: 2, nombre: 'Caja Cerrada', estado: 'Cerrada', fechaCierre: '2026-03-12T18:00:00Z' }),
    ]);

    renderWithProviders(<CajasPage />);

    expect(await screen.findByText(/cajas abiertas \(1\)/i)).toBeInTheDocument();
    expect(await screen.findByText(/cajas cerradas \(1\)/i)).toBeInTheDocument();
  });

  it('muestra botón "Cerrar Caja" en cajas abiertas', async () => {
    const { cajasApi } = await import('@/api/cajas');
    vi.mocked(cajasApi.getAll).mockResolvedValue([
      makeCaja({ estado: 'Abierta' }),
    ]);

    renderWithProviders(<CajasPage />);

    expect(await screen.findByRole('button', { name: /cerrar caja/i })).toBeInTheDocument();
  });

  it('no muestra botón "Cerrar Caja" en cajas cerradas', async () => {
    const { cajasApi } = await import('@/api/cajas');
    vi.mocked(cajasApi.getAll).mockResolvedValue([
      makeCaja({ estado: 'Cerrada', fechaCierre: '2026-03-12T18:00:00Z' }),
    ]);

    renderWithProviders(<CajasPage />);
    await screen.findByText(/cajas cerradas \(1\)/i);

    expect(screen.queryByRole('button', { name: /cerrar caja/i })).not.toBeInTheDocument();
  });

  it('abre AbrirCajaDialog al hacer clic en "Abrir Caja"', async () => {
    renderWithProviders(<CajasPage />);

    // Esperar que se carguen las sucursales y se auto-seleccione la sucursal activa
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /abrir caja/i })).not.toBeDisabled();
    });

    await userEvent.click(screen.getByRole('button', { name: /abrir caja/i }));
    expect(await screen.findByTestId('abrir-caja-dialog')).toBeInTheDocument();
  });

  it('abre CerrarCajaDialog al hacer clic en "Cerrar Caja"', async () => {
    const { cajasApi } = await import('@/api/cajas');
    vi.mocked(cajasApi.getAll).mockResolvedValue([
      makeCaja({ nombre: 'Caja Test', estado: 'Abierta' }),
    ]);

    renderWithProviders(<CajasPage />);
    await screen.findByText('Caja Test');

    await userEvent.click(screen.getByRole('button', { name: /cerrar caja/i }));
    expect(await screen.findByTestId('cerrar-caja-dialog')).toBeInTheDocument();
    expect(screen.getByTestId('cerrar-caja-dialog')).toHaveTextContent('Caja Test');
  });
});
