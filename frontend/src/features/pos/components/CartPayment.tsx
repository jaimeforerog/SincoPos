import { Box, ToggleButton, ToggleButtonGroup, TextField, Typography } from '@mui/material';
import PaymentsIcon from '@mui/icons-material/Payments';
import CreditCardIcon from '@mui/icons-material/CreditCard';
import AccountBalanceIcon from '@mui/icons-material/AccountBalance';

interface CartPaymentProps {
  metodoPago: number;
  montoPagado: number;
  total: number;
  fechaVenta: string;
  onMetodoPagoChange: (metodo: number) => void;
  onMontoPagadoChange: (monto: number) => void;
  onFechaVentaChange: (fecha: string) => void;
}

const fmt = (v: number) =>
  new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', minimumFractionDigits: 0 }).format(v);

export function CartPayment({ metodoPago, montoPagado, total, fechaVenta, onMetodoPagoChange, onMontoPagadoChange, onFechaVentaChange }: CartPaymentProps) {
  const cambio = metodoPago === 0 ? Math.max(0, montoPagado - total) : 0;
  const falta  = metodoPago === 0 && montoPagado > 0 && montoPagado < total ? total - montoPagado : 0;

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
      {/* Fecha de la venta */}
      <TextField
        label="Fecha de venta"
        type="datetime-local"
        size="small"
        value={fechaVenta}
        onChange={(e) => onFechaVentaChange(e.target.value)}
        slotProps={{ inputLabel: { shrink: true } }}
        fullWidth
      />

      {/* Método de pago */}
      <ToggleButtonGroup
        value={metodoPago}
        exclusive
        onChange={(_, v) => { if (v !== null) onMetodoPagoChange(v); }}
        size="small"
        fullWidth
      >
        <ToggleButton value={0} sx={{ flex: 1, gap: 0.5, fontSize: '0.75rem' }}>
          <PaymentsIcon sx={{ fontSize: 16 }} /> Efectivo
        </ToggleButton>
        <ToggleButton value={1} sx={{ flex: 1, gap: 0.5, fontSize: '0.75rem' }}>
          <CreditCardIcon sx={{ fontSize: 16 }} /> Tarjeta
        </ToggleButton>
        <ToggleButton value={2} sx={{ flex: 1, gap: 0.5, fontSize: '0.75rem' }}>
          <AccountBalanceIcon sx={{ fontSize: 16 }} /> Transf.
        </ToggleButton>
      </ToggleButtonGroup>

      {/* Monto + cambio/falta (solo efectivo) */}
      {metodoPago === 0 && (
        <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
          <TextField
            label="Monto recibido"
            type="number"
            size="small"
            value={montoPagado || ''}
            onChange={(e) => { const v = parseFloat(e.target.value); onMontoPagadoChange(v >= 0 ? v : 0); }}
            inputProps={{ min: 0, step: 1000 }}
            sx={{ flex: 1 }}
          />
          {cambio > 0 && (
            <Typography variant="body2" color="success.main" sx={{ fontWeight: 700, whiteSpace: 'nowrap' }}>
              Cambio: {fmt(cambio)}
            </Typography>
          )}
          {falta > 0 && (
            <Typography variant="body2" color="warning.main" sx={{ fontWeight: 700, whiteSpace: 'nowrap' }}>
              Falta: {fmt(falta)}
            </Typography>
          )}
        </Box>
      )}
    </Box>
  );
}
