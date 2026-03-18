import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { ProductoDTO } from '@/types/api';

export interface CartItem {
  producto: ProductoDTO;
  cantidad: number;
  precioUnitario: number;
  precioEditable: boolean; // true si viene de precio base, false si viene de precio_sucursal
  descuentoPorcentaje: number;
  impuestoPorcentaje: number;
}

interface CartState {
  items: CartItem[];
  addItem: (producto: ProductoDTO, precioSucursal?: number) => void;
  removeItem: (productoId: string) => void;
  updateQuantity: (productoId: string, cantidad: number) => void;
  updatePrice: (productoId: string, precio: number) => void;
  updateDiscount: (productoId: string, descuento: number) => void;
  clearCart: () => void;
  getSubtotal: () => number;
  getTotalImpuestos: () => number;
  getTotalDescuentos: () => number;
  getTotal: () => number;
}

export const useCartStore = create<CartState>()(
  persist(
    (set, get) => ({
  items: [],

  addItem: (producto, precioSucursal) => {
    const items = get().items;
    const existingItem = items.find((item) => item.producto.id === producto.id);

    if (existingItem) {
      set({
        items: items.map((item) =>
          item.producto.id === producto.id
            ? { ...item, cantidad: item.cantidad + 1 }
            : item
        ),
      });
    } else {
      // Si hay precio de sucursal, usarlo y marcarlo como no editable
      const precioFinal = precioSucursal !== undefined ? precioSucursal : producto.precioVenta;
      const editable = precioSucursal === undefined;

      set({
        items: [
          ...items,
          {
            producto,
            cantidad: 1,
            precioUnitario: precioFinal,
            precioEditable: editable,
            descuentoPorcentaje: 0,
            // Usar el % del impuesto del producto para estimación en carrito.
            // El cálculo definitivo lo hace el backend via TaxEngine.
            impuestoPorcentaje: (producto.porcentajeImpuesto ?? 0) * 100, // convertir 0.19 → 19
          },
        ],
      });
    }
  },

  removeItem: (productoId) => {
    set({
      items: get().items.filter((item) => item.producto.id !== productoId),
    });
  },

  updateQuantity: (productoId, cantidad) => {
    if (cantidad <= 0) {
      get().removeItem(productoId);
      return;
    }
    set({
      items: get().items.map((item) =>
        item.producto.id === productoId ? { ...item, cantidad } : item
      ),
    });
  },

  updatePrice: (productoId, precio) => {
    set({
      items: get().items.map((item) =>
        item.producto.id === productoId
          ? { ...item, precioUnitario: precio }
          : item
      ),
    });
  },

  updateDiscount: (productoId, descuento) => {
    set({
      items: get().items.map((item) =>
        item.producto.id === productoId
          ? { ...item, descuentoPorcentaje: descuento }
          : item
      ),
    });
  },

  clearCart: () => {
    set({ items: [] });
  },

  getSubtotal: () => {
    return get().items.reduce((total, item) => {
      return total + item.precioUnitario * item.cantidad;
    }, 0);
  },

  getTotalDescuentos: () => {
    return get().items.reduce((total, item) => {
      const subtotal = item.precioUnitario * item.cantidad;
      const descuento = (subtotal * item.descuentoPorcentaje) / 100;
      return total + descuento;
    }, 0);
  },

  getTotalImpuestos: () => {
    return get().items.reduce((total, item) => {
      const subtotal = item.precioUnitario * item.cantidad;
      const descuento = (subtotal * item.descuentoPorcentaje) / 100;
      const baseImponible = subtotal - descuento;
      const impuesto = (baseImponible * item.impuestoPorcentaje) / 100;
      return total + impuesto;
    }, 0);
  },

  getTotal: () => {
    const subtotal = get().getSubtotal();
    const descuentos = get().getTotalDescuentos();
    const impuestos = get().getTotalImpuestos();
    return subtotal - descuentos + impuestos;
  },
    }),
    {
      name: 'pos-cart',
      // Solo persistir los items; las funciones se reconstruyen en cada montaje
      partialize: (state) => ({ items: state.items }),
    }
  )
);
