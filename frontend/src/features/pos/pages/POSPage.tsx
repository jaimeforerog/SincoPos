import { useState, useEffect } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Box, Container, Paper, Typography, Alert, Chip, IconButton, Tooltip } from '@mui/material';
import { useContextualNotification } from '@/hooks/useContextualNotification';
import AccountBalanceIcon from '@mui/icons-material/AccountBalance';
import BusinessIcon from '@mui/icons-material/Business';
import PersonIcon from '@mui/icons-material/Person';
import SwapHorizIcon from '@mui/icons-material/SwapHoriz';
import { useAuth } from '@/hooks/useAuth';
import { useAuthStore } from '@/stores/auth.store';
import { HeroBanner } from '@/components/common/HeroBanner';
import { useCartStore } from '@/stores/cart.store';
import { ventasApi } from '@/api/ventas';
import { cajasApi } from '@/api/cajas';
import { useCajasAbiertas } from '../hooks/useCajasAbiertas';
import { IntentSearch } from '../components/IntentSearch';
import { CartPanel } from '../components/CartPanel';
import { VentaConfirmDialog } from '../components/VentaConfirmDialog';
import { SeleccionarCajaDialog } from '../components/SeleccionarCajaDialog';
import { OfflineStatusBanner } from '../components/OfflineStatusBanner';
import { OfflineConflictDialog } from '../components/OfflineConflictDialog';
import { useOfflineSync } from '@/offline/useOfflineSync';
import { useTurnPreload } from '../hooks/useTurnPreload';
import { enqueueVenta } from '@/offline/offlineQueue.service';
import { posSessionCache } from '@/offline/posSessionCache';
import type { ProductoDTO, CrearVentaDTO, VentaDTO } from '@/types/api';

