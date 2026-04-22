import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { CategoriasPage } from '../pages/CategoriasPage';
import type { CategoriaArbolDTO } from '@/types/api';

vi.mock('@/api/categorias', () => ({
  categoriasApi: {
    getArbol: vi.fn(),
    getAll: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    delete: vi.fn(),
    mover: vi.fn(),
  },
}));

// CategoriaTreeView puede ser pesada; el foco está en la página, no en el árbol
vi.mock('../components/CategoriaTreeView', () => ({
  CategoriaTreeView: ({ categorias }: { categorias: unknown[] }) => (
    <div data-testid="categoria-tree">{categorias.length} categorías</div>
  ),
}));

vi.mock('../components/CategoriaFormDialog', () => ({
  CategoriaFormDialog: () => null,
}));

vi.mock('../components/MoverCategoriaDialog', () => ({
  MoverCategoriaDialog: () => null,
}));

const makeCategoriaArbol = (overrides: Partial<CategoriaArbolDTO> = {}): CategoriaArbolDTO => ({
  id: 1,
  nombre: 'Bebidas',
  rutaCompleta: 'Bebidas',
  margenGanancia: 30,
  activa: true,
  nivel: 0,
  cantidadProductos: 0,
  subCategorias: [],
  ...overrides,
});

describe('CategoriasPage', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    const { categoriasApi } = await import('@/api/categorias');
    vi.mocked(categoriasApi.getArbol).mockResolvedValue([makeCategoriaArbol()]);
  });

  it('muestra el encabezado "Categorías"', async () => {
    renderWithProviders(<CategoriasPage />);
    expect((await screen.findAllByText('Categorías')).length).toBeGreaterThanOrEqual(1);
  });

  it('muestra el botón "Nueva Categoría"', async () => {
    renderWithProviders(<CategoriasPage />);
    expect(await screen.findByRole('button', { name: /nueva categoría/i })).toBeInTheDocument();
  });

  it('muestra el switch "Incluir inactivas"', async () => {
    renderWithProviders(<CategoriasPage />);
    expect(await screen.findByLabelText(/incluir.*inactivas/i)).toBeInTheDocument();
  });

  it('muestra el árbol de categorías', async () => {
    renderWithProviders(<CategoriasPage />);
    expect(await screen.findByTestId('categoria-tree')).toBeInTheDocument();
    expect(screen.getByText('1 categorías')).toBeInTheDocument();
  });

  it('muestra 0 categorías cuando la API retorna lista vacía', async () => {
    const { categoriasApi } = await import('@/api/categorias');
    vi.mocked(categoriasApi.getArbol).mockResolvedValue([]);
    renderWithProviders(<CategoriasPage />);
    expect(await screen.findByText('0 categorías')).toBeInTheDocument();
  });
});
