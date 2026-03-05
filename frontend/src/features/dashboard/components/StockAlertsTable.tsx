import {
  Card,
  CardContent,
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Box,
  Chip,
  Alert,
} from '@mui/material';
import WarningIcon from '@mui/icons-material/Warning';
import type { AlertaStockDTO } from '@/types/api';

interface StockAlertsTableProps {
  alerts: AlertaStockDTO[];
}

const getNivelStock = (actual: number, minimo: number) => {
  if (actual === 0) return { nivel: 'Crítico', color: 'error' as const };
  if (actual <= minimo) return { nivel: 'Bajo', color: 'warning' as const };
  return { nivel: 'Medio', color: 'info' as const };
};

export function StockAlertsTable({ alerts }: StockAlertsTableProps) {
  const alertasCriticas = alerts.filter((a) => a.cantidadActual === 0);

  return (
    <Card>
      <CardContent>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 2 }}>
          <WarningIcon color="warning" />
          <Typography variant="h6" sx={{ fontWeight: 600 }}>
            Alertas de Stock Bajo
          </Typography>
        </Box>

        {alertasCriticas.length > 0 && (
          <Alert severity="error" sx={{ mb: 2 }}>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {alertasCriticas.length} producto(s) sin stock
            </Typography>
          </Alert>
        )}

        {alerts.length === 0 ? (
          <Box
            sx={{
              py: 4,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <Typography color="text.secondary">
              ¡Todo bien! No hay alertas de stock bajo
            </Typography>
          </Box>
        ) : (
          <TableContainer sx={{ maxHeight: 400 }}>
            <Table size="small" stickyHeader>
              <TableHead>
                <TableRow>
                  <TableCell sx={{ fontWeight: 700 }}>Producto</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>Sucursal</TableCell>
                  <TableCell sx={{ fontWeight: 700 }} align="center">
                    Actual
                  </TableCell>
                  <TableCell sx={{ fontWeight: 700 }} align="center">
                    Mínimo
                  </TableCell>
                  <TableCell sx={{ fontWeight: 700 }} align="center">
                    Nivel
                  </TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {alerts.map((alert) => {
                  const { nivel, color } = getNivelStock(
                    alert.cantidadActual,
                    alert.stockMinimo
                  );

                  return (
                    <TableRow
                      key={`${alert.productoId}-${alert.sucursalId}`}
                      hover
                      sx={{
                        '&:last-child td, &:last-child th': { border: 0 },
                        backgroundColor:
                          alert.cantidadActual === 0
                            ? 'error.lighter'
                            : 'transparent',
                      }}
                    >
                      <TableCell>
                        <Typography variant="body2" sx={{ fontWeight: 600 }}>
                          {alert.nombreProducto}
                        </Typography>
                        {alert.codigoBarras && (
                          <Typography
                            variant="caption"
                            color="text.secondary"
                            sx={{ fontFamily: 'monospace' }}
                          >
                            {alert.codigoBarras}
                          </Typography>
                        )}
                      </TableCell>
                      <TableCell>
                        <Typography variant="body2">
                          {alert.nombreSucursal}
                        </Typography>
                      </TableCell>
                      <TableCell align="center">
                        <Typography
                          variant="body2"
                          sx={{
                            fontWeight: 700,
                            color:
                              alert.cantidadActual === 0
                                ? 'error.main'
                                : 'text.primary',
                          }}
                        >
                          {alert.cantidadActual}
                        </Typography>
                      </TableCell>
                      <TableCell align="center">
                        <Typography variant="body2" color="text.secondary">
                          {alert.stockMinimo}
                        </Typography>
                      </TableCell>
                      <TableCell align="center">
                        <Chip
                          label={nivel}
                          size="small"
                          color={color}
                          sx={{ fontWeight: 600 }}
                        />
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </CardContent>
    </Card>
  );
}
