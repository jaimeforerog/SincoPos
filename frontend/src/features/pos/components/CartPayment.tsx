import {
  Box,
  FormControl,
  FormLabel,
  RadioGroup,
  FormControlLabel,
  Radio,
  TextField,
  Alert,
  Typography,
} from '@mui/material';

interface CartPaymentProps {
  metodoPago: number; // 0=Efectivo, 1=Tarjeta, 2=Transferencia
  montoPagado: number;
  total: number;
  onMetodoPagoChange: (metodo: number) => void;
  onMontoPagadoChange: (monto: number) => void;
}

export function CartPayment({
  metodoPago,
  montoPagado,
  total,
  onMetodoPagoChange,
  onMontoPagadoChange,
}: CartPaymentProps) {
  const formatCurrency = (value: number) => {
    return new Intl.NumberFormat('es-CO', {
      style: 'currency',
      currency: 'COP',
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(value);
  };

  const cambio = metodoPago === 0 ? Math.max(0, montoPagado - total) : 0;

  return (
    <Box sx={{ mb: 2 }}>
      <FormControl component="fieldset" fullWidth sx={{ mb: 2 }}>
        <FormLabel component="legend" sx={{ mb: 1 }}>
          Método de Pago
        </FormLabel>
        <RadioGroup
          row
          value={metodoPago}
          onChange={(e) => onMetodoPagoChange(parseInt(e.target.value))}
        >
          <FormControlLabel value={0} control={<Radio />} label="Efectivo" />
          <FormControlLabel value={1} control={<Radio />} label="Tarjeta" />
          <FormControlLabel value={2} control={<Radio />} label="Transferencia" />
        </RadioGroup>
      </FormControl>

      {metodoPago === 0 && (
        <>
          <TextField
            label="Monto Pagado"
            type="number"
            fullWidth
            value={montoPagado || ''}
            onChange={(e) => {
              const val = parseFloat(e.target.value);
              onMontoPagadoChange(val >= 0 ? val : 0);
            }}
            inputProps={{ min: 0, step: 1000 }}
            sx={{ mb: 2 }}
          />

          {cambio > 0 && (
            <Alert severity="success" sx={{ mb: 2 }}>
              <Typography variant="body2">
                Cambio: <strong>{formatCurrency(cambio)}</strong>
              </Typography>
            </Alert>
          )}

          {montoPagado > 0 && montoPagado < total && (
            <Alert severity="warning" sx={{ mb: 2 }}>
              <Typography variant="body2">
                Falta: <strong>{formatCurrency(total - montoPagado)}</strong>
              </Typography>
            </Alert>
          )}
        </>
      )}
    </Box>
  );
}
