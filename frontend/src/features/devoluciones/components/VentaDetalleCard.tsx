import {
  Box,
  Card,
  CardContent,
  Typography,
  TextField,
  Button,
  Stack,
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
import UndoIcon from '@mui/icons-material/Undo';
import { formatCurrency, formatDate } from '@/utils/format';
import type { VentaDTO } from '@/types/api';

interface VentaDetalleCardProps {
  venta: VentaDTO;
  cantidadesDevolver: Record<string, number>;
  validationErrors: Record<string, string | null>;
  onCantidadChange: (productoId: string, valor: string, disponible: number) => void;
  calcularCantidadDevuelta: (productoId: string) => number;
  calcularCantidadDisponible: (productoId: string, cantidadOriginal: number) => number;
  totalDevolucion: number;
  hasValidationErrors: boolean;
  onProcesar: () => void;
}

export function VentaDetalleCard({
  venta,
  cantidadesDevolver,
  validationErrors,
  onCantidadChange,
  calcularCantidadDevuelta,
  calcularCantidadDisponible,
  totalDevolucion,
  hasValidationErrors,
  onProcesar,
}: VentaDetalleCardProps) {
  return (
    <Card>
      <CardContent>
        <Typography variant="h6" gutterBottom fontWeight={600}>
          Venta {venta.numeroVenta}
        </Typography>
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={3} sx={{ mb: 2 }}>
          <Box sx={{ flex: 1 }}>
            <Typography variant="body2" color="text.secondary">Fecha</Typography>
            <Typography variant="body1">{formatDate(venta.fechaVenta)}</Typography>
          </Box>
          <Box sx={{ flex: 1 }}>
            <Typography variant="body2" color="text.secondary">Sucursal</Typography>
            <Typography variant="body1">{venta.nombreSucursal}</Typography>
          </Box>
          <Box sx={{ flex: 1 }}>
            <Typography variant="body2" color="text.secondary">Total Venta</Typography>
            <Typography variant="h6" fontWeight={600} color="primary.main">
              {formatCurrency(venta.total)}
            </Typography>
          </Box>
          <Box sx={{ flex: 1 }}>
            <Typography variant="body2" color="text.secondary">Estado</Typography>
            <Chip label={venta.estado} color="success" size="small" />
          </Box>
        </Stack>

        <Divider sx={{ my: 2 }} />

        <Typography variant="subtitle1" gutterBottom fontWeight={600}>
          Productos de la Venta
        </Typography>
        <TableContainer component={Paper} variant="outlined">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Producto</TableCell>
                <TableCell align="right">Cant. Vendida</TableCell>
                <TableCell align="right">Precio Unit.</TableCell>
                <TableCell align="right">Subtotal</TableCell>
                <TableCell align="right">Devuelta</TableCell>
                <TableCell align="right">Disponible</TableCell>
                <TableCell align="right">Devolver</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {venta.detalles.map((detalle) => {
                const devuelta = calcularCantidadDevuelta(detalle.productoId);
                const disponible = calcularCantidadDisponible(detalle.productoId, detalle.cantidad);
                return (
                  <TableRow key={detalle.id}>
                    <TableCell>{detalle.nombreProducto}</TableCell>
                    <TableCell align="right">{detalle.cantidad}</TableCell>
                    <TableCell align="right">{formatCurrency(detalle.precioUnitario)}</TableCell>
                    <TableCell align="right">{formatCurrency(detalle.subtotal)}</TableCell>
                    <TableCell align="right">
                      {devuelta > 0 ? <Chip label={devuelta} size="small" color="warning" /> : '-'}
                    </TableCell>
                    <TableCell align="right">
                      <Chip
                        label={disponible}
                        size="small"
                        color={disponible > 0 ? 'success' : 'default'}
                      />
                    </TableCell>
                    <TableCell align="right">
                      <TextField
                        type="number"
                        size="small"
                        value={cantidadesDevolver[detalle.productoId] || ''}
                        onChange={(e) =>
                          onCantidadChange(detalle.productoId, e.target.value, disponible)
                        }
                        disabled={disponible === 0}
                        error={!!validationErrors[detalle.productoId]}
                        helperText={
                          validationErrors[detalle.productoId] ||
                          (disponible > 0 ? `Max: ${disponible}` : 'Sin disponible')
                        }
                        inputProps={{ min: 0, max: disponible }}
                        sx={{ width: 120 }}
                      />
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>

        <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mt: 2 }}>
          <Typography variant="h6">
            Total a Devolver: {formatCurrency(totalDevolucion)}
          </Typography>
          <Button
            variant="contained"
            color="error"
            startIcon={<UndoIcon />}
            onClick={onProcesar}
            disabled={totalDevolucion === 0 || hasValidationErrors}
          >
            Procesar Devolución
          </Button>
        </Stack>
      </CardContent>
    </Card>
  );
}
