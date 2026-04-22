import { useAuth } from '@/hooks/useAuth';
import { useOfflineSync } from '@/offline/useOfflineSync';
import { useContextualNotification } from '@/hooks/useContextualNotification';
import { useCartStore } from '@/stores/cart.store';
import type { ProductoDTO } from '@/types/api';

export function usePOSStock() {
  const { operacional, sistema } = useContextualNotification();
  const { activeSucursalId, user } = useAuth();
  const { isOnline } = useOfflineSync();
  const { items, addItem, updateQuantity, clearCart } = useCartStore();

  const handleSelectProduct = async (producto: ProductoDTO, voiceQuantity?: number) => {
    if (!activeSucursalId) {
      sistema(`Sin sucursal: ${user?.nombre ?? user?.email}`, 'Asigna una sucursal en Configuración de Usuarios');
      return;
    }

    const qty = voiceQuantity ?? 1;

    if (!isOnline) {
      const cantidadEnCarrito = items.find((i) => i.producto.id === producto.id)?.cantidad ?? 0;
      addItem(producto);
      if (qty > 1) updateQuantity(producto.id, cantidadEnCarrito + qty);
      operacional(`${producto.nombre} agregado — sin verificación de stock (offline)`, `cantidad: ${cantidadEnCarrito + qty}`);
      return;
    }

    try {
      const { inventarioApi } = await import('@/api/inventario');
      const { preciosApi } = await import('@/api/precios');

      const stock = await inventarioApi.getStock({ productoId: producto.id, sucursalId: activeSucursalId });

      if (stock.length === 0 || stock[0].cantidad <= 0) {
        operacional(`Sin stock: ${producto.nombre}`, 'Disponible: 0 unidades');
        return;
      }

      const itemEnCarrito = items.find((item) => item.producto.id === producto.id);
      const cantidadEnCarrito = itemEnCarrito ? itemEnCarrito.cantidad : 0;
      const stockDisponible = stock[0].cantidad;
      const cantidadFinal = cantidadEnCarrito + qty;

      if (cantidadFinal > stockDisponible) {
        operacional('Stock insuficiente', `Disponible: ${stockDisponible} — en carrito: ${cantidadEnCarrito} — solicitado: ${qty}`);
        return;
      }

      let precioSucursal: number | undefined;
      try {
        const precioResuelto = await preciosApi.resolver(producto.id, activeSucursalId);
        if (precioResuelto && precioResuelto.precioVenta > 0) precioSucursal = precioResuelto.precioVenta;
      } catch (error) {
        console.warn('No se pudo resolver precio de sucursal, usando precio base:', error);
      }

      addItem(producto, precioSucursal);
      if (qty > 1) updateQuantity(producto.id, cantidadEnCarrito + qty);
    } catch (error) {
      console.error('Error al verificar stock:', error);
      sistema('Error al verificar stock del producto');
    }
  };

  const handleUpdateQuantity = async (productoId: string, nuevaCantidad: number) => {
    if (!activeSucursalId) {
      sistema(`Sin sucursal: ${user?.nombre ?? user?.email}`, 'Asigna una sucursal en Configuración de Usuarios');
      return;
    }

    const itemActual = items.find((i) => i.producto.id === productoId);
    if (!itemActual || nuevaCantidad < itemActual.cantidad) {
      updateQuantity(productoId, nuevaCantidad);
      return;
    }

    if (!isOnline) {
      updateQuantity(productoId, nuevaCantidad);
      return;
    }

    try {
      const { inventarioApi } = await import('@/api/inventario');
      const stock = await inventarioApi.getStock({ productoId, sucursalId: activeSucursalId });

      if (stock.length === 0 || nuevaCantidad > stock[0].cantidad) {
        operacional('Stock insuficiente', `Disponible: ${stock[0]?.cantidad || 0}`);
        return;
      }

      updateQuantity(productoId, nuevaCantidad);
    } catch (error) {
      console.error('Error al verificar stock:', error);
      sistema('Error al verificar stock');
    }
  };

  const handleClearCart = () => {
    if (items.length === 0) return;
    if (window.confirm('¿Estás seguro de limpiar el carrito?')) clearCart();
  };

  return { handleSelectProduct, handleUpdateQuantity, handleClearCart };
}
