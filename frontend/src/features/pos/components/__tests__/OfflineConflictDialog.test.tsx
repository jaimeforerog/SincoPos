import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, fireEvent, waitFor, act } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { OfflineConflictDialog } from '../OfflineConflictDialog';
import type { OfflineVenta } from '@/offline/posIdb';

// ── Mocks ──────────────────────────────────────────────────────────────────

const mockGetFailed     = vi.fn();
const mockRetry         = vi.fn();
const mockDeleteFailed  = vi.fn();
const mockDiscardAll    = vi.fn();
const mockSyncPending   = vi.fn();

vi.mock('@/offline/offlineQueue.service', () => ({
  getFailedVentasForDisplay: (...args: unknown[]) => mockGetFailed(...args),
  retryVenta:                (...args: unknown[]) => mockRetry(...args),
  deleteFailedVenta:         (...args: unknown[]) => mockDeleteFailed(...args),
  discardFailedVentas:       (...args: unknown[]) => mockDiscardAll(...args),
  syncPending:               (...args: unknown[]) => mockSyncPending(...args),
}));

// ── Fixtures ───────────────────────────────────────────────────────────────

const makeVenta = (localId: string, overrides: Partial<OfflineVenta> = {}): OfflineVenta => ({
  localId,
  payload: {
    sucursalId: 1,
    cajaId:     1,
    metodoPago: 0,
    montoPagado: 50_000,
    lineas: [
      { productoId: 'p1', cantidad: 2, precioUnitario: 20_000, descuento: 0 },
      { productoId: 'p2', cantidad: 1, precioUnitario: 10_000, descuento: 0 },
    ],
  },
  creadoEn:    '2026-03-18T10:30:00Z',
  intentos:    2,
  estado:      'failed',
  errorMensaje: 'Stock insuficiente para Arroz Premium',
  ...overrides,
});

// ── Tests ──────────────────────────────────────────────────────────────────

