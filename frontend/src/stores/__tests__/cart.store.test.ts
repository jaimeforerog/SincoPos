import { describe, it, expect, beforeEach } from 'vitest';
import { useCartStore } from '../cart.store';
import type { ProductoDTO } from '@/types/api';

const makeProducto = (overrides: Partial<ProductoDTO> = {}): ProductoDTO => ({
  id: 'p1',
  codigoBarras: '001',
  nombre: 'Coca-Cola',
  precioVenta: 3500,
  precioCosto: 2000,
  porcentajeImpuesto: 0.19,
  activo: true,
  categoriaId: 1,
  esAlimentoUltraprocesado: false,
  unidadMedida: '94',
  fechaCreacion: '2026-01-01T00:00:00Z',
  manejaLotes: false,
  ...overrides,
});

beforeEach(() => {
  useCartStore.setState({ items: [] });
});

describe('cart.store — addItem', () => {
  it('agrega un producto nuevo con precio base', () => {
    const producto = makeProducto();
    useCartStore.getState().addItem(producto);

    const { items } = useCartStore.getState();
    expect(items).toHaveLength(1);
    expect(items[0].precioUnitario).toBe(3500);
    expect(items[0].precioEditable).toBe(true);
    expect(items[0].cantidad).toBe(1);
  });

  it('usa precio de sucursal cuando se proporciona y lo marca como no editable', () => {
    const producto = makeProducto();
    useCartStore.getState().addItem(producto, 3200);

    const { items } = useCartStore.getState();
    expect(items[0].precioUnitario).toBe(3200);
    expect(items[0].precioEditable).toBe(false);
  });

  it('incrementa cantidad si el producto ya existe', () => {
    const producto = makeProducto();
    useCartStore.getState().addItem(producto);
    useCartStore.getState().addItem(producto);

    const { items } = useCartStore.getState();
    expect(items).toHaveLength(1);
    expect(items[0].cantidad).toBe(2);
  });

  it('convierte porcentajeImpuesto de decimal a porcentaje (0.19 → 19)', () => {
    const producto = makeProducto({ porcentajeImpuesto: 0.19 });
    useCartStore.getState().addItem(producto);

    expect(useCartStore.getState().items[0].impuestoPorcentaje).toBe(19);
  });

  it('agrega múltiples productos distintos', () => {
    useCartStore.getState().addItem(makeProducto({ id: 'p1', nombre: 'Producto A' }));
    useCartStore.getState().addItem(makeProducto({ id: 'p2', nombre: 'Producto B' }));

    expect(useCartStore.getState().items).toHaveLength(2);
  });
});

describe('cart.store — removeItem', () => {
  it('elimina un producto del carrito', () => {
    useCartStore.getState().addItem(makeProducto());
    useCartStore.getState().removeItem('p1');

    expect(useCartStore.getState().items).toHaveLength(0);
  });

  it('no falla si el producto no existe', () => {
    useCartStore.getState().removeItem('inexistente');
    expect(useCartStore.getState().items).toHaveLength(0);
  });
});

describe('cart.store — updateQuantity', () => {
  it('actualiza la cantidad de un item', () => {
    useCartStore.getState().addItem(makeProducto());
    useCartStore.getState().updateQuantity('p1', 5);

    expect(useCartStore.getState().items[0].cantidad).toBe(5);
  });

  it('elimina el item cuando cantidad es 0', () => {
    useCartStore.getState().addItem(makeProducto());
    useCartStore.getState().updateQuantity('p1', 0);

    expect(useCartStore.getState().items).toHaveLength(0);
  });

  it('elimina el item cuando cantidad es negativa', () => {
    useCartStore.getState().addItem(makeProducto());
    useCartStore.getState().updateQuantity('p1', -1);

    expect(useCartStore.getState().items).toHaveLength(0);
  });
});

describe('cart.store — updateDiscount', () => {
  it('aplica descuento al item', () => {
    useCartStore.getState().addItem(makeProducto());
    useCartStore.getState().updateDiscount('p1', 10);

    expect(useCartStore.getState().items[0].descuentoPorcentaje).toBe(10);
  });
});

describe('cart.store — clearCart', () => {
  it('vacía todos los items del carrito', () => {
    useCartStore.getState().addItem(makeProducto({ id: 'p1' }));
    useCartStore.getState().addItem(makeProducto({ id: 'p2' }));
    useCartStore.getState().clearCart();

    expect(useCartStore.getState().items).toHaveLength(0);
  });
});

describe('cart.store — cálculos', () => {
  it('getSubtotal: precio × cantidad', () => {
    useCartStore.getState().addItem(makeProducto({ precioVenta: 3500 }));
    useCartStore.getState().updateQuantity('p1', 3);

    expect(useCartStore.getState().getSubtotal()).toBe(10500);
  });

  it('getTotalDescuentos: aplica porcentaje correcto', () => {
    useCartStore.getState().addItem(makeProducto({ precioVenta: 10000 }));
    useCartStore.getState().updateDiscount('p1', 20); // 20%

    expect(useCartStore.getState().getTotalDescuentos()).toBe(2000);
  });

  it('getTotalImpuestos: calcula sobre base imponible (subtotal - descuento)', () => {
    // precio 10000, descuento 0%, IVA 19%
    useCartStore.getState().addItem(makeProducto({ precioVenta: 10000, porcentajeImpuesto: 0.19 }));

    // base = 10000, impuesto = 10000 * 19 / 100 = 1900
    expect(useCartStore.getState().getTotalImpuestos()).toBeCloseTo(1900);
  });

  it('getTotalImpuestos: descuenta antes de calcular impuesto', () => {
    useCartStore.getState().addItem(makeProducto({ precioVenta: 10000, porcentajeImpuesto: 0.19 }));
    useCartStore.getState().updateDiscount('p1', 10); // 10% → base = 9000

    // impuesto = 9000 * 19 / 100 = 1710
    expect(useCartStore.getState().getTotalImpuestos()).toBeCloseTo(1710);
  });

  it('getTotal: subtotal - descuentos + impuestos', () => {
    useCartStore.getState().addItem(makeProducto({ precioVenta: 10000, porcentajeImpuesto: 0.19 }));
    // sin descuento: total = 10000 - 0 + 1900 = 11900
    expect(useCartStore.getState().getTotal()).toBeCloseTo(11900);
  });

  it('carrito vacío retorna 0 en todos los cálculos', () => {
    expect(useCartStore.getState().getSubtotal()).toBe(0);
    expect(useCartStore.getState().getTotalDescuentos()).toBe(0);
    expect(useCartStore.getState().getTotalImpuestos()).toBe(0);
    expect(useCartStore.getState().getTotal()).toBe(0);
  });
});
