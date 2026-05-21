import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/test/test-utils';
import { ProductoFormDialog } from '../components/ProductoFormDialog';
import type {
  ProductoDTO,
  CrearProductoDTO,
  ActualizarProductoDTO,
} from '@/types/api';

// El Switch de MUI dentro de FormControlLabel no expone su accessible name
// como el texto del primer span (porque el label es un Box con descripción).
// Helper: localiza el <input type=checkbox> a partir de cualquier texto del label.
function getSwitchByLabelText(re: RegExp): HTMLInputElement {
  const labelTextNode = screen.getByText(re);
  const labelEl = labelTextNode.closest('label');
  if (!labelEl) throw new Error(`No se encontró <label> que envuelva "${re}"`);
  const input = labelEl.querySelector('input[type="checkbox"]');
  if (!input) throw new Error(`No se encontró checkbox dentro del label "${re}"`);
  return input as HTMLInputElement;
}

// ── Mocks de APIs ─────────────────────────────────────────────────────────────
vi.mock('@/api/productos', () => ({
  productosApi: {
    create: vi.fn(),
    update: vi.fn(),
  },
}));

vi.mock('@/api/categorias', () => ({
  categoriasApi: {
    getAll: vi.fn().mockResolvedValue([
      {
        id: 1,
        nombre: 'Bebidas',
        rutaCompleta: 'Bebidas',
        activa: true,
        nivel: 0,
        cantidadSubCategorias: 0,
        cantidadProductos: 0,
        margenGanancia: 0,
        origenDatos: 'manual',
      },
    ]),
  },
}));

vi.mock('@/api/impuestos', () => ({
  impuestosApi: {
    getAll: vi.fn().mockResolvedValue([
      {
        id: 1,
        nombre: 'IVA 19%',
        tipo: 'IVA',
        porcentaje: 0.19,
        aplicaSobreBase: true,
        codigoPais: 'CO',
      },
    ]),
  },
  conceptosRetencionApi: {
    getAll: vi.fn().mockResolvedValue([
      { id: 5, nombre: 'Compras generales', codigoDian: '2307', activo: true },
    ]),
  },
}));

// El form lee `activeEmpresaId` del store; basta con un valor estable.
vi.mock('@/stores/auth.store', () => ({
  useAuthStore: (selector: (s: { activeEmpresaId: number }) => unknown) =>
    selector({ activeEmpresaId: 1 }),
}));

// ── Helpers ───────────────────────────────────────────────────────────────────
const makeProducto = (overrides: Partial<ProductoDTO> = {}): ProductoDTO => ({
  id: 'p1',
  codigoBarras: '7701234567890',
  nombre: 'Coca-Cola 350ml',
  descripcion: 'Bebida azucarada',
  categoriaId: 1,
  precioCosto: 2_000,
  precioVenta: 3_500,
  activo: true,
  fechaCreacion: '2026-01-01T00:00:00Z',
  esAlimentoUltraprocesado: false,
  unidadMedida: '94',
  manejaLotes: false,
  ...overrides,
});

const defaultProps = {
  open: true,
  producto: null,
  onClose: vi.fn(),
  onSuccess: vi.fn(),
};

