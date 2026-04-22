import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/hooks/useAuth';
import { useOfflineSync } from '@/offline/useOfflineSync';
import { useContextualNotification } from '@/hooks/useContextualNotification';
import { useCartStore } from '@/stores/cart.store';
import { orquestadorApi } from '@/api/orquestador';
import { enqueueVenta } from '@/offline/offlineQueue.service';
import type { CrearVentaDTO, VentaDTO, ApiError } from '@/types/api';

interface POSVentaParams {
  selectedCajaId: number | null;
  selectedClienteId: number | null;
  metodoPago: number;
  montoPagado: number;
  fechaVenta: string;
  mostrarFechaVenta: boolean;
  diaMaxVentaAtrazada: number;
  onVentaExitosa: () => void;
}

export function usePOSVenta({
  selectedCajaId,
  selectedClienteId,
  metodoPago,
  montoPagado,
  fechaVenta,
  mostrarFechaVenta,
  diaMaxVentaAtrazada,
  onVentaExitosa,
}: POSVentaParams) {
  const queryClient = useQueryClient();
  const { operacional, sistema } = useContextualNotification();
  const { isCajero, activeSucursalId, user } = useAuth();
  const { isOnline } = useOfflineSync();
  const { items, clearCart, getTotal } = useCartStore();

  const [lastVenta, setLastVenta] = useState<VentaDTO | null>(null);
  const [showConfirmDialog, setShowConfirmDialog] = useState(false);

  const crearVentaMutation = useMutation({
    mutationFn: (data: CrearVentaDTO) => orquestadorApi.procesarVenta(data),
    onSuccess: (result) => {
      const venta = result.venta;
      if (!venta) return;
      setLastVenta(venta);
      setShowConfirmDialog(true);
      clearCart();
      onVentaExitosa();
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
      queryClient.invalidateQueries({ queryKey: ['ventas'] });
      queryClient.invalidateQueries({ queryKey: ['inventario', activeSucursalId] });
    },
    onError: (error: ApiError) => {
      const mensaje =
        error.errors
          ? (Array.isArray(error.errors) ? error.errors.join(', ') : JSON.stringify(error.errors))
          : (error.message || 'Error al procesar la venta');
      sistema(mensaje);
    },
  });

  const handleCobrar = async () => {
    if (!isCajero()) {
      sistema('No tienes permisos de cajero');
      return;
    }
    if (items.length === 0) {
      operacional('El carrito está vacío');
      return;
    }
    if (!selectedCajaId) {
      operacional('Debes seleccionar una caja');
      return;
    }
    if (!selectedClienteId) {
      operacional('El cliente es obligatorio', 'Selecciona un cliente antes de procesar la venta');
      return;
    }
    if (!activeSucursalId) {
      sistema(`Sin sucursal: ${user?.nombre ?? user?.email}`, 'Asigna una sucursal en Configuración de Usuarios');
      return;
    }

    const totalVenta = getTotal();

    if (metodoPago === 0 && montoPagado < totalVenta) {
      operacional('Monto insuficiente', `Pagado: $${montoPagado.toLocaleString()} / Total: $${totalVenta.toLocaleString()}`);
      return;
    }

    if (mostrarFechaVenta) {
      const fechaSeleccionada = new Date(fechaVenta);
      const ahora = new Date();
      const fechaMin = new Date();
      fechaMin.setDate(fechaMin.getDate() - diaMaxVentaAtrazada);

      if (fechaSeleccionada > ahora) {
        operacional('La fecha de venta no puede ser futura');
        return;
      }
      if (fechaSeleccionada < fechaMin) {
        operacional('Fecha fuera de rango', `El máximo permitido es ${diaMaxVentaAtrazada} día(s) atrás`);
        return;
      }
    }

    const preciosInvalidos = items.filter((item) => item.precioUnitario <= 0);
    if (preciosInvalidos.length > 0) {
      sistema('Hay productos con precio inválido');
      return;
    }

    const preciosMenoresAlCosto = items.filter((item) => item.precioUnitario < item.producto.precioCosto);
    if (preciosMenoresAlCosto.length > 0) {
      const detalles = preciosMenoresAlCosto
        .map((item) => `${item.producto.nombre}: Precio $${item.precioUnitario.toLocaleString()} < Costo $${item.producto.precioCosto.toLocaleString()}`)
        .join(', ');
      sistema(`Venta por debajo del costo: ${detalles}`);
      return;
    }

    const descuentosInvalidos = items.filter((item) => item.descuentoPorcentaje < 0 || item.descuentoPorcentaje > 100);
    if (descuentosInvalidos.length > 0) {
      operacional('Descuentos inválidos', 'Deben estar entre 0% y 100%');
      return;
    }

    const crearVentaDto: CrearVentaDTO = {
      sucursalId: activeSucursalId!,
      cajaId: selectedCajaId!,
      clienteId: selectedClienteId || undefined,
      metodoPago,
      montoPagado: metodoPago === 0 ? montoPagado : totalVenta,
      observaciones: undefined,
      fechaVenta: mostrarFechaVenta ? new Date(fechaVenta).toISOString() : undefined,
      lineas: items.map((item) => {
        const subtotal = item.precioUnitario * item.cantidad;
        const descuentoValor = (subtotal * item.descuentoPorcentaje) / 100;
        return {
          productoId: item.producto.id,
          cantidad: item.cantidad,
          precioUnitario: item.precioUnitario,
          descuento: descuentoValor,
        };
      }),
    };

    if (!isOnline) {
      try {
        const localId = await enqueueVenta(crearVentaDto);
        clearCart();
        onVentaExitosa();
        operacional(`Venta guardada offline (ref: ${localId.slice(-6)})`, 'Se enviará al reconectar');
      } catch {
        sistema('Error al guardar la venta offline');
      }
      return;
    }

    try {
      await crearVentaMutation.mutateAsync(crearVentaDto);
    } catch {
      // handled by onError
    }
  };

  return {
    crearVentaMutation,
    lastVenta,
    showConfirmDialog,
    setShowConfirmDialog,
    handleCobrar,
  };
}
