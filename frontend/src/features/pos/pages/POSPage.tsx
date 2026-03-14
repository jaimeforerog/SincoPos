import { useState, useEffect } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Box, Container, Paper, Typography, Alert, Chip, IconButton, Tooltip } from '@mui/material';
import { useSnackbar } from 'notistack';
import AccountBalanceIcon from '@mui/icons-material/AccountBalance';
import BusinessIcon from '@mui/icons-material/Business';
import PersonIcon from '@mui/icons-material/Person';
import SwapHorizIcon from '@mui/icons-material/SwapHoriz';
import { useAuth } from '@/hooks/useAuth';
import { useAuthStore } from '@/stores/auth.store';
import { useCartStore } from '@/stores/cart.store';
import { ventasApi } from '@/api/ventas';
import { cajasApi } from '@/api/cajas';
import { useCajasAbiertas } from '../hooks/useCajasAbiertas';
import { ProductSearch } from '../components/ProductSearch';
import { CartPanel } from '../components/CartPanel';
import { VentaConfirmDialog } from '../components/VentaConfirmDialog';
import { SeleccionarCajaDialog } from '../components/SeleccionarCajaDialog';
import type { ProductoDTO, CrearVentaDTO, VentaDTO } from '@/types/api';

export function POSPage() {
  const { enqueueSnackbar } = useSnackbar();
  const { user, isCajero, activeSucursalId, isLoading } = useAuth();

  // Cargar cajas abiertas
  const { data: _cajasAbiertas = [] } = useCajasAbiertas();

  // Estado de caja y cliente
  const [selectedCajaId, setSelectedCajaId] = useState<number | null>(null);

  // Cargar detalles de la caja seleccionada
  const { data: cajaActual } = useQuery({
    queryKey: ['caja', selectedCajaId],
    queryFn: () => cajasApi.getById(selectedCajaId!),
    enabled: selectedCajaId !== null,
  });
  const [selectedClienteId, setSelectedClienteId] = useState<number | null>(null);
  const [showSeleccionarCaja, setShowSeleccionarCaja] = useState(false);

  // Mostrar diálogo de selección de caja o auto-seleccionar si solo hay una
  useEffect(() => {
    // Esperar a que el perfil del usuario y las cajas estén cargados
    if (isLoading) return;

    // Si solo hay una caja abierta, seleccionarla automáticamente
    if (_cajasAbiertas.length === 1 && !selectedCajaId) {
      handleSelectCaja(_cajasAbiertas[0].id, _cajasAbiertas[0].sucursalId);
    } else if (!selectedCajaId && _cajasAbiertas.length !== 1) {
      // Si no hay cajas o hay múltiples, mostrar el diálogo
      setShowSeleccionarCaja(true);
    }
  }, [isLoading, _cajasAbiertas, selectedCajaId]);

  // Handler para seleccionar caja (también recibe la sucursal del diálogo)
  const { setActiveSucursal } = useAuthStore();
  const handleSelectCaja = (cajaId: number, sucursalId: number) => {
    setSelectedCajaId(cajaId);
    // Asegurar que activeSucursalId quede sincronizado con la sucursal de la caja
    if (!activeSucursalId || activeSucursalId !== sucursalId) {
      setActiveSucursal(sucursalId);
    }
    setShowSeleccionarCaja(false);
    enqueueSnackbar('Caja seleccionada correctamente', { variant: 'success' });
  };

  // Handler para cambiar de caja
  const handleCambiarCaja = () => {
    if (items.length > 0) {
      if (!window.confirm('¿Estás seguro? Se perderá el carrito actual')) {
        return;
      }
      clearCart();
    }
    setShowSeleccionarCaja(true);
  };

  // Estado de pago
  const [metodoPago, setMetodoPago] = useState<number>(0); // 0=Efectivo
  const [montoPagado, setMontoPagado] = useState<number>(0);

  // Estado de confirmación
  const [showConfirmDialog, setShowConfirmDialog] = useState(false);
  const [lastVenta, setLastVenta] = useState<VentaDTO | null>(null);

  // Cart store
  const {
    items,
    addItem,
    removeItem,
    updateQuantity,
    updatePrice,
    updateDiscount,
    clearCart,
    getSubtotal,
    getTotalDescuentos,
    getTotalImpuestos,
    getTotal,
  } = useCartStore();

  // Mutación para crear venta
  const crearVentaMutation = useMutation({
    mutationFn: (data: CrearVentaDTO) => ventasApi.create(data),
    onSuccess: (venta) => {
      console.log('✅ Venta creada exitosamente:', venta);
      setLastVenta(venta);
      setShowConfirmDialog(true);
      clearCart();
      setSelectedClienteId(null);
      setMontoPagado(0);
      enqueueSnackbar('Venta completada exitosamente', { variant: 'success' });
    },
    onError: (error: any) => {
      console.error('❌ Error al crear venta:', error);
      console.error('Detalles:', {
        message: error.message,
        errors: error.errors,
        statusCode: error.statusCode,
        response: error.response,
      });
      const mensaje =
        error.errors
          ? (Array.isArray(error.errors) ? error.errors.join(', ') : JSON.stringify(error.errors))
          : (error.message || 'Error al procesar la venta');
      enqueueSnackbar(mensaje, { variant: 'error' });
    },
  });

  // Verificar rol
  if (!isCajero()) {
    return (
      <Container>
        <Alert severity="error" sx={{ mt: 4 }}>
          No tienes permisos para acceder al punto de venta. Se requiere el rol de Cajero.
        </Alert>
      </Container>
    );
  }

  // Handler para agregar producto con validación de stock y precio de sucursal
  const handleSelectProduct = async (producto: ProductoDTO) => {
    if (!activeSucursalId) {
      enqueueSnackbar(`El usuario ${user?.nombre ?? user?.email} no tiene una sucursal asignada`, { variant: 'error' });
      return;
    }

    try {
      const { inventarioApi } = await import('@/api/inventario');
      const { preciosApi } = await import('@/api/precios');

      // Consultar stock disponible
      const stock = await inventarioApi.getStock({
        productoId: producto.id,
        sucursalId: activeSucursalId,
      });

      if (stock.length === 0 || stock[0].cantidad <= 0) {
        enqueueSnackbar(`${producto.nombre} no tiene stock disponible`, { variant: 'warning' });
        return;
      }

      // Verificar si ya está en el carrito
      const itemEnCarrito = items.find((item) => item.producto.id === producto.id);
      const cantidadEnCarrito = itemEnCarrito ? itemEnCarrito.cantidad : 0;
      const stockDisponible = stock[0].cantidad;

      if (cantidadEnCarrito >= stockDisponible) {
        enqueueSnackbar(
          `Stock insuficiente. Disponible: ${stockDisponible}, en carrito: ${cantidadEnCarrito}`,
          { variant: 'warning' }
        );
        return;
      }

      // Resolver precio de la sucursal
      let precioSucursal: number | undefined = undefined;
      try {
        const precioResuelto = await preciosApi.resolver(producto.id, activeSucursalId);

        if (precioResuelto && precioResuelto.precioVenta > 0) {
          precioSucursal = precioResuelto.precioVenta;
          console.log(`✅ Precio resuelto para ${producto.nombre}:`, {
            base: producto.precioVenta,
            sucursal: precioSucursal,
            origen: precioResuelto.origen,
          });
        }
      } catch (error) {
        console.warn('No se pudo resolver precio de sucursal, usando precio base:', error);
      }

      // Agregar al carrito (con precio de sucursal si existe)
      addItem(producto, precioSucursal);

      const precioFinal = precioSucursal !== undefined ? precioSucursal : producto.precioVenta;
      const origenPrecio = precioSucursal !== undefined ? '(Precio Sucursal)' : '(Precio Base)';

      enqueueSnackbar(
        `${producto.nombre} agregado (${cantidadEnCarrito + 1}/${stockDisponible}) - $${precioFinal.toLocaleString()} ${origenPrecio}`,
        { variant: 'success' }
      );
    } catch (error) {
      console.error('Error al verificar stock:', error);
      enqueueSnackbar('Error al verificar stock del producto', { variant: 'error' });
    }
  };

  // Handler para actualizar cantidad con validación de stock
  const handleUpdateQuantity = async (productoId: string, nuevaCantidad: number) => {
    if (!activeSucursalId) {
      enqueueSnackbar(`El usuario ${user?.nombre ?? user?.email} no tiene una sucursal asignada`, { variant: 'error' });
      return;
    }

    // Si está disminuyendo, permitir siempre
    const itemActual = items.find((i) => i.producto.id === productoId);
    if (!itemActual || nuevaCantidad < itemActual.cantidad) {
      updateQuantity(productoId, nuevaCantidad);
      return;
    }

    // Si está aumentando, validar stock
    try {
      const { inventarioApi } = await import('@/api/inventario');
      const stock = await inventarioApi.getStock({
        productoId,
        sucursalId: activeSucursalId,
      });

      if (stock.length === 0 || nuevaCantidad > stock[0].cantidad) {
        enqueueSnackbar(
          `Stock insuficiente. Disponible: ${stock[0]?.cantidad || 0}`,
          { variant: 'warning' }
        );
        return;
      }

      updateQuantity(productoId, nuevaCantidad);
    } catch (error) {
      console.error('Error al verificar stock:', error);
      enqueueSnackbar('Error al verificar stock', { variant: 'error' });
    }
  };

  // Handler para limpiar carrito
  const handleClearCart = () => {
    if (items.length === 0) return;

    if (window.confirm('¿Estás seguro de limpiar el carrito?')) {
      clearCart();
      enqueueSnackbar('Carrito limpiado', { variant: 'info' });
    }
  };

  // Handler para cobrar
  const handleCobrar = async () => {
    // Validaciones
    if (!isCajero()) {
      enqueueSnackbar('No tienes permisos de cajero', { variant: 'error' });
      return;
    }

    if (items.length === 0) {
      enqueueSnackbar('El carrito está vacío', { variant: 'warning' });
      return;
    }

    if (!selectedCajaId) {
      enqueueSnackbar('Debes seleccionar una caja', { variant: 'warning' });
      return;
    }

    if (!activeSucursalId) {
      enqueueSnackbar(`El usuario ${user?.nombre ?? user?.email} no tiene una sucursal asignada`, { variant: 'error' });
      return;
    }

    const totalVentaValidacion = getTotal();

    // Validar monto pagado solo para efectivo
    if (metodoPago === 0 && montoPagado < totalVentaValidacion) {
      enqueueSnackbar(
        `El monto pagado (${montoPagado}) es insuficiente. Total: ${totalVentaValidacion}`,
        { variant: 'warning' }
      );
      return;
    }

    // Validar precios
    const preciosInvalidos = items.filter((item) => item.precioUnitario <= 0);
    if (preciosInvalidos.length > 0) {
      enqueueSnackbar('Hay productos con precio inválido', { variant: 'error' });
      return;
    }

    // Validar que precio de venta no sea menor al costo
    const preciosMenoresAlCosto = items.filter(
      (item) => item.precioUnitario < item.producto.precioCosto
    );
    if (preciosMenoresAlCosto.length > 0) {
      const detalles = preciosMenoresAlCosto
        .map((item) => {
          const producto = item.producto.nombre;
          const precio = `$${item.precioUnitario.toLocaleString()}`;
          const costo = `$${item.producto.precioCosto.toLocaleString()}`;
          return `${producto}: Precio ${precio} < Costo ${costo}`;
        })
        .join(', ');

      enqueueSnackbar(
        `No se puede vender por debajo del costo. ${detalles}`,
        {
          variant: 'error',
          autoHideDuration: 8000 // Mostrar más tiempo para leer
        }
      );
      return;
    }

    // Validar descuentos
    const descuentosInvalidos = items.filter(
      (item) => item.descuentoPorcentaje < 0 || item.descuentoPorcentaje > 100
    );
    if (descuentosInvalidos.length > 0) {
      enqueueSnackbar('Hay descuentos inválidos (deben estar entre 0% y 100%)', {
        variant: 'error',
      });
      return;
    }

    console.log('🛒 Items en carrito antes de mapear:', items);
    console.log('📊 Total de items:', items.length);

    const totalVenta = getTotal();

    // Construir DTO
    const crearVentaDto: CrearVentaDTO = {
      sucursalId: activeSucursalId!,
      cajaId: selectedCajaId!,
      clienteId: selectedClienteId || undefined,
      metodoPago: metodoPago,
      // Para efectivo: usar montoPagado ingresado, para otros: usar total exacto
      montoPagado: metodoPago === 0 ? montoPagado : totalVenta,
      observaciones: undefined,
      lineas: items.map((item) => {
        // CRÍTICO: Convertir descuentoPorcentaje a valor absoluto
        const subtotal = item.precioUnitario * item.cantidad;
        const descuentoValor = (subtotal * item.descuentoPorcentaje) / 100;

        return {
          productoId: item.producto.id,
          cantidad: item.cantidad,
          precioUnitario: item.precioUnitario,
          descuento: descuentoValor, // Convertido de % a valor absoluto
        };
      }),
    };

    console.log('📤 Enviando venta:', crearVentaDto);

    // Ejecutar mutación
    try {
      await crearVentaMutation.mutateAsync(crearVentaDto);
    } catch (error) {
      // El error ya se maneja en el onError del mutation
      console.error('Error capturado en handleCobrar:', error);
    }
  };

  // Calcular totales
  const subtotal = getSubtotal();
  const totalDescuentos = getTotalDescuentos();
  const totalImpuestos = getTotalImpuestos();
  const total = getTotal();

  // Validar si se puede cobrar
  const canCobrar =
    items.length > 0 &&
    selectedCajaId !== null &&
    (metodoPago !== 0 || montoPagado >= total);

  return (
    <Box sx={{ flexGrow: 1, bgcolor: 'grey.100', minHeight: '100vh', py: 3 }}>
      <Container maxWidth="xl">
        {/* Banner de Información de Sesión */}
        <Box
          sx={{
            background: cajaActual
              ? 'linear-gradient(135deg, #1565c0 0%, #0d47a1 50%, #01579b 100%)'
              : 'linear-gradient(135deg, #e65100 0%, #bf360c 100%)',
            borderRadius: 3,
            p: 2,
            mb: 3,
            position: 'relative',
            overflow: 'hidden',
            '&::before': {
              content: '""', position: 'absolute', top: -40, right: -40,
              width: 140, height: 140, borderRadius: '50%', background: 'rgba(255,255,255,0.06)',
            },
          }}
        >
          <Box sx={{ display: 'flex', gap: 3, alignItems: 'center', flexWrap: 'wrap', justifyContent: 'space-between', position: 'relative', zIndex: 1 }}>
            <Box sx={{ display: 'flex', gap: 3, alignItems: 'center', flexWrap: 'wrap' }}>
              <Box>
                <Typography variant="h6" fontWeight={700} sx={{ color: '#fff', lineHeight: 1.1 }}>
                  Punto de Venta
                </Typography>
                <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.7)' }}>
                  Sesión activa
                </Typography>
              </Box>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                <AccountBalanceIcon />
                <Box>
                  <Typography variant="caption" sx={{ opacity: 0.9, display: 'block' }}>
                    Caja
                  </Typography>
                  <Typography variant="body1" sx={{ fontWeight: 600 }}>
                    {cajaActual ? cajaActual.nombre : 'Sin caja seleccionada'}
                  </Typography>
                </Box>
              </Box>

              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                <BusinessIcon />
                <Box>
                  <Typography variant="caption" sx={{ opacity: 0.9, display: 'block' }}>
                    Sucursal
                  </Typography>
                  <Typography variant="body1" sx={{ fontWeight: 600 }}>
                    {cajaActual?.nombreSucursal || 'Sin sucursal'}
                  </Typography>
                </Box>
              </Box>

              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                <PersonIcon />
                <Box>
                  <Typography variant="caption" sx={{ opacity: 0.9, display: 'block' }}>
                    Cajero
                  </Typography>
                  <Typography variant="body1" sx={{ fontWeight: 600 }}>
                    {user?.nombre}
                  </Typography>
                </Box>
              </Box>

              {!cajaActual && (
                <Chip
                  label="⚠️ Selecciona una caja para comenzar"
                  sx={{ bgcolor: 'white', color: 'warning.main', fontWeight: 600 }}
                />
              )}
            </Box>

            {cajaActual && (
              <Tooltip title="Cambiar de caja">
                <IconButton
                  onClick={handleCambiarCaja}
                  sx={{
                    color: 'white',
                    bgcolor: 'rgba(255,255,255,0.15)',
                    border: '1px solid rgba(255,255,255,0.3)',
                    '&:hover': { bgcolor: 'rgba(255,255,255,0.25)' },
                  }}
                >
                  <SwapHorizIcon />
                </IconButton>
              </Tooltip>
            )}
          </Box>
        </Box>

        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: { xs: '1fr', md: '1.4fr 1fr' },
            gap: 3,
            height: 'calc(100vh - 220px)',
          }}
        >
          {/* Panel Izquierdo - Búsqueda de Productos */}
          <Paper sx={{ p: 3, height: '100%' }}>
            <Typography variant="h6" sx={{ mb: 2, fontWeight: 600 }}>
              Productos
            </Typography>
            <ProductSearch onSelectProduct={handleSelectProduct} />
          </Paper>

          {/* Panel Derecho - Carrito y Pago */}
          <CartPanel
            selectedCajaId={selectedCajaId}
            selectedClienteId={selectedClienteId}
            onCajaChange={setSelectedCajaId}
            onClienteChange={setSelectedClienteId}
            items={items}
            onUpdateQuantity={handleUpdateQuantity}
            onUpdatePrice={updatePrice}
            onUpdateDiscount={updateDiscount}
            onRemoveItem={removeItem}
            subtotal={subtotal}
            totalDescuentos={totalDescuentos}
            totalImpuestos={totalImpuestos}
            total={total}
            metodoPago={metodoPago}
            montoPagado={montoPagado}
            onMetodoPagoChange={setMetodoPago}
            onMontoPagadoChange={setMontoPagado}
            onClear={handleClearCart}
            onCobrar={handleCobrar}
            canCobrar={canCobrar}
            isLoading={crearVentaMutation.isPending}
          />
        </Box>
      </Container>

      {/* Dialog de Selección de Caja */}
      <SeleccionarCajaDialog
        open={showSeleccionarCaja}
        onSelect={handleSelectCaja}
      />

      {/* Dialog de Confirmación */}
      <VentaConfirmDialog
        open={showConfirmDialog}
        venta={lastVenta}
        onClose={() => setShowConfirmDialog(false)}
      />
    </Box>
  );
}