export function POSPage() {
  const { operacional, sistema } = useContextualNotification();
  const { user, isCajero, activeSucursalId, isLoading } = useAuth();
  const { isOnline, pendingCount } = useOfflineSync();

  // Cargar cajas abiertas
  const { data: _cajasAbiertas = [] } = useCajasAbiertas();

  // Estado de caja y cliente
  const [selectedCajaId, setSelectedCajaId] = useState<number | null>(null);
  useTurnPreload(selectedCajaId, activeSucursalId ?? null);

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
    if (selectedCajaId) return;

    // Si solo hay una caja abierta (online o cacheada), seleccionarla automáticamente
    if (_cajasAbiertas.length === 1) {
      handleSelectCaja(_cajasAbiertas[0].id, _cajasAbiertas[0].sucursalId);
      return;
    }

    // Sin caja única: si offline, intentar restaurar la última sesión conocida
    if (!isOnline) {
      const lastSession = posSessionCache.loadSession();
      if (lastSession) {
        setSelectedCajaId(lastSession.cajaId);
        if (!activeSucursalId || activeSucursalId !== lastSession.sucursalId) {
          setActiveSucursal(lastSession.sucursalId);
        }
        operacional(`Offline: usando "${lastSession.cajaNombre}" de la última sesión`);
        return;
      }
    }

    // Mostrar diálogo (con datos cacheados si está offline)
    setShowSeleccionarCaja(true);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isLoading, _cajasAbiertas, selectedCajaId, isOnline]);

  // Handler para seleccionar caja (también recibe la sucursal del diálogo)
  const { setActiveSucursal } = useAuthStore();
  const handleSelectCaja = (cajaId: number, sucursalId: number) => {
    setSelectedCajaId(cajaId);
    // Asegurar que activeSucursalId quede sincronizado con la sucursal de la caja
    if (!activeSucursalId || activeSucursalId !== sucursalId) {
      setActiveSucursal(sucursalId);
    }
    setShowSeleccionarCaja(false);

    // Guardar sesión en cache para restaurar offline en próximas aperturas
    const allCajas = [..._cajasAbiertas, ...posSessionCache.loadCajas()];
    const caja = allCajas.find((c) => c.id === cajaId);
    posSessionCache.saveSession({
      cajaId,
      cajaNombre: caja?.nombre ?? `Caja ${cajaId}`,
      sucursalId,
      sucursalNombre: caja?.nombreSucursal ?? '',
    });

    // Capa 7: informacional — el cambio visual ya confirma la selección
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

  // Estado del diálogo de conflictos offline
  const [showConflictDialog, setShowConflictDialog] = useState(false);

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
      // Capa 7: informacional — VentaConfirmDialog ya comunica el éxito
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
      sistema(mensaje);
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
      sistema(`Sin sucursal: ${user?.nombre ?? user?.email}`, 'Asigna una sucursal en Configuración de Usuarios');
      return;
    }

    // Modo offline: agregar directamente sin consultar API (backend valida en sync)
    if (!isOnline) {
      const cantidadEnCarrito = items.find((i) => i.producto.id === producto.id)?.cantidad ?? 0;
      addItem(producto);
      operacional(`${producto.nombre} agregado — sin verificación de stock (offline)`, `cantidad: ${cantidadEnCarrito + 1}`);
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
        operacional(`Sin stock: ${producto.nombre}`, 'Disponible: 0 unidades');
        return;
      }

      // Verificar si ya está en el carrito
      const itemEnCarrito = items.find((item) => item.producto.id === producto.id);
      const cantidadEnCarrito = itemEnCarrito ? itemEnCarrito.cantidad : 0;
      const stockDisponible = stock[0].cantidad;

      if (cantidadEnCarrito >= stockDisponible) {
        operacional('Stock insuficiente', `Disponible: ${stockDisponible} — en carrito: ${cantidadEnCarrito}`);
        return;
      }

      // Resolver precio de la sucursal
      let precioSucursal: number | undefined = undefined;
      try {
        const precioResuelto = await preciosApi.resolver(producto.id, activeSucursalId);
        if (precioResuelto && precioResuelto.precioVenta > 0) {
          precioSucursal = precioResuelto.precioVenta;
        }
      } catch (error) {
        console.warn('No se pudo resolver precio de sucursal, usando precio base:', error);
      }

      addItem(producto, precioSucursal);

      const precioFinal = precioSucursal !== undefined ? precioSucursal : producto.precioVenta;
      const origenPrecio = precioSucursal !== undefined ? '(Precio Sucursal)' : '(Precio Base)';
      // Capa 7: informacional — el ítem aparece en el carrito de forma inmediata
    } catch (error) {
      console.error('Error al verificar stock:', error);
      sistema('Error al verificar stock del producto');
    }
  };

  // Handler para actualizar cantidad con validación de stock
  const handleUpdateQuantity = async (productoId: string, nuevaCantidad: number) => {
    if (!activeSucursalId) {
      sistema(`Sin sucursal: ${user?.nombre ?? user?.email}`, 'Asigna una sucursal en Configuración de Usuarios');
      return;
    }

    // Si está disminuyendo, permitir siempre
    const itemActual = items.find((i) => i.producto.id === productoId);
    if (!itemActual || nuevaCantidad < itemActual.cantidad) {
      updateQuantity(productoId, nuevaCantidad);
      return;
    }

    // Modo offline: permitir aumentar sin validar (backend valida en sync)
    if (!isOnline) {
      updateQuantity(productoId, nuevaCantidad);
      return;
    }

    // Si está aumentando online, validar stock
    try {
      const { inventarioApi } = await import('@/api/inventario');
      const stock = await inventarioApi.getStock({
        productoId,
        sucursalId: activeSucursalId,
      });

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

  // Handler para limpiar carrito
  const handleClearCart = () => {
    if (items.length === 0) return;

    if (window.confirm('¿Estás seguro de limpiar el carrito?')) {
      clearCart();
      // Capa 7: informacional — el carrito vacío es visualmente evidente
    }
  };

  // Handler para cobrar
  const handleCobrar = async () => {
    // Validaciones
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

    if (!activeSucursalId) {
      sistema(`Sin sucursal: ${user?.nombre ?? user?.email}`, 'Asigna una sucursal en Configuración de Usuarios');
      return;
    }

    const totalVentaValidacion = getTotal();

    // Validar monto pagado solo para efectivo
    if (metodoPago === 0 && montoPagado < totalVentaValidacion) {
      operacional('Monto insuficiente', `Pagado: $${montoPagado.toLocaleString()} / Total: $${totalVentaValidacion.toLocaleString()}`);
      return;
    }

    // Validar precios
    const preciosInvalidos = items.filter((item) => item.precioUnitario <= 0);
    if (preciosInvalidos.length > 0) {
      sistema('Hay productos con precio inválido');
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

      sistema(`Venta por debajo del costo: ${detalles}`);
      return;
    }

    // Validar descuentos
    const descuentosInvalidos = items.filter(
      (item) => item.descuentoPorcentaje < 0 || item.descuentoPorcentaje > 100
    );
    if (descuentosInvalidos.length > 0) {
      operacional('Descuentos inválidos', 'Deben estar entre 0% y 100%');
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

    // ── Modo offline: encolar la venta en IndexedDB ──────────────────────
    if (!isOnline) {
      try {
        const localId = await enqueueVenta(crearVentaDto);
        clearCart();
        setSelectedClienteId(null);
        setMontoPagado(0);
        operacional(`Venta guardada offline (ref: ${localId.slice(-6)})`, 'Se enviará al reconectar');
      } catch {
        sistema('Error al guardar la venta offline');
      }
      return;
    }

    // ── Modo online: enviar al servidor ──────────────────────────────────
    console.log('📤 Enviando venta:', crearVentaDto);
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
    <Box sx={{ display: 'flex', flexDirection: 'column', height: 'calc(100vh - 112px)', overflow: 'hidden', bgcolor: selectedCajaId ? 'grey.100' : 'background.default' }}>
      <Container maxWidth="xl" sx={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
        {/* Banner de estado offline */}
        <OfflineStatusBanner onViewFailed={() => setShowConflictDialog(true)} />

        {/* Banner de Información de Sesión */}
        <HeroBanner
          title="Punto de Venta"
          subtitle="Sesión activa"
          info={
            <>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                <BusinessIcon sx={{ color: 'rgba(255,255,255,0.8)' }} />
                <Box>
                  <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.7)', display: 'block' }}>
                    Sucursal
                  </Typography>
                  <Typography variant="body1" sx={{ fontWeight: 600, color: '#fff' }}>
                    {cajaActual?.nombreSucursal || 'Sin sucursal'}
                  </Typography>
                </Box>
              </Box>

              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                <AccountBalanceIcon sx={{ color: 'rgba(255,255,255,0.8)' }} />
                <Box>
                  <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.7)', display: 'block' }}>
                    Caja
                  </Typography>
                  <Typography variant="body1" sx={{ fontWeight: 600, color: '#fff' }}>
                    {cajaActual ? cajaActual.nombre : 'Sin caja seleccionada'}
                  </Typography>
                </Box>
              </Box>

              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                <PersonIcon sx={{ color: 'rgba(255,255,255,0.8)' }} />
                <Box>
                  <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.7)', display: 'block' }}>
                    Cajero
                  </Typography>
                  <Typography variant="body1" sx={{ fontWeight: 600, color: '#fff' }}>
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
            </>
          }
          actions={
            cajaActual ? (
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
            ) : undefined
          }
        />

        <Box
          sx={{
            display: 'flex',
            gap: 3,
            flex: 1,
            minHeight: 0,
          }}
        >
          {/* Panel Izquierdo - Búsqueda de Productos */}
          <Paper sx={{ flex: '1.1 1 0', p: 3, overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
            <IntentSearch onSelectProduct={handleSelectProduct} />
          </Paper>

          {/* Panel Derecho - Carrito y Pago */}
          <Box sx={{ flex: '1 1 0', minWidth: 0, display: 'flex', flexDirection: 'column' }}>
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
              canCobrar={canCobrar || (!isOnline && items.length > 0 && selectedCajaId !== null)}
              isLoading={crearVentaMutation.isPending}
              isOffline={!isOnline}
            />
          </Box>
        </Box>
      </Container>

      {/* Overlay blanco cuando no hay caja seleccionada */}
      {!selectedCajaId && (
        <Box sx={{ position: 'fixed', inset: 0, bgcolor: 'background.default', zIndex: 1200 }} />
      )}

      {/* Dialog de conflictos offline */}
      <OfflineConflictDialog
        open={showConflictDialog}
        onClose={() => setShowConflictDialog(false)}
      />

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
