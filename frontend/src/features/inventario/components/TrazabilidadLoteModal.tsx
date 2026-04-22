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
  AddCircleOutline,
} from '@mui/icons-material';
import { lotesApi } from '@/api/lotes';
import { formatCurrency, formatDateOnly } from '@/utils/format';

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

function esEntrada(tipo: string) {
  return tipo === 'Devolucion';
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
        Kardex de lote
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
          <Alert severity="error">No se pudo cargar el kardex del lote.</Alert>
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
                  <Typography variant="caption" color="text.secondary">Saldo actual</Typography>
                  <Typography
                    variant="body2"
                    fontWeight={700}
                    color={lote!.cantidadDisponible > 0 ? 'success.main' : 'text.secondary'}
                  >
                    {lote!.cantidadDisponible}
                  </Typography>
                </Box>
                {lote!.fechaVencimiento && (
                  <Box>
                    <Typography variant="caption" color="text.secondary">Fecha vencimiento</Typography>
                    <Typography variant="body2">{lote!.fechaVencimiento}</Typography>
                  </Box>
                )}
                <Box>
                  <Typography variant="caption" color="text.secondary">Fecha entrada</Typography>
                  <Typography variant="body2">{formatDateOnly(lote!.fechaEntrada)}</Typography>
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

            {/* ── Kardex ── */}
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
                      <TableRow sx={{ bgcolor: 'grey.50' }}>
                        <TableCell>Tipo</TableCell>
                        <TableCell>Referencia</TableCell>
                        <TableCell>Fecha</TableCell>
                        <TableCell align="right">Entrada</TableCell>
                        <TableCell align="right">Salida</TableCell>
                        <TableCell align="right">Saldo</TableCell>
                        <TableCell>Detalle</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {/* Fila de apertura con la entrada inicial */}
                      {entrada && (
                        <TableRow sx={{ bgcolor: 'success.50' }}>
                          <TableCell>
                            <Chip icon={<AddCircleOutline fontSize="small" />} label="Apertura" color="success" size="small" />
                          </TableCell>
                          <TableCell>
                            <Typography variant="body2" fontFamily="monospace">{entrada.referencia}</Typography>
                          </TableCell>
                          <TableCell>
                            <Typography variant="body2">{new Date(entrada.fecha).toLocaleDateString('es-CO')}</Typography>
                          </TableCell>
                          <TableCell align="right">
                            <Typography variant="body2" fontWeight={600} color="success.main">
                              +{entrada.cantidadInicial}
                            </Typography>
                          </TableCell>
                          <TableCell />
                          <TableCell align="right">
                            <Typography variant="body2" fontWeight={700}>
                              {lote!.cantidadInicial}
                            </Typography>
                          </TableCell>
                          <TableCell>
                            <Typography variant="caption" color="text.secondary">
                              {formatCurrency(entrada.costoUnitario)} c/u
                              {entrada.proveedor && ` · ${entrada.proveedor}`}
                            </Typography>
                          </TableCell>
                        </TableRow>
                      )}

                      {movimientos.map((m, i) => {
                        const entrada_ = esEntrada(m.tipo);
                        return (
                          <TableRow key={i} hover>
                            <TableCell>{tipoChip(m.tipo)}</TableCell>
                            <TableCell>
                              <Typography variant="body2" fontFamily="monospace">{m.referencia}</Typography>
                            </TableCell>
                            <TableCell>
                              <Typography variant="body2">{new Date(m.fecha).toLocaleDateString('es-CO')}</Typography>
                            </TableCell>
                            <TableCell align="right">
                              {entrada_ && (
                                <Typography variant="body2" fontWeight={600} color="success.main">
                                  +{m.cantidad}
                                </Typography>
                              )}
                            </TableCell>
                            <TableCell align="right">
                              {!entrada_ && (
                                <Typography variant="body2" fontWeight={600} color="error.main">
                                  -{m.cantidad}
                                </Typography>
                              )}
                            </TableCell>
                            <TableCell align="right">
                              <Typography
                                variant="body2"
                                fontWeight={700}
                                color={m.saldo <= 0 ? 'text.disabled' : 'text.primary'}
                              >
                                {m.saldo}
                              </Typography>
                            </TableCell>
                            <TableCell>
                              <Typography variant="caption" color="text.secondary">{m.detalle || '—'}</Typography>
                            </TableCell>
                          </TableRow>
                        );
                      })}
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
