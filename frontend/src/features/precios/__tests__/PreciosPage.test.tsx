import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { PreciosPage } from '../pages/PreciosPage';

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({
    isSupervisor: () => true,
  }),
}));

vi.mock('@/api/sucursales', () => ({
  sucursalesApi: {
    getAll: vi.fn().mockResolvedValue([
      { id: 1, nombre: 'Principal', activo: true },
      { id: 2, nombre: 'Norte', activo: true },
    ]),
  },
}));

vi.mock('@/api/productos', () => ({
  productosApi: {
    getAll: vi.fn().mockResolvedValue({
      items: [
        {
          id: 'p1',
          codigoBarras: '7701234',
          nombre: 'Coca-Cola 350ml',
          precioCosto: 2_000,
          precioVenta: 3_500,
          activo: true,
          categoriaId: 1,
          manejaLotes: false,
        },
      ],
      totalCount: 1,
    }),
  },
}));

vi.mock('@/api/precios', () => ({
  preciosApi: {
    resolver: vi.fn().mockResolvedValue({
      precioVenta: 3_500,
      precioMinimo: undefined,
      origen: 'Producto',
      origenDato: 'Manual',
    }),
    createOrUpdate: vi.fn(),
  },
}));

describe('PreciosPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('muestra el encabezado "Precios Sucursal"', async () => {
    renderWithProviders(<PreciosPage />);
    // Aparece en breadcrumb + h5 → varios elementos
    expect((await screen.findAllByText('Precios Sucursal')).length).toBeGreaterThanOrEqual(1);
  });

  it('muestra el selector de sucursal', async () => {
    renderWithProviders(<PreciosPage />);
    // Esperar a que cargue la página
    await screen.findAllByText('Precios Sucursal');
    // MUI Select → role combobox
    expect(screen.getByRole('combobox')).toBeInTheDocument();
  });

  it('muestra el campo "Buscar Producto" (deshabilitado sin sucursal)', async () => {
    renderWithProviders(<PreciosPage />);
    expect(
      await screen.findByPlaceholderText(/nombre o código de barras/i)
    ).toBeDisabled();
  });

  it('muestra la alerta "Selecciona una sucursal" sin sucursal elegida', async () => {
    renderWithProviders(<PreciosPage />);
    expect(
      await screen.findByText(/selecciona una sucursal para gestionar precios/i)
    ).toBeInTheDocument();
  });

  it('no muestra la tabla inicialmente (sin sucursal seleccionada)', async () => {
    renderWithProviders(<PreciosPage />);
    await screen.findAllByText('Precios Sucursal');
    expect(screen.queryByText('Código Barras')).not.toBeInTheDocument();
  });

  it('no muestra el botón "Importar desde Excel" sin sucursal seleccionada', async () => {
    renderWithProviders(<PreciosPage />);
    await screen.findAllByText('Precios Sucursal');
    expect(
      screen.queryByRole('button', { name: /importar desde excel/i })
    ).not.toBeInTheDocument();
  });
});
