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
  onMetodoPagoChange: (metodo: number) => void;
  onMontoPagadoChange: (monto: number) => void;

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
  onMetodoPagoChange,
  onMontoPagadoChange,
  onClear,
  onCobrar,
  canCobrar,
  isLoading,
  isOffline = false,
}: CartPanelProps) {
  return (
    <Paper sx={{ p: 3, display: 'flex', flexDirection: 'column', height: '100%', maxHeight: '100%', overflow: 'hidden' }}>
      <Typography variant="h5" sx={{ mb: 2, fontWeight: 700, flexShrink: 0 }}>
        Carrito de Compra
      </Typography>

      <Box sx={{ flexShrink: 0 }}>
        <CartHeader
          selectedCajaId={selectedCajaId}
          selectedClienteId={selectedClienteId}
          onCajaChange={onCajaChange}
          onClienteChange={onClienteChange}
        />
        {/* Capa 4 — historial del cliente seleccionado */}
        {selectedClienteId && (
          <ClienteHistorialCard clienteId={selectedClienteId} />
        )}
      </Box>

      <Box sx={{ flexGrow: 1, overflow: 'auto', mb: 2, minHeight: 0, maxHeight: 'calc(100vh - 520px)' }}>
        <CartItems
          items={items}
          onUpdateQuantity={onUpdateQuantity}
          onUpdatePrice={onUpdatePrice}
          onUpdateDiscount={onUpdateDiscount}
          onRemove={onRemoveItem}
        />
      </Box>

      <Box sx={{ flexShrink: 0 }}>
        <CartTotals
          subtotal={subtotal}
          totalDescuentos={totalDescuentos}
          totalImpuestos={totalImpuestos}
          total={total}
        />

        <Box sx={{ mt: 2 }}>
          <CartPayment
            metodoPago={metodoPago}
            montoPagado={montoPagado}
            total={total}
            onMetodoPagoChange={onMetodoPagoChange}
            onMontoPagadoChange={onMontoPagadoChange}
          />

          <CartActions
            onClear={onClear}
            onCobrar={onCobrar}
            canCobrar={canCobrar}
            isLoading={isLoading}
            isOffline={isOffline}
          />
        </Box>
      </Box>
    </Paper>
  );
}
