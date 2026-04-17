import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Box,
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Chip,
  Divider,
} from '@mui/material';
import type { OrdenCompraDTO } from '@/types/api';
import { formatDateOnly } from '@/utils/format';

interface OrdenCompraDetalleDialogProps {
  open: boolean;
  orden: OrdenCompraDTO;
  onClose: () => void;
}

const getEstadoColor = (estado: string) => {
  switch (estado) {
    case 'Pendiente':
      return 'warning';
    case 'Aprobada':
      return 'info';
    case 'RecibidaParcial':
      return 'primary';
    case 'RecibidaCompleta':
      return 'success';
    case 'Rechazada':
      return 'error';
    case 'Cancelada':
      return 'default';
    default:
      return 'default';
  }
};

export function OrdenCompraDetalleDialog({
  open,
  orden,
  onClose,
}: OrdenCompraDetalleDialogProps) {
  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <Typography variant="h6">Orden de Compra {orden.numeroOrden}</Typography>
          <Chip
            label={orden.estado}
            color={getEstadoColor(orden.estado)}
            size="medium"
          />
        </Box>
      </DialogTitle>

      <DialogContent>
        {/* Información General */}
        <Typography variant="subtitle2" color="text.secondary" gutterBottom>
          INFORMACIÓN GENERAL
        </Typography>
        <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 2, mb: 3 }}>
          <Box>
            <Typography variant="caption" color="text.secondary">
              Sucursal
            </Typography>
            <Typography variant="body2">{orden.nombreSucursal}</Typography>
          </Box>
          <Box>
            <Typography variant="caption" color="text.secondary">
              Proveedor
            </Typography>
            <Typography variant="body2">{orden.nombreProveedor}</Typography>
          </Box>
          <Box>
            <Typography variant="caption" color="text.secondary">
              Fecha de Orden
            </Typography>
            <Typography variant="body2">
              {formatDateOnly(orden.fechaOrden)}
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" color="text.secondary">
              Fecha de Entrega Esperada
            </Typography>
            <Typography variant="body2">
              {orden.fechaEntregaEsperada
                ? formatDateOnly(orden.fechaEntregaEsperada)
                : 'No especificada'}
            </Typography>
          </Box>

          {orden.fechaAprobacion && (
            <>
              <Box>
                <Typography variant="caption" color="text.secondary">
                  Fecha de Aprobación
                </Typography>
                <Typography variant="body2">
                  {formatDateOnly(orden.fechaAprobacion)}
                </Typography>
              </Box>
              <Box>
                <Typography variant="caption" color="text.secondary">
                  Aprobado Por
                </Typography>
                <Typography variant="body2">{orden.aprobadoPor || 'N/A'}</Typography>
              </Box>
            </>
          )}

          <Box>
            <Typography variant="caption" color="text.secondary">
              Forma de Pago
            </Typography>
            <Typography variant="body2">
              {orden.formaPago} {orden.formaPago === 'Credito' ? `(${orden.diasPlazo} días)` : ''}
            </Typography>
          </Box>

          {orden.fechaRecepcion && (
            <>
              <Box>
                <Typography variant="caption" color="text.secondary">
                  Fecha de Recepción
                </Typography>
                <Typography variant="body2">
                  {formatDateOnly(orden.fechaRecepcion)}
                </Typography>
              </Box>
              <Box>
                <Typography variant="caption" color="text.secondary">
                  Recibido Por
                </Typography>
                <Typography variant="body2">{orden.recibidoPor || 'N/A'}</Typography>
              </Box>
            </>
          )}
        </Box>

        {orden.observaciones && (
          <>
            <Typography variant="caption" color="text.secondary">
              Observaciones
            </Typography>
            <Typography variant="body2" sx={{ mb: 2 }}>
              {orden.observaciones}
            </Typography>
          </>
        )}

        {orden.motivoRechazo && (
          <>
            <Typography variant="caption" color="text.secondary">
              Motivo de Rechazo/Cancelación
            </Typography>
            <Typography variant="body2" color="error" sx={{ mb: 2 }}>
              {orden.motivoRechazo}
            </Typography>
          </>
        )}

        <Divider sx={{ my: 2 }} />

        {/* Detalle de Productos */}
        <Typography variant="subtitle2" color="text.secondary" gutterBottom>
          PRODUCTOS
        </Typography>
        <TableContainer component={Paper} variant="outlined" sx={{ mb: 3 }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Producto</TableCell>
                <TableCell align="center">Solicitada</TableCell>
                <TableCell align="center">Recibida</TableCell>
                <TableCell align="right">P. Unit.</TableCell>
                <TableCell align="center">Impuesto</TableCell>
                <TableCell align="right">Subtotal</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {orden.detalles.map((detalle) => (
                <TableRow key={detalle.id}>
                  <TableCell>
                    <Typography variant="body2">{detalle.nombreProducto}</Typography>
                    {detalle.observaciones && (
                      <Typography variant="caption" color="text.secondary">
                        {detalle.observaciones}
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell align="center">{detalle.cantidadSolicitada}</TableCell>
                  <TableCell align="center">
                    <Typography
                      variant="body2"
                      color={
                        detalle.cantidadRecibida >= detalle.cantidadSolicitada
                          ? 'success.main'
                          : detalle.cantidadRecibida > 0
                          ? 'warning.main'
                          : 'text.secondary'
                      }
                      fontWeight={detalle.cantidadRecibida > 0 ? 'medium' : 'normal'}
                    >
                      {detalle.cantidadRecibida}
                    </Typography>
                  </TableCell>
                  <TableCell align="right">
                    ${detalle.precioUnitario.toLocaleString('es-CO')}
                  </TableCell>
                  <TableCell align="center">
                    <Typography variant="body2">
                      {detalle.nombreImpuesto ?? '-'}
                    </Typography>
                  </TableCell>
                  <TableCell align="right">
                    ${detalle.subtotal.toLocaleString('es-CO')}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>

        {/* Totales */}
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end' }}>
          <Box>
            {orden.requiereFacturaElectronica && (
              <Chip label="Requiere Factura Electrónica" color="warning" size="small" />
            )}
          </Box>
          <Box sx={{ width: 300 }}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
              <Typography variant="body2">Subtotal:</Typography>
              <Typography variant="body2">
                ${orden.subtotal.toLocaleString('es-CO')}
              </Typography>
            </Box>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
              <Typography variant="body2">Impuestos:</Typography>
              <Typography variant="body2">
                ${orden.impuestos.toLocaleString('es-CO')}
              </Typography>
            </Box>
            <Divider sx={{ my: 1 }} />
            <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
              <Typography variant="h6">Total:</Typography>
              <Typography variant="h6" color="primary">
                ${orden.total.toLocaleString('es-CO')}
              </Typography>
            </Box>
          </Box>
        </Box>
      </DialogContent>

      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={onClose}>Cerrar</Button>
      </DialogActions>
    </Dialog>
  );
}
