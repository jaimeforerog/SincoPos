import { Box, Typography, Divider } from '@mui/material';

interface TotalesOrdenProps {
  subtotal: number;
  impuestos: number;
  total: number;
}

export function TotalesOrden({ subtotal, impuestos, total }: TotalesOrdenProps) {
  return (
    <Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>
      <Box sx={{ width: 200 }}>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
          <Typography variant="caption" color="text.secondary">Subtotal:</Typography>
          <Typography variant="caption">${subtotal.toLocaleString('es-CO')}</Typography>
        </Box>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
          <Typography variant="caption" color="text.secondary">IVA:</Typography>
          <Typography variant="caption">${impuestos.toLocaleString('es-CO')}</Typography>
        </Box>
        <Divider sx={{ my: 0.5 }} />
        <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
          <Typography variant="body2" fontWeight={700}>Total:</Typography>
          <Typography variant="body2" fontWeight={700} color="primary">
            ${total.toLocaleString('es-CO')}
          </Typography>
        </Box>
      </Box>
    </Box>
  );
}
