import {
  Box,
  Typography,
  Paper,
  Divider,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  alpha,
} from '@mui/material';
import type { OrdenCompraDTO } from '@/types/api';

const HERO_COLOR = '#1565c0';

interface OrdenCompraProductosTablaProps {
  orden: OrdenCompraDTO;
}

export function OrdenCompraProductosTabla({ orden }: OrdenCompraProductosTablaProps) {
  return (
    <Paper variant="outlined" sx={{ borderRadius: 2, overflow: 'hidden' }}>
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          px: 2,
          py: 1.5,
          borderBottom: '1px solid',
          borderColor: 'divider',
          bgcolor: alpha(HERO_COLOR, 0.04),
        }}
      >
        <Box>
          <Typography variant="subtitle1" fontWeight={700}>
            Productos ({orden.detalles.length})
          </Typography>
          <Typography variant="caption" color="text.secondary">
            {orden.detalles.reduce((sum, d) => sum + d.cantidadSolicitada, 0)} unidades totales
          </Typography>
        </Box>
      </Box>

      <TableContainer>
        <Table
          size="small"
          sx={{
            '& .MuiTableCell-root': { py: 0.75, px: 1 },
            '& .MuiTableCell-head': {
              bgcolor: 'grey.50',
              fontWeight: 700,
              fontSize: '0.72rem',
              textTransform: 'uppercase',
              letterSpacing: '0.04em',
              color: 'text.secondary',
              borderBottom: `2px solid ${alpha(HERO_COLOR, 0.15)}`,
            },
          }}
        >
          <TableHead>
            <TableRow>
              <TableCell width={36}>#</TableCell>
              <TableCell>Producto</TableCell>
              <TableCell width={140} sx={{ fontFamily: 'monospace' }}>Código</TableCell>
              <TableCell align="center" width={90}>Solicitada</TableCell>
              <TableCell align="center" width={90}>Recibida</TableCell>
              <TableCell align="right" width={110}>P. Unit.</TableCell>
              <TableCell align="right" width={110}>Subtotal</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {orden.detalles.map((detalle, index) => {
              const recibidaCompleta = detalle.cantidadRecibida >= detalle.cantidadSolicitada;
              const recibidaParcial = detalle.cantidadRecibida > 0 && !recibidaCompleta;

              return (
                <TableRow
                  key={detalle.id}
                  sx={{
                    '&:hover': { bgcolor: alpha(HERO_COLOR, 0.02) },
                    '&:last-child td': { borderBottom: 0 },
                  }}
                >
                  <TableCell>
                    <Typography variant="caption" color="text.secondary">{index + 1}</Typography>
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2" fontWeight={500}>
                      {detalle.nombreProducto}
                    </Typography>
                    {detalle.nombreImpuesto && (
                      <Typography variant="caption" color="text.secondary">
                        {detalle.nombreImpuesto}
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell>
                    <Typography
                      variant="caption"
                      sx={{ fontFamily: 'monospace', color: 'text.secondary', fontSize: '0.72rem' }}
                    >
                      {detalle.productoId}
                    </Typography>
                  </TableCell>
                  <TableCell align="center">
                    <Typography variant="body2">{detalle.cantidadSolicitada}</Typography>
                  </TableCell>
                  <TableCell align="center">
                    <Typography
                      variant="body2"
                      fontWeight={detalle.cantidadRecibida > 0 ? 600 : 400}
                      color={
                        recibidaCompleta
                          ? 'success.main'
                          : recibidaParcial
                          ? 'warning.main'
                          : 'text.disabled'
                      }
                    >
                      {detalle.cantidadRecibida || '—'}
                    </Typography>
                  </TableCell>
                  <TableCell align="right">
                    <Typography variant="body2">
                      ${detalle.precioUnitario.toLocaleString('es-CO')}
                    </Typography>
                  </TableCell>
                  <TableCell align="right">
                    <Typography variant="body2" fontWeight={500}>
                      ${detalle.subtotal.toLocaleString('es-CO')}
                    </Typography>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </TableContainer>

      <Box
        sx={{
          px: 2,
          py: 1.5,
          borderTop: '1px solid',
          borderColor: 'divider',
          bgcolor: alpha(HERO_COLOR, 0.02),
        }}
      >
        <Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>
          <Box sx={{ width: 220 }}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
              <Typography variant="caption" color="text.secondary">Subtotal:</Typography>
              <Typography variant="caption">${orden.subtotal.toLocaleString('es-CO')}</Typography>
            </Box>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
              <Typography variant="caption" color="text.secondary">IVA:</Typography>
              <Typography variant="caption">${orden.impuestos.toLocaleString('es-CO')}</Typography>
            </Box>
            <Divider sx={{ my: 0.75 }} />
            <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
              <Typography variant="body2" fontWeight={700}>Total:</Typography>
              <Typography variant="body2" fontWeight={700} color="primary">
                ${orden.total.toLocaleString('es-CO')}
              </Typography>
            </Box>
          </Box>
        </Box>
      </Box>
    </Paper>
  );
}
