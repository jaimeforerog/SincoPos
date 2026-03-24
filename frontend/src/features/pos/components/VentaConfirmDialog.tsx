import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Box,
  Typography,
  Divider,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Stack,
} from '@mui/material';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import PrintIcon from '@mui/icons-material/Print';
import type { VentaDTO } from '@/types/api';
import { useAuth } from '@/hooks/useAuth';
import { printTicket } from '@/utils/printTicket';

interface VentaConfirmDialogProps {
  open: boolean;
  venta: VentaDTO | null;
  onClose: () => void;
}

const fmt = (value: number) =>
  new Intl.NumberFormat('es-CO', {
    style: 'currency',
    currency: 'COP',
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  }).format(value);

const fmtDate = (iso: string) =>
  new Intl.DateTimeFormat('es-CO', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(iso));

export function VentaConfirmDialog({ open, venta, onClose }: VentaConfirmDialogProps) {
  const { user } = useAuth();
  if (!venta) return null;

  const handlePrint = () => {
    printTicket(venta, user?.nombre ?? user?.email);
  };

  return (
    <>

      {/* Dialog normal en pantalla */}
      <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
        <DialogTitle sx={{ textAlign: 'center', pt: 3 }}>
          <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 1 }}>
            <CheckCircleIcon color="success" sx={{ fontSize: 56 }} />
            <Typography variant="h5" fontWeight={700}>Venta Completada</Typography>
            <Typography variant="subtitle2" color="text.secondary">
              #{venta.numeroVenta} · {fmtDate(venta.fechaVenta)}
            </Typography>
          </Box>
        </DialogTitle>

        <DialogContent sx={{ px: 3 }}>
          {venta.nombreCliente && (
            <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
              Cliente: <strong>{venta.nombreCliente}</strong>
            </Typography>
          )}

          {/* Tabla de productos */}
          <Table size="small" sx={{ mb: 2 }}>
            <TableHead>
              <TableRow sx={{ '& th': { fontWeight: 700, bgcolor: 'grey.50' } }}>
                <TableCell>Producto</TableCell>
                <TableCell align="center">Cant.</TableCell>
                <TableCell align="right">Precio</TableCell>
                <TableCell align="right">Total</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {venta.detalles.map((d) => (
                <TableRow key={d.id}>
                  <TableCell>
                    <Typography variant="body2">{d.nombreProducto}</Typography>
                    {d.numeroLote && (
                      <Typography variant="caption" color="text.secondary">
                        Lote: {d.numeroLote}
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell align="center">{d.cantidad}</TableCell>
                  <TableCell align="right">{fmt(d.precioUnitario)}</TableCell>
                  <TableCell align="right" sx={{ fontWeight: 600 }}>{fmt(d.subtotal)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>

          <Divider />

          {/* Totales */}
          <Stack spacing={0.5} sx={{ mt: 1.5 }}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
              <Typography variant="body2" color="text.secondary">Subtotal</Typography>
              <Typography variant="body2">{fmt(venta.subtotal)}</Typography>
            </Box>
            {venta.descuento > 0 && (
              <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                <Typography variant="body2" color="error.main">Descuento</Typography>
                <Typography variant="body2" color="error.main">-{fmt(venta.descuento)}</Typography>
              </Box>
            )}
            {venta.impuestos > 0 && (
              <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                <Typography variant="body2" color="text.secondary">Impuestos</Typography>
                <Typography variant="body2">{fmt(venta.impuestos)}</Typography>
              </Box>
            )}

            <Divider sx={{ my: 0.5 }} />

            <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
              <Typography variant="h6" fontWeight={700}>Total</Typography>
              <Typography variant="h6" fontWeight={700} color="primary.main">{fmt(venta.total)}</Typography>
            </Box>

            <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
              <Typography variant="body2" color="text.secondary">
                Pago ({venta.metodoPago})
              </Typography>
              <Typography variant="body2">
                {venta.montoPagado ? fmt(venta.montoPagado) : '—'}
              </Typography>
            </Box>

            {venta.cambio != null && venta.cambio > 0 && (
              <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                <Typography variant="body2" color="success.main" fontWeight={600}>Cambio</Typography>
                <Typography variant="body2" color="success.main" fontWeight={600}>{fmt(venta.cambio)}</Typography>
              </Box>
            )}
          </Stack>
        </DialogContent>

        <DialogActions sx={{ px: 3, pb: 3, gap: 1 }}>
          <Button
            variant="outlined"
            startIcon={<PrintIcon />}
            onClick={handlePrint}
            sx={{ flex: 1 }}
          >
            Imprimir
          </Button>
          <Button
            variant="contained"
            size="large"
            onClick={onClose}
            sx={{ flex: 2 }}
          >
            Nueva Venta
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}
