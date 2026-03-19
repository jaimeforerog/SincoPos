import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { SucursalesPage } from '../pages/SucursalesPage';
import type { SucursalDTO } from '@/types/api';

// DataGrid requiere licencia en tests — reemplazar con stub ligero
vi.mock('@mui/x-data-grid', () => ({
  DataGrid: ({ rows }: { rows: unknown[] }) => (
    <div data-testid="sucursales-grid">{rows.length} sucursales</div>
  ),
  GridActionsCellItem: () => null,
}));

vi.mock('@/api/sucursales', () => ({
  sucursalesApi: {
    getAll: vi.fn(),
    delete: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    getById: vi.fn(),
    listar: vi.fn(),
  },
}));

const makeSucursal = (overrides: Partial<SucursalDTO> = {}): SucursalDTO => ({
  id: 1,
  nombre: 'Principal Bogotá',
  ciudad: 'Bogotá',
  activa: true,
  metodoCosteo: 'PrecioPonderado',
  centroCosto: 'CC-001',
  ...overrides,
});

describe('SucursalesPage', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    const { sucursalesApi } = await import('@/api/sucursales');
    vi.mocked(sucursalesApi.getAll).mockResolvedValue([makeSucursal()]);
  });

  it('muestra el encabezado "Sucursales"', async () => {
    renderWithProviders(<SucursalesPage />);
    expect((await screen.findAllByText('Sucursales')).length).toBeGreaterThanOrEqual(1);
  });

  it('muestra el botón "Nueva Sucursal"', async () => {
    renderWithProviders(<SucursalesPage />);
    expect(await screen.findByRole('button', { name: /nueva sucursal/i })).toBeInTheDocument();
  });

  it('muestra el switch "Incluir inactivas"', async () => {
    renderWithProviders(<SucursalesPage />);
    expect(await screen.findByLabelText(/incluir.*inactivas/i)).toBeInTheDocument();
  });

  it('muestra el DataGrid con las sucursales', async () => {
    renderWithProviders(<SucursalesPage />);
    expect(await screen.findByText('1 sucursales')).toBeInTheDocument();
    expect(screen.getByTestId('sucursales-grid')).toBeInTheDocument();
  });

  it('muestra 0 sucursales cuando la API retorna lista vacía', async () => {
    const { sucursalesApi } = await import('@/api/sucursales');
    vi.mocked(sucursalesApi.getAll).mockResolvedValue([]);
    renderWithProviders(<SucursalesPage />);
    expect(await screen.findByText('0 sucursales')).toBeInTheDocument();
  });
});
