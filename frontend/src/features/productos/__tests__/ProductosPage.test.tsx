import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { ProductosPage } from '../pages/ProductosPage';
import type { ProductoDTO } from '@/types/api';

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({
    isSupervisor: () => true,
    isAdmin: () => true,
  }),
}));

vi.mock('@/api/productos', () => ({
  productosApi: {
    getAll: vi.fn(),
    delete: vi.fn(),
  },
}));

vi.mock('@/api/categorias', () => ({
  categoriasApi: {
    getAll: vi.fn().mockResolvedValue([
      { id: 1, nombre: 'Bebidas', rutaCompleta: 'Bebidas', activo: true },
    ]),
  },
}));

const makeProducto = (overrides: Partial<ProductoDTO> = {}): ProductoDTO => ({
  id: 'p1',
  codigoBarras: '7701234567890',
  nombre: 'Coca-Cola 350ml',
  descripcion: '',
  categoriaId: 1,
  precioCosto: 2_000,
  precioVenta: 3_500,
  activo: true,
  fechaCreacion: '2026-01-01T00:00:00Z',
  esAlimentoUltraprocesado: false,
  unidadMedida: 'NIU',
  manejaLotes: false,
  ...overrides,
});

describe('ProductosPage', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    const { productosApi } = await import('@/api/productos');
    vi.mocked(productosApi.getAll).mockResolvedValue({
      items: [makeProducto()],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 50,
      totalPages: 1,
    });
  });

  it('muestra el encabezado "Productos"', async () => {
    renderWithProviders(<ProductosPage />);
    // El texto "Productos" aparece en breadcrumb + h5 → varios elementos
    expect((await screen.findAllByText('Productos')).length).toBeGreaterThanOrEqual(1);
  });

  it('muestra el campo de búsqueda', async () => {
    renderWithProviders(<ProductosPage />);
    expect(await screen.findByPlaceholderText(/nombre o código de barras/i)).toBeInTheDocument();
  });

  it('muestra los encabezados de la tabla', async () => {
    renderWithProviders(<ProductosPage />);
    expect(await screen.findByText('Nombre')).toBeInTheDocument();
    expect(screen.getByText('Código de Barras')).toBeInTheDocument();
    // "Categoría" aparece en filtro select Y en cabecera de tabla
    expect(screen.getAllByText('Categoría').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Estado').length).toBeGreaterThanOrEqual(1);
  });

  it('muestra los productos en la tabla', async () => {
    renderWithProviders(<ProductosPage />);
    expect(await screen.findByText('Coca-Cola 350ml')).toBeInTheDocument();
    expect(screen.getByText('7701234567890')).toBeInTheDocument();
  });

  it('muestra el conteo total de productos', async () => {
    renderWithProviders(<ProductosPage />);
    expect(await screen.findByText(/total: 1 producto/i)).toBeInTheDocument();
  });

  it('muestra el botón "Nuevo Producto" para supervisor', async () => {
    renderWithProviders(<ProductosPage />);
    expect(await screen.findByRole('button', { name: /nuevo producto/i })).toBeInTheDocument();
  });

  it('muestra chip "Activo" para productos activos', async () => {
    renderWithProviders(<ProductosPage />);
    expect(await screen.findByText('Activo')).toBeInTheDocument();
  });

  it('muestra "No se encontraron productos" cuando la lista está vacía', async () => {
    const { productosApi } = await import('@/api/productos');
    vi.mocked(productosApi.getAll).mockResolvedValue({
      items: [],
      totalCount: 0,
      pageNumber: 1,
      pageSize: 50,
      totalPages: 0,
    });

    renderWithProviders(<ProductosPage />);
    expect(await screen.findByText(/no se encontraron productos/i)).toBeInTheDocument();
  });
});
