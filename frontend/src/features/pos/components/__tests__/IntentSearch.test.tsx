import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, fireEvent, waitFor } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { IntentSearch } from '../IntentSearch';
import type { ProductoDTO, PaginatedResponse } from '@/types/api';

// ── Mocks ──────────────────────────────────────────────────────────────────

const mockGetAll     = vi.fn();
const mockGetStock   = vi.fn();
const mockResolverLote = vi.fn();

vi.mock('@/api/productos', () => ({
  productosApi: { getAll: (...args: unknown[]) => mockGetAll(...args) },
}));

vi.mock('@/api/inventario', () => ({
  inventarioApi: { getStock: (...args: unknown[]) => mockGetStock(...args) },
}));

vi.mock('@/api/precios', () => ({
  preciosApi: { resolverLote: (...args: unknown[]) => mockResolverLote(...args) },
}));

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({ activeSucursalId: 1 }),
}));

// CameraInput tiene dependencia nativa (BarcodeDetector / @zxing) — la mockeamos
vi.mock('../CameraInput', () => ({
  CameraInput: ({ onDetected }: { onDetected: (code: string) => void }) => (
    <button onClick={() => onDetected('123456789')} data-testid="camera-input">
      Cámara
    </button>
  ),
}));

// VoiceInput usa SpeechRecognition que no existe en jsdom
vi.mock('../VoiceInput', () => ({
  VoiceInput: () => null,
}));

// ProductCard es un componente interno — lo mockeamos para simplificar assertions
vi.mock('../ProductCard', () => ({
  ProductCard: ({ producto }: { producto: ProductoDTO }) => (
    <div data-testid="product-card">{producto.nombre}</div>
  ),
}));

// ── Fixtures ───────────────────────────────────────────────────────────────

const makeProducto = (id: string, nombre: string): ProductoDTO => ({
  id,
  codigoBarras: `CB-${id}`,
  nombre,
  categoriaId:  1,
  precioCosto:  500,
  precioVenta:  1000,
  activo:       true,
  fechaCreacion: '2026-01-01T00:00:00Z',
  esAlimentoUltraprocesado: false,
  unidadMedida: '94',
  manejaLotes:  false,
});

const paginatedEmpty: PaginatedResponse<ProductoDTO> = {
  items: [], total: 0, page: 1, pageSize: 50, totalPages: 0,
};

const paginatedConProductos: PaginatedResponse<ProductoDTO> = {
  items: [
    makeProducto('p1', 'Arroz Premium'),
    makeProducto('p2', 'Aceite Vegetal'),
  ],
  total: 2, page: 1, pageSize: 50, totalPages: 1,
};

// ── Tests ──────────────────────────────────────────────────────────────────

