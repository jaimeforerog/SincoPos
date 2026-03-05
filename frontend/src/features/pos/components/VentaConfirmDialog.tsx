import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Box,
  Typography,
  Divider,
} from '@mui/material';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import type { VentaDTO } from '@/types/api';

interface VentaConfirmDialogProps {
  open: boolean;
  venta: VentaDTO | null;
  onClose: () => void;
}

export function VentaConfirmDialog({ open, venta, onClose }: VentaConfirmDialogProps) {
  const formatCurrency = (value: number) => {
    return new Intl.NumberFormat('es-CO', {
      style: 'currency',
      currency: 'COP',
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(value);
  };

  if (!venta) return null;

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ textAlign: 'center', pt: 3 }}>
        <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 1 }}>
          <CheckCircleIcon color="success" sx={{ fontSize: 60 }} />
          <Typography variant="h5" sx={{ fontWeight: 700 }}>
            Venta Completada
          </Typography>
        </Box>
      </DialogTitle>

      <DialogContent>
        <Box sx={{ textAlign: 'center', mb: 2 }}>
          <Typography variant="h4" color="primary.main" sx={{ fontWeight: 700, mb: 1 }}>
            {venta.numeroVenta}
          </Typography>
        </Box>

        <Divider sx={{ my: 2 }} />

        <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
          <Typography variant="body1">Subtotal:</Typography>
          <Typography variant="body1">{formatCurrency(venta.subtotal)}</Typography>
        </Box>

        {venta.descuento > 0 && (
          <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
            <Typography variant="body1" color="error">
              Descuentos:
            </Typography>
            <Typography variant="body1" color="error">
              -{formatCurrency(venta.descuento)}
            </Typography>
          </Box>
        )}

        {venta.impuestos > 0 && (
          <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
            <Typography variant="body1">Impuestos:</Typography>
            <Typography variant="body1">{formatCurrency(venta.impuestos)}</Typography>
          </Box>
        )}

        <Divider sx={{ my: 2 }} />

        <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 2 }}>
          <Typography variant="h6" sx={{ fontWeight: 700 }}>
            Total:
          </Typography>
          <Typography variant="h6" color="primary.main" sx={{ fontWeight: 700 }}>
            {formatCurrency(venta.total)}
          </Typography>
        </Box>

        <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
          <Typography variant="body2" color="text.secondary">
            Método de pago:
          </Typography>
          <Typography variant="body2">{venta.metodoPago}</Typography>
        </Box>

        {venta.montoPagado && (
          <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
            <Typography variant="body2" color="text.secondary">
              Monto pagado:
            </Typography>
            <Typography variant="body2">{formatCurrency(venta.montoPagado)}</Typography>
          </Box>
        )}

        {venta.cambio && venta.cambio > 0 && (
          <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
            <Typography variant="body2" color="success.main" sx={{ fontWeight: 600 }}>
              Cambio:
            </Typography>
            <Typography variant="body2" color="success.main" sx={{ fontWeight: 600 }}>
              {formatCurrency(venta.cambio)}
            </Typography>
          </Box>
        )}

        <Divider sx={{ my: 2 }} />

        <Typography variant="body2" color="text.secondary" align="center">
          Productos: {venta.detalles.length} •{' '}
          Caja: {venta.nombreCaja}
        </Typography>
      </DialogContent>

      <DialogActions sx={{ px: 3, pb: 3, justifyContent: 'center' }}>
        <Button
          variant="contained"
          size="large"
          onClick={onClose}
          sx={{ minWidth: 200 }}
        >
          Nueva Venta
        </Button>
      </DialogActions>
    </Dialog>
  );
}
