import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/test/test-utils';
import { Routes, Route } from 'react-router-dom';
import { ComprasPage } from '../pages/ComprasPage';
import type { OrdenCompraDTO, PaginatedResult } from '@/types/api';

vi.mock('@/api/compras', () => ({
  comprasApi: {
    getAll: vi.fn(),
    getErroresErp: vi.fn().mockResolvedValue([]),
    reintentarErp: vi.fn().mockResolvedValue({}),
  },
}));

// Stub de diálogos pesados
vi.mock('../components/OrdenCompraFormDialog', () => ({
  OrdenCompraFormDialog: ({ open }: { open: boolean }) =>
    open ? <div data-testid="form-dialog" /> : null,
}));
vi.mock('../components/OrdenCompraDetalleDialog', () => ({
  OrdenCompraDetalleDialog: ({ open }: { open: boolean }) =>
    open ? <div data-testid="detalle-dialog" /> : null,
}));
vi.mock('../components/AprobarOrdenDialog', () => ({
  AprobarOrdenDialog: ({ open }: { open: boolean }) =>
    open ? <div data-testid="aprobar-dialog" /> : null,
}));
vi.mock('../components/RechazarOrdenDialog', () => ({
  RechazarOrdenDialog: ({ open }: { open: boolean }) =>
    open ? <div data-testid="rechazar-dialog" /> : null,
}));
vi.mock('../components/RecibirOrdenDialog', () => ({
  RecibirOrdenDialog: ({ open }: { open: boolean }) =>
    open ? <div data-testid="recibir-dialog" /> : null,
}));
vi.mock('../components/CancelarOrdenDialog', () => ({
  CancelarOrdenDialog: ({ open }: { open: boolean }) =>
    open ? <div data-testid="cancelar-dialog" /> : null,
}));

const makeOrden = (overrides: Partial<OrdenCompraDTO> = {}): OrdenCompraDTO => ({
  id: 1,
  numeroOrden: 'OC-000001',
  sucursalId: 1,
  nombreSucursal: 'Central',
  proveedorId: 10,
  nombreProveedor: 'Proveedor ABC',
  estado: 'Pendiente',
  formaPago: 'Contado',
  diasPlazo: 0,
  fechaOrden: '2026-03-12T10:00:00Z',
  subtotal: 50000,
  impuestos: 9500,
  total: 59500,
  requiereFacturaElectronica: false,
  sincronizadoErp: false,
  detalles: [],
  ...overrides,
});

const makePage = (items: OrdenCompraDTO[]): PaginatedResult<OrdenCompraDTO> => ({
  items,
  totalCount: items.length,
  pageNumber: 1,
  pageSize: 100,
  totalPages: 1,
});

