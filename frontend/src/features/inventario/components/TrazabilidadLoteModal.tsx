import { useQuery } from '@tanstack/react-query';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Box,
  Typography,
  Chip,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Divider,
  CircularProgress,
  Alert,
  Stack,
} from '@mui/material';
import {
  LocalShipping,
  ShoppingCart,
  Undo,
  SwapHoriz,
  Inventory,
} from '@mui/icons-material';
import { lotesApi } from '@/api/lotes';
import { formatCurrency } from '@/utils/format';

interface Props {
  loteId: number | null;
  onClose: () => void;
}

function tipoChip(tipo: string) {
  switch (tipo) {
    case 'Venta':
      return <Chip icon={<ShoppingCart fontSize="small" />} label="Venta" color="primary" size="small" />;
    case 'Devolucion':
      return <Chip icon={<Undo fontSize="small" />} label="Devolución" color="warning" size="small" />;
    case 'Traslado':
      return <Chip icon={<SwapHoriz fontSize="small" />} label="Traslado" color="secondary" size="small" />;
    default:
      return <Chip label={tipo} size="small" />;
  }
}

function entradaTipoChip(tipo: string) {
  switch (tipo) {
    case 'OrdenCompra':
      return <Chip icon={<LocalShipping fontSize="small" />} label="Orden de Compra" color="success" size="small" />;
    case 'Traslado':
      return <Chip icon={<SwapHoriz fontSize="small" />} label="Traslado" color="secondary" size="small" />;
    default:
      return <Chip icon={<Inventory fontSize="small" />} label="Entrada Manual" size="small" />;
  }
}

export function TrazabilidadLoteModal({ loteId, onClose }: Props) {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['lotes', 'trazabilidad', loteId],
    queryFn: () => lotesApi.trazabilidad(loteId!),
    enabled: loteId !== null,
  });

  const lote = data?.lote;
  const entrada = data?.entrada;
  const movimientos = data?.movimientos ?? [];

  return (
    <Dialog open={loteId !== null} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>
        Trazabilidad de lote
        {lote && (
          <Typography variant="subtitle2" color="text.secondary">
            {lote.nombreProducto} — Lote: <strong>{lote.numeroLote || '(sin número)'}</strong>
          </Typography>
        )}
      </DialogTitle>

      <DialogContent dividers>
        {isLoading && (
          <Box display="flex" justifyContent="center" py={4}>
            <CircularProgress />
          </Box>
        )}

        {isError && (
          <Alert severity="error">No se pudo cargar la trazabilidad del lote.</Alert>
        )}

        {data && (
          <Stack spacing={3}>
            {/* ── Datos del lote ── */}
            <Box>
              <Typography variant="subtitle1" fontWeight={600} gutterBottom>Datos del lote</Typography>
              <Box
                sx={{
                  display: 'grid',
                  gridTemplateColumns: { xs: '1fr 1fr', sm: 'repeat(4, 1fr)' },
                  gap: 2,
                }}
              >
                <Box>
                  <Typography variant="caption" color="text.secondary">Sucursal</Typography>
                  <Typography variant="body2">{lote!.nombreSucursal}</Typography>
                </Box>
                <Box>
                  <Typography variant="caption" color="text.secondary">Costo unitario</Typography>
                  <Typography variant="body2">{formatCurrency(lote!.costoUnitario)}</Typography>
                </Box>
                <Box>
                  <Typography variant="caption" color="text.secondary">Cantidad inicial</Typography>
                  <Typography variant="body2">{lote!.cantidadInicial}</Typography>
                </Box>
                <Box>
                  <Typography variant="caption" color="text.secondary">Cantidad disponible</Typography>
                  <Typography variant="body2" fontWeight={600}>{lote!.cantidadDisponible}</Typography>
                </Box>
                {lote!.fechaVencimiento && (
                  <Box>
                    <Typography variant="caption" color="text.secondary">Fecha vencimiento</Typography>
                    <Typography variant="body2">{lote!.fechaVencimiento}</Typography>
                  </Box>
                )}
                <Box>
                  <Typography variant="caption" color="text.secondary">Fecha entrada</Typography>
                  <Typography variant="body2">{new Date(lote!.fechaEntrada).toLocaleDateString('es-CO')}</Typography>
                </Box>
              </Box>
            </Box>

            <Divider />

            {/* ── Entrada original ── */}
            <Box>
              <Typography variant="subtitle1" fontWeight={600} gutterBottom>Origen</Typography>
              {entrada ? (
                <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 2 }}>
                  {entradaTipoChip(entrada.tipo)}
                  <Box>
                    <Typography variant="body2">
                      <strong>{entrada.referencia}</strong>
                      {entrada.proveedor && ` — ${entrada.proveedor}`}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {new Date(entrada.fecha).toLocaleDateString('es-CO')} · {entrada.cantidadInicial} unidades · {formatCurrency(entrada.costoUnitario)} c/u
                    </Typography>
                  </Box>
                </Box>
              ) : (
                <Typography variant="body2" color="text.secondary">Sin información de origen registrada.</Typography>
              )}
            </Box>

            <Divider />

            {/* ── Movimientos ── */}
            <Box>
              <Typography variant="subtitle1" fontWeight={600} gutterBottom>
                Movimientos ({movimientos.length})
              </Typography>
              {movimientos.length === 0 ? (
                <Alert severity="info">Este lote no tiene movimientos registrados.</Alert>
              ) : (
                <TableContainer component={Paper} variant="outlined">
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell>Tipo</TableCell>
                        <TableCell>Referencia</TableCell>
                        <TableCell>Fecha</TableCell>
                        <TableCell align="right">Cantidad</TableCell>
                        <TableCell>Detalle</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {movimientos.map((m, i) => (
                        <TableRow key={i}>
                          <TableCell>{tipoChip(m.tipo)}</TableCell>
                          <TableCell>
                            <Typography variant="body2" fontFamily="monospace">{m.referencia}</Typography>
                          </TableCell>
                          <TableCell>{new Date(m.fecha).toLocaleDateString('es-CO')}</TableCell>
                          <TableCell align="right">
                            <Typography
                              variant="body2"
                              fontWeight={600}
                              color={m.tipo === 'Devolucion' ? 'success.main' : 'text.primary'}
                            >
                              {m.tipo === 'Devolucion' ? '+' : '-'}{m.cantidad}
                            </Typography>
                          </TableCell>
                          <TableCell>
                            <Typography variant="caption" color="text.secondary">{m.detalle || '—'}</Typography>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </TableContainer>
              )}
            </Box>
          </Stack>
        )}
      </DialogContent>

      <DialogActions>
        <Button onClick={onClose}>Cerrar</Button>
      </DialogActions>
    </Dialog>
  );
}
