import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/test/test-utils';
import TercerosPage from '../pages/TercerosPage';
import { tercerosApi } from '@/api/terceros';
import type { TerceroDTO, PaginatedResult } from '@/types/api';

vi.mock('@/api/terceros', () => ({
  tercerosApi: {
    getAll: vi.fn(),
    getById: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    deactivate: vi.fn(),
    calcularDV: vi.fn(),
    agregarActividad: vi.fn(),
    eliminarActividad: vi.fn(),
    establecerPrincipal: vi.fn(),
    descargarPlantilla: vi.fn(),
    importarExcel: vi.fn(),
  },
}));

const makeTercero = (overrides: Partial<TerceroDTO> = {}): TerceroDTO => ({
  id: 1,
  tipoIdentificacion: 'NIT',
  identificacion: '900123456',
  digitoVerificacion: '7',
  nombre: 'Proveedor Café S.A.S',
  tipoTercero: 'Proveedor',
  activo: true,
  perfilTributario: 'REGIMEN_COMUN',
  esGranContribuyente: false,
  esAutorretenedor: false,
  esResponsableIVA: true,
  origenDatos: 'Manual',
  actividades: [],
  ...overrides,
});

const makePage = (items: TerceroDTO[]): PaginatedResult<TerceroDTO> => ({
  items,
  totalCount: items.length,
  pageNumber: 1,
  pageSize: 50,
  totalPages: 1,
});

describe('TercerosPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(tercerosApi.getAll).mockResolvedValue(makePage([]));
  });

  it('muestra el título Terceros', async () => {
    renderWithProviders(<TercerosPage />);
    expect(await screen.findByRole('heading', { name: 'Terceros' })).toBeInTheDocument();
  });

  it('muestra el botón Nuevo Tercero', async () => {
    renderWithProviders(<TercerosPage />);
    expect(await screen.findByRole('button', { name: /nuevo tercero/i })).toBeInTheDocument();
  });

  it('muestra el campo de búsqueda', async () => {
    renderWithProviders(<TercerosPage />);
    expect(await screen.findByLabelText(/buscar por nombre o identificación/i)).toBeInTheDocument();
  });

  it('muestra mensaje vacío cuando no hay terceros', async () => {
    renderWithProviders(<TercerosPage />);
    expect(await screen.findByText('No se encontraron terceros.')).toBeInTheDocument();
  });

  it('muestra terceros en la tabla cuando la API retorna datos', async () => {
    vi.mocked(tercerosApi.getAll).mockResolvedValue(
      makePage([
        makeTercero(),
        makeTercero({ id: 2, nombre: 'Cliente ABC', tipoTercero: 'Cliente', identificacion: '12345678' }),
      ])
    );

    renderWithProviders(<TercerosPage />);

    expect(await screen.findByText('Proveedor Café S.A.S')).toBeInTheDocument();
    expect(await screen.findByText('Cliente ABC')).toBeInTheDocument();
  });

  it('muestra chip con tipo de tercero Proveedor', async () => {
    vi.mocked(tercerosApi.getAll).mockResolvedValue(makePage([makeTercero()]));

    renderWithProviders(<TercerosPage />);
    expect(await screen.findByText('Proveedor')).toBeInTheDocument();
  });

  it('abre el diálogo de nuevo tercero al hacer clic en el botón', async () => {
    renderWithProviders(<TercerosPage />);
    const btn = await screen.findByRole('button', { name: /nuevo tercero/i });
    await userEvent.click(btn);
    expect(await screen.findByRole('heading', { name: /nuevo tercero/i })).toBeInTheDocument();
  });

  it('muestra el conteo total de terceros', async () => {
    vi.mocked(tercerosApi.getAll).mockResolvedValue(makePage([makeTercero()]));

    renderWithProviders(<TercerosPage />);
    expect(await screen.findByText(/total: 1 tercero/i)).toBeInTheDocument();
  });
});
