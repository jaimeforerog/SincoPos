import { Box, Typography, Divider } from '@mui/material';

interface CartTotalsProps {
  subtotal: number;
  totalDescuentos: number;
  totalImpuestos: number;
  total: number;
}

export function CartTotals({
  subtotal,
  totalDescuentos,
  totalImpuestos,
  total,
}: CartTotalsProps) {
  const formatCurrency = (value: number) => {
    return new Intl.NumberFormat('es-CO', {
      style: 'currency',
      currency: 'COP',
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(value);
  };

  return (
    <Box sx={{ p: 2, bgcolor: 'grey.50', borderRadius: 1 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
        <Typography variant="body2">Subtotal:</Typography>
        <Typography variant="body2">{formatCurrency(subtotal)}</Typography>
      </Box>

      {totalDescuentos > 0 && (
        <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
          <Typography variant="body2" color="error">
            Descuentos:
          </Typography>
          <Typography variant="body2" color="error">
            -{formatCurrency(totalDescuentos)}
          </Typography>
        </Box>
      )}

      {totalImpuestos > 0 && (
        <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
          <Typography variant="body2">Impuestos:</Typography>
          <Typography variant="body2">{formatCurrency(totalImpuestos)}</Typography>
        </Box>
      )}

      <Divider sx={{ my: 1 }} />

      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h6" sx={{ fontWeight: 700 }}>
          TOTAL:
        </Typography>
        <Typography variant="h5" color="primary.main" sx={{ fontWeight: 700 }}>
          {formatCurrency(total)}
        </Typography>
      </Box>
    </Box>
  );
}