describe('OfflineConflictDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetFailed.mockResolvedValue([]);
    mockRetry.mockResolvedValue(undefined);
    mockDeleteFailed.mockResolvedValue(undefined);
    mockDiscardAll.mockResolvedValue(undefined);
    mockSyncPending.mockResolvedValue({ synced: 0, failed: 0, tokenExpired: false, errors: [] });
  });

  it('no renderiza nada cuando open=false', () => {
    renderWithProviders(<OfflineConflictDialog open={false} onClose={vi.fn()} />);
    expect(screen.queryByText(/Ventas con error/i)).not.toBeInTheDocument();
  });

  it('muestra el título del diálogo', async () => {
    renderWithProviders(<OfflineConflictDialog open={true} onClose={vi.fn()} />);
    await screen.findByText(/Ventas con error de sincronización/i);
  });

  it('muestra mensaje vacío cuando no hay ventas fallidas', async () => {
    mockGetFailed.mockResolvedValue([]);
    renderWithProviders(<OfflineConflictDialog open={true} onClose={vi.fn()} />);
    await screen.findByText(/No hay ventas con errores pendientes/i);
  });

  it('muestra la lista de ventas fallidas', async () => {
    mockGetFailed.mockResolvedValue([
      makeVenta('local-001'),
      makeVenta('local-002', { errorMensaje: 'Caja cerrada' }),
    ]);

    renderWithProviders(<OfflineConflictDialog open={true} onClose={vi.fn()} />);

    // Al haber 2 ventas con el mismo error, usamos getAllByText
    await waitFor(() => {
      const errores = screen.getAllByText('Stock insuficiente para Arroz Premium');
      expect(errores.length).toBeGreaterThanOrEqual(1);
    });
    expect(screen.getByText('Caja cerrada')).toBeInTheDocument();
  });

  it('muestra chips con cantidad de ítems y total por venta', async () => {
    mockGetFailed.mockResolvedValue([makeVenta('local-001')]);

    renderWithProviders(<OfflineConflictDialog open={true} onClose={vi.fn()} />);

    await waitFor(() => {
      // 2 ítems en lineas
      expect(screen.getByText('2 ítems')).toBeInTheDocument();
    });
  });

  it('muestra el chip del método de pago', async () => {
    mockGetFailed.mockResolvedValue([makeVenta('local-001')]);
    renderWithProviders(<OfflineConflictDialog open={true} onClose={vi.fn()} />);
    await screen.findByText('Efectivo');
  });

  it('muestra el número de intentos', async () => {
    mockGetFailed.mockResolvedValue([makeVenta('local-001', { intentos: 2 })]);
    renderWithProviders(<OfflineConflictDialog open={true} onClose={vi.fn()} />);
    await screen.findByText('2 intentos');
  });

  it('llama a retryVenta y syncPending al hacer clic en Reintentar venta', async () => {
    mockGetFailed.mockResolvedValue([makeVenta('local-001')]);
    renderWithProviders(<OfflineConflictDialog open={true} onClose={vi.fn()} />);

    await screen.findByText('Stock insuficiente para Arroz Premium');

    // aria-label="Reintentar venta" es el botón por-venta; "Reintentar todas" es el footer
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Reintentar venta' }));
    });

    expect(mockRetry).toHaveBeenCalledWith('local-001');
    expect(mockSyncPending).toHaveBeenCalled();
  });

  it('llama a deleteFailedVenta al hacer clic en Eliminar', async () => {
    mockGetFailed.mockResolvedValue([makeVenta('local-001')]);
    renderWithProviders(<OfflineConflictDialog open={true} onClose={vi.fn()} />);

    await screen.findByText('Stock insuficiente para Arroz Premium');

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /eliminar/i }));
    });

    expect(mockDeleteFailed).toHaveBeenCalledWith('local-001');
  });

  it('llama a discardFailedVentas y cierra el diálogo al descartar todas', async () => {
    mockGetFailed.mockResolvedValue([
      makeVenta('local-001'),
      makeVenta('local-002', { errorMensaje: 'Precio inválido' }),
    ]);
    const onClose = vi.fn();
    renderWithProviders(<OfflineConflictDialog open={true} onClose={onClose} />);

    await screen.findByText('Precio inválido');

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /descartar todas/i }));
    });

    expect(mockDiscardAll).toHaveBeenCalled();
    expect(onClose).toHaveBeenCalled();
  });

  it('cierra el diálogo al hacer clic en el botón "Cerrar" del footer', async () => {
    mockGetFailed.mockResolvedValue([]);
    const onClose = vi.fn();
    renderWithProviders(<OfflineConflictDialog open={true} onClose={onClose} />);

    await screen.findByText(/No hay ventas con errores/i);

    // El botón de texto "Cerrar" está en el footer
    fireEvent.click(screen.getByRole('button', { name: 'Cerrar' }));
    expect(onClose).toHaveBeenCalled();
  });

  it('cierra el diálogo al hacer clic en el botón X del título', async () => {
    mockGetFailed.mockResolvedValue([]);
    const onClose = vi.fn();
    renderWithProviders(<OfflineConflictDialog open={true} onClose={onClose} />);

    await screen.findByText(/No hay ventas con errores/i);

    // El botón X tiene aria-label="Cerrar diálogo"
    fireEvent.click(screen.getByRole('button', { name: 'Cerrar diálogo' }));
    expect(onClose).toHaveBeenCalled();
  });

  it('no muestra los botones de acción cuando no hay ventas fallidas', async () => {
    mockGetFailed.mockResolvedValue([]);
    renderWithProviders(<OfflineConflictDialog open={true} onClose={vi.fn()} />);

    await screen.findByText(/No hay ventas con errores/i);

    expect(screen.queryByRole('button', { name: /reintentar todas/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /descartar todas/i })).not.toBeInTheDocument();
  });

  it('muestra los botones de acción cuando hay ventas fallidas', async () => {
    mockGetFailed.mockResolvedValue([makeVenta('local-001')]);
    renderWithProviders(<OfflineConflictDialog open={true} onClose={vi.fn()} />);

    await screen.findByText('Stock insuficiente para Arroz Premium');
    expect(screen.getByRole('button', { name: /reintentar todas/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /descartar todas/i })).toBeInTheDocument();
  });

  it('vuelve a cargar la lista de ventas al abrir el diálogo', async () => {
    mockGetFailed.mockResolvedValue([makeVenta('local-001')]);
    const { rerender } = renderWithProviders(
      <OfflineConflictDialog open={false} onClose={vi.fn()} />
    );

    expect(mockGetFailed).not.toHaveBeenCalled();

    rerender(
      <OfflineConflictDialog open={true} onClose={vi.fn()} />
    );

    await waitFor(() => {
      expect(mockGetFailed).toHaveBeenCalledTimes(1);
    });
  });
});
