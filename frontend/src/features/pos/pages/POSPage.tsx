import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Box, Container, Paper, Typography, Alert, Chip, IconButton, Tooltip } from '@mui/material';
import AccountBalanceIcon from '@mui/icons-material/AccountBalance';
import BusinessIcon from '@mui/icons-material/Business';
import StorefrontIcon from '@mui/icons-material/Storefront';
import PersonIcon from '@mui/icons-material/Person';
import SwapHorizIcon from '@mui/icons-material/SwapHoriz';
import CalendarTodayIcon from '@mui/icons-material/CalendarToday';
import { useAuth } from '@/hooks/useAuth';
import { useAuthStore } from '@/stores/auth.store';
import { HeroBanner } from '@/components/common/HeroBanner';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { useCartStore } from '@/stores/cart.store';
import { useOfflineSync } from '@/offline/useOfflineSync';
import { IntentSearch } from '../components/IntentSearch';
import { CartPanel } from '../components/CartPanel';
import { VentaConfirmDialog } from '../components/VentaConfirmDialog';
import { SeleccionarCajaDialog } from '../components/SeleccionarCajaDialog';
import { OfflineStatusBanner } from '../components/OfflineStatusBanner';
import { OfflineConflictDialog } from '../components/OfflineConflictDialog';
import { usePOSSession } from '../hooks/usePOSSession';
import { usePOSStock } from '../hooks/usePOSStock';
import { usePOSVenta } from '../hooks/usePOSVenta';