describe('IntentSearch', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAll.mockResolvedValue(paginatedEmpty);
    mockGetStock.mockResolvedValue([]);
    mockResolverLote.mockResolvedValue([]);
  });

  it('renderiza el campo de búsqueda con el placeholder correcto', () => {
    renderWithProviders(<IntentSearch onSelectProduct={vi.fn()} />);
    expect(screen.getByPlaceholderText(/Nombre, código/i)).toBeInTheDocument();
  });

  it('renderiza el título "Productos"', () => {
    renderWithProviders(<IntentSearch onSelectProduct={vi.fn()} />);
    expect(screen.getByText('Productos')).toBeInTheDocument();
  });

  it('muestra "Escribe para buscar productos" cuando el campo está vacío y no hay resultados', async () => {
    renderWithProviders(<IntentSearch onSelectProduct={vi.fn()} />);

    await waitFor(() => {
      expect(screen.getByText('Escribe para buscar productos')).toBeInTheDocument();
    });
  });

  it('muestra los productos retornados por la API', async () => {
    mockGetAll.mockResolvedValue(paginatedConProductos);

    renderWithProviders(<IntentSearch onSelectProduct={vi.fn()} />);

    await screen.findByText('Arroz Premium');
    expect(screen.getByText('Aceite Vegetal')).toBeInTheDocument();
    expect(screen.getAllByTestId('product-card')).toHaveLength(2);
  });

  it('muestra "No se encontraron productos" cuando hay búsqueda pero no hay resultados', async () => {
    mockGetAll.mockResolvedValue(paginatedEmpty);

    renderWithProviders(<IntentSearch onSelectProduct={vi.fn()} />);

    const input = screen.getByPlaceholderText(/Nombre, código/i);
    fireEvent.change(input, { target: { value: 'zzz-inexistente' } });

    await waitFor(() => {
      expect(screen.getByText('No se encontraron productos')).toBeInTheDocument();
    });
  });

  it('llama a getAll con la query escrita por el usuario (debounced)', async () => {
    renderWithProviders(<IntentSearch onSelectProduct={vi.fn()} />);

    const input = screen.getByPlaceholderText(/Nombre, código/i);
    fireEvent.change(input, { target: { value: 'arroz' } });

    // El debounce es de 300ms — esperamos la llamada con la query
    await waitFor(() => {
      expect(mockGetAll).toHaveBeenCalledWith(
        expect.objectContaining({ query: 'arroz' })
      );
    }, { timeout: 1000 });
  });

  it('renderiza el botón de CameraInput una vez que la carga termina', async () => {
    renderWithProviders(<IntentSearch onSelectProduct={vi.fn()} />);
    // Mientras isLoading=true el componente muestra CircularProgress en lugar de CameraInput
    // Esperamos a que la query resuelva para que CameraInput aparezca
    await screen.findByTestId('camera-input');
  });

  it('al detectar código por cámara, actualiza el campo de búsqueda', async () => {
    renderWithProviders(<IntentSearch onSelectProduct={vi.fn()} />);

    const cameraBtn = await screen.findByTestId('camera-input');
    fireEvent.click(cameraBtn);

    const input = screen.getByPlaceholderText(/Nombre, código/i) as HTMLInputElement;
    await waitFor(() => {
      expect(input.value).toBe('123456789');
    });
  });

  it('Ctrl+K pone el foco en el campo de búsqueda', () => {
    renderWithProviders(<IntentSearch onSelectProduct={vi.fn()} />);

    const input = screen.getByPlaceholderText(/Nombre, código/i);
    input.blur();

    fireEvent.keyDown(window, { key: 'k', ctrlKey: true });

    // El foco debe volver al input
    expect(document.activeElement).toBe(input);
  });

  it('llama a getStock con la sucursal activa', async () => {
    renderWithProviders(<IntentSearch onSelectProduct={vi.fn()} />);

    await waitFor(() => {
      expect(mockGetStock).toHaveBeenCalledWith({ sucursalId: 1 });
    });
  });

  it('llama a resolverLote con la sucursal activa', async () => {
    renderWithProviders(<IntentSearch onSelectProduct={vi.fn()} />);

    await waitFor(() => {
      expect(mockResolverLote).toHaveBeenCalledWith(1);
    });
  });

  it('pasa el callback onSelectProduct a ProductCard (integración)', async () => {
    const onSelect = vi.fn();
    mockGetAll.mockResolvedValue(paginatedConProductos);

    // Re-mock ProductCard para invocar onClick al hacer click
    vi.mock('../ProductCard', () => ({
      ProductCard: ({ producto, onClick }: { producto: ProductoDTO; onClick: (p: ProductoDTO) => void }) => (
        <div data-testid="product-card" onClick={() => onClick(producto)}>
          {producto.nombre}
        </div>
      ),
    }));

    renderWithProviders(<IntentSearch onSelectProduct={onSelect} />);

    await screen.findByText('Arroz Premium');
    fireEvent.click(screen.getByText('Arroz Premium'));
    expect(onSelect).toHaveBeenCalledWith(expect.objectContaining({ nombre: 'Arroz Premium' }), undefined);
  });
});