describe('ProductoFormDialog — Impuesto Saludable', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renderiza la sección "Impuesto Saludable (Ley 2277/2022)"', async () => {
    renderWithProviders(<ProductoFormDialog {...defaultProps} />);

    expect(
      await screen.findByText(/Impuesto Saludable \(Ley 2277\/2022\)/i)
    ).toBeInTheDocument();
    expect(screen.getByText(/Es alimento ultraprocesado/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Azúcar \(g \/ 100 ml\)/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Volumen por unidad \(ml\)/i)).toBeInTheDocument();
  });

  it('en modo edit, pre-llena los 3 controles con los valores del producto', async () => {
    const producto = makeProducto({
      esAlimentoUltraprocesado: true,
      gramosAzucarPor100ml: 8.5,
      cantidadMlPorUnidad: 500,
    });

    renderWithProviders(
      <ProductoFormDialog {...defaultProps} producto={producto} />
    );

    // Switch ultraprocesado → checked
    await screen.findByText(/Impuesto Saludable \(Ley 2277\/2022\)/i);
    const switchUltra = getSwitchByLabelText(/Es alimento ultraprocesado/i);
    expect(switchUltra).toBeChecked();

    // Inputs numéricos pre-llenados
    const azucarInput = screen.getByLabelText(/Azúcar \(g \/ 100 ml\)/i) as HTMLInputElement;
    const volumenInput = screen.getByLabelText(/Volumen por unidad \(ml\)/i) as HTMLInputElement;
    expect(azucarInput.value).toBe('8.5');
    expect(volumenInput.value).toBe('500');
  });

  it('al crear con los 3 campos llenos, envía los valores en el payload', async () => {
    const user = userEvent.setup();
    const { productosApi } = await import('@/api/productos');
    vi.mocked(productosApi.create).mockResolvedValue(makeProducto());

    renderWithProviders(<ProductoFormDialog {...defaultProps} />);

    // Esperar que carguen las categorías (queries enabled)
    await screen.findByText(/Impuesto Saludable \(Ley 2277\/2022\)/i);

    // Llenar campos requeridos
    await user.type(screen.getByLabelText(/Código de Barras \*/i), '7700001');
    await user.type(screen.getByLabelText(/Nombre \*/i), 'Gaseosa Premium');
    // Precio costo (input numérico)
    const precio = screen.getByLabelText(/Precio Costo \*/i);
    await user.clear(precio);
    await user.type(precio, '1500');

    // Seleccionar categoría: open MUI Select via combobox y click
    const categoriaCombo = screen.getByRole('combobox', { name: /Categoría \*/i });
    await user.click(categoriaCombo);
    const opcionBebidas = await screen.findByRole('option', { name: 'Bebidas' });
    await user.click(opcionBebidas);

    // Activar Switch ultraprocesado
    const switchUltra = getSwitchByLabelText(/Es alimento ultraprocesado/i);
    await user.click(switchUltra);
    expect(switchUltra).toBeChecked();

    // Llenar gramos + volumen
    await user.type(screen.getByLabelText(/Azúcar \(g \/ 100 ml\)/i), '12');
    await user.type(screen.getByLabelText(/Volumen por unidad \(ml\)/i), '350');

    // Submit
    await user.click(screen.getByRole('button', { name: /^Crear$/i }));

    await waitFor(() => {
      expect(productosApi.create).toHaveBeenCalledTimes(1);
    });

    const payload = vi.mocked(productosApi.create).mock.calls[0][0] as CrearProductoDTO;
    expect(payload.esAlimentoUltraprocesado).toBe(true);
    expect(payload.gramosAzucarPor100ml).toBe(12);
    expect(payload.cantidadMlPorUnidad).toBe(350);
    expect(payload.codigoBarras).toBe('7700001');
    expect(payload.nombre).toBe('Gaseosa Premium');
  });

  it('al crear sin tocar Imp. Saludable, envía flag en false y ml/azúcar undefined', async () => {
    const user = userEvent.setup();
    const { productosApi } = await import('@/api/productos');
    vi.mocked(productosApi.create).mockResolvedValue(makeProducto());

    renderWithProviders(<ProductoFormDialog {...defaultProps} />);

    await screen.findByText(/Impuesto Saludable \(Ley 2277\/2022\)/i);

    await user.type(screen.getByLabelText(/Código de Barras \*/i), '7700099');
    await user.type(screen.getByLabelText(/Nombre \*/i), 'Producto Simple');
    const precio = screen.getByLabelText(/Precio Costo \*/i);
    await user.clear(precio);
    await user.type(precio, '500');

    const categoriaCombo = screen.getByRole('combobox', { name: /Categoría \*/i });
    await user.click(categoriaCombo);
    const opcionBebidas = await screen.findByRole('option', { name: 'Bebidas' });
    await user.click(opcionBebidas);

    await user.click(screen.getByRole('button', { name: /^Crear$/i }));

    await waitFor(() => {
      expect(productosApi.create).toHaveBeenCalledTimes(1);
    });

    const payload = vi.mocked(productosApi.create).mock.calls[0][0] as CrearProductoDTO;
    expect(payload.esAlimentoUltraprocesado).toBe(false);
    expect(payload.gramosAzucarPor100ml).toBeUndefined();
    expect(payload.cantidadMlPorUnidad).toBeUndefined();
  });

  it('al actualizar, propaga los valores de Imp. Saludable modificados al payload', async () => {
    const user = userEvent.setup();
    const { productosApi } = await import('@/api/productos');
    vi.mocked(productosApi.update).mockResolvedValue(makeProducto());

    const producto = makeProducto({
      esAlimentoUltraprocesado: true,
      gramosAzucarPor100ml: 5,
      cantidadMlPorUnidad: 250,
    });

    renderWithProviders(
      <ProductoFormDialog {...defaultProps} producto={producto} />
    );

    // Esperar prefill
    const azucarInput = (await screen.findByLabelText(
      /Azúcar \(g \/ 100 ml\)/i
    )) as HTMLInputElement;
    await waitFor(() => expect(azucarInput.value).toBe('5'));

    // Cambiar gramos a 9
    await user.clear(azucarInput);
    await user.type(azucarInput, '9');

    // Submit (modo edit → botón "Actualizar")
    await user.click(screen.getByRole('button', { name: /^Actualizar$/i }));

    await waitFor(() => {
      expect(productosApi.update).toHaveBeenCalledTimes(1);
    });

    const [id, payload] = vi.mocked(productosApi.update).mock.calls[0] as [
      string,
      ActualizarProductoDTO,
    ];
    expect(id).toBe('p1');
    expect(payload.esAlimentoUltraprocesado).toBe(true);
    expect(payload.gramosAzucarPor100ml).toBe(9);
    expect(payload.cantidadMlPorUnidad).toBe(250);
  });
});