export function POSPage() {
  const navigate = useNavigate();
  const { user, isCajero, activeSucursalId } = useAuth();
  const { isOnline } = useOfflineSync();
  const { empresasDisponibles, activeEmpresaId } = useAuthStore();

  const {
    selectedCajaId,
    cajaActual,
    fechaVenta,
    mostrarFechaVenta,
    diaMaxVentaAtrazada,
    showSeleccionarCaja,
    handleSelectCaja,
    handleCambiarCaja,
    cambiarCajaConfirmProps,
  } = usePOSSession();

  const { handleSelectProduct, handleUpdateQuantity, handleClearCart, limpiarConfirmProps } = usePOSStock();

  // Estado de pago y cliente (permanece en el page porque conecta session ↔ venta)
  const [selectedClienteId, setSelectedClienteId] = useState<number | null>(null);
  const [metodoPago, setMetodoPago] = useState<number>(0);
  const [montoPagado, setMontoPagado] = useState<number>(0);
  const [showConflictDialog, setShowConflictDialog] = useState(false);

  const {
    crearVentaMutation,
    lastVenta,
    showConfirmDialog,
    setShowConfirmDialog,
    handleCobrar,
  } = usePOSVenta({
    selectedCajaId,
    selectedClienteId,
    metodoPago,
    montoPagado,
    fechaVenta,
    mostrarFechaVenta,
    diaMaxVentaAtrazada,
    onVentaExitosa: () => {
      setSelectedClienteId(null);
      setMontoPagado(0);
    },
  });

  const {
    items,
    removeItem,
    updatePrice,
    updateDiscount,
    getSubtotal,
    getTotalDescuentos,
    getTotalImpuestos,
    getTotal,
  } = useCartStore();

  if (!isCajero()) {
    return (
      <Container>
        <Alert severity="error" sx={{ mt: 4 }}>
          No tienes permisos para acceder al punto de venta. Se requiere el rol de Cajero.
        </Alert>
      </Container>
    );
  }

  const sucursalInfo = user?.sucursalesDisponibles.find(
    (s) => s.id === (cajaActual?.sucursalId ?? activeSucursalId)
  );
  const sucursalNombre = cajaActual?.nombreSucursal ?? sucursalInfo?.nombre ?? 'Sin sucursal';
  const empresaNombre =
    sucursalInfo?.empresaNombre ??
    empresasDisponibles.find((e) => e.id === activeEmpresaId)?.nombre ??
    null;

  const fechaVentaDisplay = (() => {
    if (!mostrarFechaVenta || !fechaVenta) return null;
    try {
      return new Date(fechaVenta).toLocaleString('es-CO', {
        day: '2-digit', month: '2-digit', year: 'numeric',
        hour: '2-digit', minute: '2-digit',
      });
    } catch {
      return fechaVenta;
    }
  })();

  const subtotal = getSubtotal();
  const totalDescuentos = getTotalDescuentos();
  const totalImpuestos = getTotalImpuestos();
  const total = getTotal();

  const canCobrar =
    items.length > 0 &&
    selectedCajaId !== null &&
    (metodoPago !== 0 || montoPagado >= total);

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', height: 'calc(100vh - 112px)', overflow: 'hidden', bgcolor: selectedCajaId ? 'grey.100' : 'background.default' }}>
      <Container maxWidth="xl" sx={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
        <OfflineStatusBanner onViewFailed={() => setShowConflictDialog(true)} />

        <HeroBanner
          title="Punto de Venta"
          subtitle="Sesión activa"
          info={
            <>
              {empresaNombre && (
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                  <BusinessIcon sx={{ color: 'rgba(255,255,255,0.8)' }} />
                  <Box>
                    <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.7)', display: 'block' }}>Empresa</Typography>
                    <Typography variant="body1" sx={{ fontWeight: 600, color: '#fff' }}>{empresaNombre}</Typography>
                  </Box>
                </Box>
              )}
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                <StorefrontIcon sx={{ color: 'rgba(255,255,255,0.8)' }} />
                <Box>
                  <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.7)', display: 'block' }}>Sucursal</Typography>
                  <Typography variant="body1" sx={{ fontWeight: 600, color: '#fff' }}>{sucursalNombre}</Typography>
                </Box>
              </Box>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                <AccountBalanceIcon sx={{ color: 'rgba(255,255,255,0.8)' }} />
                <Box>
                  <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.7)', display: 'block' }}>Caja</Typography>
                  <Typography variant="body1" sx={{ fontWeight: 600, color: '#fff' }}>{cajaActual?.nombre ?? '—'}</Typography>
                </Box>
              </Box>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                <PersonIcon sx={{ color: 'rgba(255,255,255,0.8)' }} />
                <Box>
                  <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.7)', display: 'block' }}>Cajero</Typography>
                  <Typography variant="body1" sx={{ fontWeight: 600, color: '#fff' }}>{user?.nombre}</Typography>
                </Box>
              </Box>
              {fechaVentaDisplay && (
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                  <CalendarTodayIcon sx={{ color: 'rgba(255,255,255,0.8)' }} />
                  <Box>
                    <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.7)', display: 'block' }}>Fecha</Typography>
                    <Typography variant="body1" sx={{ fontWeight: 600, color: '#fff' }}>{fechaVentaDisplay}</Typography>
                  </Box>
                </Box>
              )}
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

        <Box sx={{ display: 'flex', gap: 3, flex: 1, minHeight: 0 }}>
          <Paper sx={{ flex: '1.1 1 0', p: 3, overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
            <IntentSearch
              onSelectProduct={handleSelectProduct}
              fechaVenta={mostrarFechaVenta ? new Date(fechaVenta).toISOString() : undefined}
            />
          </Paper>

          <Box sx={{ flex: '1 1 0', minWidth: 0, display: 'flex', flexDirection: 'column' }}>
            <CartPanel
              selectedClienteId={selectedClienteId}
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

      {!selectedCajaId && (
        <Box sx={{ position: 'fixed', inset: 0, bgcolor: 'background.default', zIndex: 1200 }} />
      )}

      <OfflineConflictDialog open={showConflictDialog} onClose={() => setShowConflictDialog(false)} />

      <SeleccionarCajaDialog
        open={showSeleccionarCaja}
        onSelect={handleSelectCaja}
        onClose={() => navigate('/dashboard')}
      />

      <VentaConfirmDialog
        open={showConfirmDialog}
        venta={lastVenta}
        onClose={() => setShowConfirmDialog(false)}
      />

      <ConfirmDialog {...limpiarConfirmProps} />
      <ConfirmDialog {...cambiarCajaConfirmProps} />
    </Box>
  );
}