describe('ComprasPage', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    const { comprasApi } = await import('@/api/compras');
    vi.mocked(comprasApi.getAll).mockResolvedValue(makePage([]));
    vi.mocked(comprasApi.getErroresErp).mockResolvedValue([]);
  });

  it('muestra el título "Órdenes de Compra"', () => {
    renderWithProviders(<ComprasPage />);
    expect(screen.getByRole('heading', { name: /órdenes de compra/i })).toBeInTheDocument();
  });

  it('muestra botón "Nueva Orden"', () => {
    renderWithProviders(<ComprasPage />);
    expect(screen.getByRole('button', { name: /nueva orden/i })).toBeInTheDocument();
  });

  it('abre el formulario al hacer clic en "Nueva Orden"', async () => {
    renderWithProviders(
      <Routes>
        <Route path="/" element={<ComprasPage />} />
        <Route path="/compras/nueva" element={<div data-testid="nueva-orden-page" />} />
      </Routes>
    );
    await userEvent.click(screen.getByRole('button', { name: /nueva orden/i }));
    expect(await screen.findByTestId('nueva-orden-page')).toBeInTheDocument();
  });

  it('muestra "No hay órdenes" cuando la lista está vacía', async () => {
    renderWithProviders(<ComprasPage />);
    expect(await screen.findByText(/no hay órdenes de compra registradas/i)).toBeInTheDocument();
  });

  it('muestra filas de órdenes cuando la API retorna datos', async () => {
    const { comprasApi } = await import('@/api/compras');
    vi.mocked(comprasApi.getAll).mockResolvedValue(
      makePage([makeOrden({ numeroOrden: 'OC-000001', nombreProveedor: 'Proveedor ABC' })])
    );

    renderWithProviders(<ComprasPage />);

    expect(await screen.findByText('OC-000001')).toBeInTheDocument();
    expect(await screen.findByText('Proveedor ABC')).toBeInTheDocument();
  });

  it('muestra botones Aprobar y Rechazar para órdenes en estado Pendiente', async () => {
    const { comprasApi } = await import('@/api/compras');
    vi.mocked(comprasApi.getAll).mockResolvedValue(
      makePage([makeOrden({ estado: 'Pendiente' })])
    );

    renderWithProviders(<ComprasPage />);
    await screen.findByText('OC-000001');

    expect(screen.getByRole('button', { name: /aprobar/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /rechazar/i })).toBeInTheDocument();
  });

  it('NO muestra Aprobar/Rechazar para órdenes en estado Aprobada', async () => {
    const { comprasApi } = await import('@/api/compras');
    vi.mocked(comprasApi.getAll).mockResolvedValue(
      makePage([makeOrden({ estado: 'Aprobada' })])
    );

    renderWithProviders(<ComprasPage />);
    await screen.findByText('OC-000001');

    expect(screen.queryByRole('button', { name: /aprobar/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /rechazar/i })).not.toBeInTheDocument();
  });

  it('muestra botón Recibir para órdenes en estado Aprobada', async () => {
    const { comprasApi } = await import('@/api/compras');
    vi.mocked(comprasApi.getAll).mockResolvedValue(
      makePage([makeOrden({ estado: 'Aprobada' })])
    );

    renderWithProviders(<ComprasPage />);
    await screen.findByText('OC-000001');

    expect(screen.getByRole('button', { name: /recibir mercancía/i })).toBeInTheDocument();
  });

  it('abre diálogo de detalle al hacer clic en Ver detalle', async () => {
    const { comprasApi } = await import('@/api/compras');
    vi.mocked(comprasApi.getAll).mockResolvedValue(makePage([makeOrden()]));

    renderWithProviders(<ComprasPage />);
    await screen.findByText('OC-000001');

    await userEvent.click(screen.getByRole('button', { name: /ver detalle/i }));
    expect(await screen.findByTestId('detalle-dialog')).toBeInTheDocument();
  });

  it('muestra Alert de error cuando la API falla', async () => {
    const { comprasApi } = await import('@/api/compras');
    vi.mocked(comprasApi.getAll).mockRejectedValue(new Error('Error de red'));

    renderWithProviders(<ComprasPage />);

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(await screen.findByText(/error al cargar las órdenes de compra/i)).toBeInTheDocument();
  });

  it('muestra chip de estado correcto', async () => {
    const { comprasApi } = await import('@/api/compras');
    vi.mocked(comprasApi.getAll).mockResolvedValue(
      makePage([makeOrden({ estado: 'RecibidaCompleta' })])
    );

    renderWithProviders(<ComprasPage />);

    expect(await screen.findByText('RecibidaCompleta')).toBeInTheDocument();
  });

  it('filtra por estado al cambiar el selector', async () => {
    const { comprasApi } = await import('@/api/compras');
    vi.mocked(comprasApi.getAll).mockResolvedValue(makePage([]));

    renderWithProviders(<ComprasPage />);

    // El filtro usa Chips en lugar de Select — clic en el chip "Pendiente"
    const chipPendiente = await screen.findByRole('button', { name: /^pendiente$/i });
    await userEvent.click(chipPendiente);

    await waitFor(() => {
      expect(comprasApi.getAll).toHaveBeenCalledWith(
        expect.objectContaining({ estado: 'Pendiente' })
      );
    });
  });
});
