import {
  Box,
  Card,
  CardContent,
  Typography,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Alert,
  CircularProgress,
} from '@mui/material';
import { formatCurrency, formatDate } from '@/utils/format';
import type { DevolucionVentaDTO } from '@/types/api';

interface HistorialDevolucionesProps {
  loading: boolean;
  devoluciones: DevolucionVentaDTO[];
}

export function HistorialDevoluciones({ loading, devoluciones }: HistorialDevolucionesProps) {
  if (loading) {
    return (
      <Box display="flex" justifyContent="center" py={3}>
        <CircularProgress />
      </Box>
    );
  }

  if (devoluciones.length === 0) return null;

  return (
    <Card>
      <CardContent>
        <Typography variant="h6" gutterBottom fontWeight={600}>
          Devoluciones Anteriores ({devoluciones.length})
        </Typography>
        {devoluciones.map((devolucion) => (
          <Card key={devolucion.id} variant="outlined" sx={{ mb: 2 }}>
            <CardContent>
              <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 2 }}>
                <Box>
                  <Typography variant="subtitle1" fontWeight={600}>
                    {devolucion.numeroDevolucion}
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    {formatDate(devolucion.fechaDevolucion)}
                  </Typography>
                </Box>
                <Box textAlign="right">
                  <Typography variant="h6" color="error.main">
                    -{formatCurrency(devolucion.totalDevuelto)}
                  </Typography>
                  {devolucion.autorizadoPor && (
                    <Typography variant="caption" color="text.secondary">
                      Por: {devolucion.autorizadoPor}
                    </Typography>
                  )}
                </Box>
              </Stack>
              <Alert severity="info" sx={{ mb: 2 }}>
                <strong>Motivo:</strong> {devolucion.motivo}
              </Alert>
              <TableContainer>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Producto</TableCell>
                      <TableCell align="right">Cantidad</TableCell>
                      <TableCell align="right">Precio Unit.</TableCell>
                      <TableCell align="right">Subtotal</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {devolucion.detalles.map((detalle) => (
                      <TableRow key={detalle.id}>
                        <TableCell>{detalle.nombreProducto}</TableCell>
                        <TableCell align="right">{detalle.cantidadDevuelta}</TableCell>
                        <TableCell align="right">{formatCurrency(detalle.precioUnitario)}</TableCell>
                        <TableCell align="right">{formatCurrency(detalle.subtotalDevuelto)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            </CardContent>
          </Card>
        ))}
      </CardContent>
    </Card>
  );
}
