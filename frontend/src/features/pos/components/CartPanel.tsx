import { Box, Paper, Typography } from '@mui/material';
import { CartHeader } from './CartHeader';
import { CartItems } from './CartItems';
import { CartTotals } from './CartTotals';
import { CartPayment } from './CartPayment';
import { CartActions } from './CartActions';
import { ClienteHistorialCard } from './ClienteHistorialCard';
import type { CartItem } from '@/stores/cart.store';

interface CartPanelProps {
  // Caja y Cliente
  selectedCajaId: number | null;
  selectedClienteId: number | null;
  onCajaChange: (cajaId: number | null) => void;
  onClienteChange: (clienteId: number | null) => void;

  // Items del carrito
  items: CartItem[];
  onUpdateQuantity: (productoId: string, cantidad: number) => void;
  onUpdatePrice: (productoId: string, precio: number) => void;
  onUpdateDiscount: (productoId: string, descuento: number) => void;
  onRemoveItem: (productoId: string) => void;

  // Totales
  subtotal: number;
  totalDescuentos: number;
  totalImpuestos: number;
  total: number;

  // Pago
  metodoPago: number;
  montoPagado: number;
  fechaVenta: string;
  onMetodoPagoChange: (metodo: number) => void;
  onMontoPagadoChange: (monto: number) => void;
  onFechaVentaChange: (fecha: string) => void;

  // Acciones
  onClear: () => void;
  onCobrar: () => void;
  canCobrar: boolean;
  isLoading: boolean;
  isOffline?: boolean;
}

export function CartPanel({
  selectedCajaId,
  selectedClienteId,
  onCajaChange,
  onClienteChange,
  items,
  onUpdateQuantity,
  onUpdatePrice,
  onUpdateDiscount,
  onRemoveItem,
  subtotal,
  totalDescuentos,
  totalImpuestos,
  total,
  metodoPago,
  montoPagado,
  fechaVenta,
  onMetodoPagoChange,
  onMontoPagadoChange,
  onFechaVentaChange,
  onClear,
  onCobrar,
  canCobrar,
  isLoading,
  isOffline = false,
}: CartPanelProps) {
  return (
    <Paper sx={{ display: 'flex', flexDirection: 'column', flex: 1, overflow: 'hidden' }}>
      {/* Área scrollable: encabezado + tabla de items */}
      <Box sx={{ flex: 1, overflow: 'auto', overflowX: 'hidden', minHeight: 0, px: 2, pt: 2, pb: 1 }}>
        <Typography variant="h6" sx={{ mb: 1.5, fontWeight: 700 }}>
          Carrito de Compra
        </Typography>

        <CartHeader
          selectedCajaId={selectedCajaId}
          selectedClienteId={selectedClienteId}
          onCajaChange={onCajaChange}
          onClienteChange={onClienteChange}
        />
        {selectedClienteId && (
          <ClienteHistorialCard clienteId={selectedClienteId} />
        )}

        <CartItems
          items={items}
          onUpdateQuantity={onUpdateQuantity}
          onUpdatePrice={onUpdatePrice}
          onUpdateDiscount={onUpdateDiscount}
          onRemove={onRemoveItem}
        />
      </Box>

      {/* Footer fijo: totales + método de pago + botones */}
      <Box sx={{ flexShrink: 0, px: 2, pt: 1.5, pb: 2, borderTop: '1px solid', borderColor: 'divider', display: 'flex', flexDirection: 'column', gap: 1.5 }}>
        <CartTotals
          subtotal={subtotal}
          totalDescuentos={totalDescuentos}
          totalImpuestos={totalImpuestos}
          total={total}
        />

        <CartPayment
          metodoPago={metodoPago}
          montoPagado={montoPagado}
          total={total}
          fechaVenta={fechaVenta}
          onMetodoPagoChange={onMetodoPagoChange}
          onMontoPagadoChange={onMontoPagadoChange}
          onFechaVentaChange={onFechaVentaChange}
        />

        <CartActions
          onClear={onClear}
          onCobrar={onCobrar}
          canCobrar={canCobrar}
          isLoading={isLoading}
          isOffline={isOffline}
        />
      </Box>
    </Paper>
  );
}
