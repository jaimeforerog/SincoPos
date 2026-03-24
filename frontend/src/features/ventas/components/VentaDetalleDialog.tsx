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
  Chip,
  Tooltip,
} from '@mui/material';
import SyncIcon from '@mui/icons-material/Sync';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import ErrorIcon from '@mui/icons-material/Error';
import HourglassEmptyIcon from '@mui/icons-material/HourglassEmpty';
import PrintIcon from '@mui/icons-material/Print';
import type { VentaDTO } from '@/types/api';
import { useAuth } from '@/hooks/useAuth';
import { printTicket } from '@/utils/printTicket';

interface VentaDetalleDialogProps {
  open: boolean;
  venta: VentaDTO | null;
  onClose: () => void;
}

export function VentaDetalleDialog({ open, venta, onClose }: VentaDetalleDialogProps) {
  const { user } = useAuth();
  const formatCurrency = (value: number) => {
    return new Intl.NumberFormat('es-CO', {
      style: 'currency',
      currency: 'COP',
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(value);
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleString('es-CO', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  if (!venta) return null;

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <Typography variant="h5" sx={{ fontWeight: 700 }}>
            Detalle de Venta
          </Typography>
          <Chip
            label={venta.estado}
            color={
              venta.estado === 'Completada'
                ? 'success'
                : venta.estado === 'Cancelada' || venta.estado === 'Anulada'
                ? 'error'
                : 'default'
            }
          />
        </Box>
      </DialogTitle>

      <DialogContent>
        {/* Información General */}
        <Box sx={{ mb: 3 }}>
          <Typography variant="h6" sx={{ mb: 2, fontWeight: 600 }}>
            Información General
          </Typography>
          <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 2 }}>
            <Box>
              <Typography variant="caption" color="text.secondary">
                Número de Venta
              </Typography>
              <Typography variant="body1" sx={{ fontWeight: 600, fontFamily: 'monospace' }}>
                {venta.numeroVenta}
              </Typography>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary">
                Fecha
              </Typography>
              <Typography variant="body1">{formatDate(venta.fechaVenta)}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary">
                Sucursal
              </Typography>
              <Typography variant="body1">{venta.nombreSucursal}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary">
                Caja
              </Typography>
              <Typography variant="body1">{venta.nombreCaja}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary">
                Cliente
              </Typography>
              <Typography variant="body1">
                {venta.nombreCliente || 'Sin cliente'}
              </Typography>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary">
                Método de Pago
              </Typography>
              <Typography variant="body1">{venta.metodoPago}</Typography>
            </Box>
          </Box>
        </Box>

        <Divider sx={{ my: 2 }} />

        {/* Productos */}
        <Box sx={{ mb: 3 }}>
          <Typography variant="h6" sx={{ mb: 2, fontWeight: 600 }}>
            Productos
          </Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell sx={{ fontWeight: 700 }}>Producto</TableCell>
                <TableCell align="center" sx={{ fontWeight: 700 }}>Cant.</TableCell>
                <TableCell align="right" sx={{ fontWeight: 700 }}>Precio</TableCell>
                <TableCell align="right" sx={{ fontWeight: 700 }}>Desc.</TableCell>
                <TableCell align="right" sx={{ fontWeight: 700 }}>Subtotal</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {venta.detalles.map((detalle) => (
                <TableRow key={detalle.id}>
                  <TableCell>{detalle.nombreProducto}</TableCell>
                  <TableCell align="center">{detalle.cantidad}</TableCell>
                  <TableCell align="right">{formatCurrency(detalle.precioUnitario)}</TableCell>
                  <TableCell align="right">
                    {detalle.descuento > 0 ? formatCurrency(detalle.descuento) : '-'}
                  </TableCell>
                  <TableCell align="right" sx={{ fontWeight: 600 }}>
                    {formatCurrency(detalle.subtotal)}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>

        <Divider sx={{ my: 2 }} />

        {/* Totales */}
        <Box>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
            <Typography variant="body2">Subtotal:</Typography>
            <Typography variant="body2">{formatCurrency(venta.subtotal)}</Typography>
          </Box>

          {venta.descuento > 0 && (
            <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
              <Typography variant="body2" color="error">
                Descuentos:
              </Typography>
              <Typography variant="body2" color="error">
                -{formatCurrency(venta.descuento)}
              </Typography>
            </Box>
          )}

          {venta.impuestos > 0 && (
            <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
              <Typography variant="body2">Impuestos:</Typography>
              <Typography variant="body2">{formatCurrency(venta.impuestos)}</Typography>
            </Box>
          )}

          <Divider sx={{ my: 1 }} />

          <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 2 }}>
            <Typography variant="h6" sx={{ fontWeight: 700 }}>
              TOTAL:
            </Typography>
            <Typography variant="h6" color="primary.main" sx={{ fontWeight: 700 }}>
              {formatCurrency(venta.total)}
            </Typography>
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

          {venta.observaciones && (
            <Box sx={{ mt: 2, p: 1.5, bgcolor: 'grey.100', borderRadius: 1 }}>
              <Typography variant="caption" color="text.secondary">
                Observaciones
              </Typography>
              <Typography variant="body2">{venta.observaciones}</Typography>
            </Box>
          )}
        </Box>

        {/* Estado ERP */}
        <Divider sx={{ my: 2 }} />
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
          <SyncIcon fontSize="small" color="action" />
          <Typography variant="caption" color="text.secondary" sx={{ mr: 0.5 }}>
            Sincronización ERP:
          </Typography>
          {venta.sincronizadoErp ? (
            <Tooltip title={`Ref: ${venta.erpReferencia ?? ''} · ${venta.fechaSincronizacionErp ? new Date(venta.fechaSincronizacionErp).toLocaleString('es-CO') : ''}`}>
              <Chip
                icon={<CheckCircleIcon />}
                label="Sincronizado"
                color="success"
                size="small"
              />
            </Tooltip>
          ) : venta.errorSincronizacion ? (
            <Tooltip title={venta.errorSincronizacion}>
              <Chip
                icon={<ErrorIcon />}
                label="Error ERP"
                color="error"
                size="small"
              />
            </Tooltip>
          ) : (
            <Chip
              icon={<HourglassEmptyIcon />}
              label="Pendiente ERP"
              color="warning"
              size="small"
            />
          )}
        </Box>
      </DialogContent>

      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button
          variant="outlined"
          startIcon={<PrintIcon />}
          onClick={() => printTicket(venta, user?.nombre ?? user?.email)}
        >
          Imprimir
        </Button>
        <Button onClick={onClose} variant="contained">
          Cerrar
        </Button>
      </DialogActions>
    </Dialog>
  );
}
