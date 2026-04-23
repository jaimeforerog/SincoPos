import { useState } from 'react';
import {
  Box,
  Button,
  Typography,
  Paper,
  Chip,
  Divider,
  CircularProgress,
  TextField,
  Alert,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  IconButton,
  alpha,
} from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import ArrowForwardIcon from '@mui/icons-material/ArrowForward';
import SwapHorizIcon from '@mui/icons-material/SwapHoriz';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useSnackbar } from 'notistack';
import { trasladosApi } from '@/api/traslados';

const HERO_COLOR = '#1565c0';

const apiMsg = (e: unknown, fallback: string) =>
  (e as { response?: { data?: { error?: string } } })?.response?.data?.error ?? fallback;

const ESTADO_META: Record<string, { color: 'warning' | 'info' | 'success' | 'error' | 'default'; label: string }> = {
  Pendiente:  { color: 'warning', label: 'Pendiente' },
  EnTransito: { color: 'info',    label: 'En Tránsito' },
  Recibido:   { color: 'success', label: 'Recibido' },
  Rechazado:  { color: 'error',   label: 'Rechazado' },
  Cancelado:  { color: 'error',   label: 'Cancelado' },
};

const formatFecha = (fecha?: string) => {
  if (!fecha) return '-';
  return new Date(fecha).toLocaleString('es-CO', {
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit',
  });
};

interface Props {
  trasladoId: number;
  onBack: () => void;
}

type Accion = 'envio' | 'recibo' | 'rechazo' | 'cancelacion' | null;

export function DetallesTrasladoView({ trasladoId, onBack }: Props) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();
  const [accion, setAccion] = useState<Accion>(null);
  const [motivoRechazo, setMotivoRechazo] = useState('');
  const [motivoCancelacion, setMotivoCancelacion] = useState('');

  const { data: traslado, isLoading } = useQuery({
    queryKey: ['traslado', trasladoId],
    queryFn: () => trasladosApi.obtener(trasladoId),
  });

  const invalidar = () => {
    queryClient.invalidateQueries({ queryKey: ['traslado', trasladoId] });
    queryClient.invalidateQueries({ queryKey: ['traslados'] });
  };

  const enviarMutation = useMutation({
    mutationFn: () => trasladosApi.enviar(trasladoId),
    onSuccess: () => {
      enqueueSnackbar('Traslado enviado exitosamente', { variant: 'success' });
      invalidar();
      setAccion(null);
    },
    onError: (e: unknown) =>
      enqueueSnackbar(apiMsg(e, 'Error al enviar el traslado'), { variant: 'error' }),
  });

  const recibirMutation = useMutation({
    mutationFn: () => {
      if (!traslado) throw new Error('Sin datos');
      return trasladosApi.recibir(trasladoId, {
        lineas: traslado.detalles.map((d) => ({
          productoId: d.productoId,
          cantidadRecibida: d.cantidadSolicitada,
          observaciones: null,
        })),
        observaciones: null,
      });
    },
    onSuccess: () => {
      enqueueSnackbar('Traslado recibido exitosamente', { variant: 'success' });
      invalidar();
      setAccion(null);
      onBack();
    },
    onError: (e: unknown) =>
      enqueueSnackbar(apiMsg(e, 'Error al recibir el traslado'), { variant: 'error' }),
  });

  const rechazarMutation = useMutation({
    mutationFn: (motivo: string) => trasladosApi.rechazar(trasladoId, { motivo }),
    onSuccess: () => {
      enqueueSnackbar('Traslado rechazado', { variant: 'info' });
      invalidar();
      setAccion(null);
      setMotivoRechazo('');
      onBack();
    },
    onError: (e: unknown) =>
      enqueueSnackbar(apiMsg(e, 'Error al rechazar el traslado'), { variant: 'error' }),
  });

  const cancelarMutation = useMutation({
    mutationFn: (motivo: string) => trasladosApi.cancelar(trasladoId, { motivo }),
    onSuccess: () => {
      enqueueSnackbar('Traslado cancelado', { variant: 'info' });
      invalidar();
      setAccion(null);
      setMotivoCancelacion('');
      onBack();
    },
    onError: (e: unknown) =>
      enqueueSnackbar(apiMsg(e, 'Error al cancelar el traslado'), { variant: 'error' }),
  });

  const isPending =
    enviarMutation.isPending ||
    recibirMutation.isPending ||
    rechazarMutation.isPending ||
    cancelarMutation.isPending;

  if (isLoading || !traslado) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: 300 }}>
        <CircularProgress />
      </Box>
    );
  }

  const estadoMeta = ESTADO_META[traslado.estado] ?? { color: 'default' as const, label: traslado.estado };
  const editable = traslado.estado === 'Pendiente' || traslado.estado === 'EnTransito';

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      {/* Header */}
      <Box
        sx={{
          background: `linear-gradient(135deg, ${HERO_COLOR} 0%, #0d47a1 50%, #01579b 100%)`,
          borderRadius: 3,
          px: { xs: 2, md: 3 },
          py: 1.5,
          display: 'flex',
          alignItems: 'center',
          gap: 2,
          flexWrap: 'wrap',
        }}
      >
        <IconButton
          onClick={onBack}
          aria-label="regresar"
          sx={{ color: '#fff', '&:hover': { bgcolor: 'rgba(255,255,255,0.15)' } }}
        >
          <ArrowBackIcon />
        </IconButton>
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
            <Typography
              variant="h6"
              fontWeight={700}
              sx={{ color: '#fff', lineHeight: 1.2, fontFamily: 'monospace' }}
            >
              {traslado.numeroTraslado}
            </Typography>
            <Chip
              label={estadoMeta.label}
              color={estadoMeta.color}
              size="small"
              sx={{ fontWeight: 700 }}
            />
          </Box>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, mt: 0.25 }}>
            <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.75)' }}>
              {traslado.nombreSucursalOrigen}
            </Typography>
            <ArrowForwardIcon sx={{ fontSize: 12, color: 'rgba(255,255,255,0.5)' }} />
            <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.75)' }}>
              {traslado.nombreSucursalDestino}
            </Typography>
          </Box>
        </Box>
        <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.65)' }}>
          {formatFecha(traslado.fechaTraslado)}
        </Typography>
      </Box>

      {/* Info + Tabla */}
      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: { xs: '1fr', md: '260px 1fr' },
          gap: 2,
          alignItems: 'start',
        }}
      >
        {/* Info panel */}
        <Paper
          sx={{
            p: 2.5,
            borderRadius: 2,
            border: '1px solid',
            borderColor: 'divider',
          }}
        >
          <Typography
            variant="caption"
            fontWeight={700}
            color="text.secondary"
            sx={{ textTransform: 'uppercase', letterSpacing: '0.06em', display: 'block', mb: 2 }}
          >
            Información
          </Typography>

          {[
            { label: 'Número', content: <Typography variant="body2" fontFamily="monospace" fontWeight={700}>{traslado.numeroTraslado}</Typography> },
            { label: 'Creación', content: <Typography variant="body2">{formatFecha(traslado.fechaTraslado)}</Typography> },
            { label: 'Origen', content: <Typography variant="body2" fontWeight={600}>{traslado.nombreSucursalOrigen}</Typography> },
            { label: 'Destino', content: <Typography variant="body2" fontWeight={600}>{traslado.nombreSucursalDestino}</Typography> },
            traslado.fechaEnvio
              ? { label: 'Enviado', content: <Typography variant="body2">{formatFecha(traslado.fechaEnvio)}</Typography> }
              : null,
            traslado.fechaRecepcion
              ? { label: 'Recibido', content: <Typography variant="body2">{formatFecha(traslado.fechaRecepcion)}</Typography> }
              : null,
          ]
            .filter(Boolean)
            .map((item) => (
              <Box key={item!.label} sx={{ mb: 1.5 }}>
                <Typography variant="caption" color="text.secondary" display="block">
                  {item!.label}
                </Typography>
                {item!.content}
              </Box>
            ))}

          {traslado.observaciones && (
            <>
              <Divider sx={{ my: 1.5 }} />
              <Typography variant="caption" color="text.secondary" display="block">
                Observaciones
              </Typography>
              <Typography variant="body2">{traslado.observaciones}</Typography>
            </>
          )}
        </Paper>

        {/* Products table POS-style */}
        <Paper
          sx={{
            borderRadius: 2,
            border: '1px solid',
            borderColor: 'divider',
            overflow: 'hidden',
          }}
        >
          <Box
            sx={{
              px: 2,
              py: 1.25,
              borderBottom: '1px solid',
              borderColor: 'divider',
              background: alpha(HERO_COLOR, 0.04),
              display: 'flex',
              alignItems: 'center',
            }}
          >
            <Typography variant="subtitle2" fontWeight={700} color={HERO_COLOR} sx={{ flex: 1 }}>
              Productos ({traslado.detalles.length})
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {traslado.detalles.reduce((a, d) => a + d.cantidadSolicitada, 0)} unidades totales
            </Typography>
          </Box>

          <TableContainer>
            <Table size="small" stickyHeader sx={{ tableLayout: 'fixed', width: '100%' }}>
              <TableHead>
                <TableRow
                  sx={{
                    '& .MuiTableCell-head': {
                      color: HERO_COLOR,
                      fontWeight: 700,
                      fontSize: '0.72rem',
                      textTransform: 'uppercase',
                      letterSpacing: '0.04em',
                      borderBottom: `2px solid ${alpha(HERO_COLOR, 0.2)}`,
                      bgcolor: alpha(HERO_COLOR, 0.06),
                      py: 0.75,
                    },
                  }}
                >
                  <TableCell sx={{ width: 36 }}>#</TableCell>
                  <TableCell>Producto</TableCell>
                  <TableCell sx={{ width: 150 }}>Código</TableCell>
                  <TableCell align="center" sx={{ width: 120 }}>
                    Solicitado
                  </TableCell>
                  {traslado.estado === 'Recibido' && (
                    <TableCell align="center" sx={{ width: 110 }}>
                      Recibido
                    </TableCell>
                  )}
                </TableRow>
              </TableHead>
              <TableBody>
                {traslado.detalles.map((detalle, idx) => (
                  <TableRow
                    key={detalle.id}
                    sx={{
                      '&:hover': { bgcolor: alpha(HERO_COLOR, 0.03) },
                      '&:last-child td': { borderBottom: 0 },
                    }}
                  >
                    <TableCell sx={{ py: 0.75, color: 'text.disabled', fontSize: '0.75rem' }}>
                      {idx + 1}
                    </TableCell>
                    <TableCell sx={{ py: 0.75 }}>
                      <Typography variant="body2" fontWeight={600}>
                        {detalle.nombreProducto}
                      </Typography>
                    </TableCell>
                    <TableCell
                      sx={{ py: 0.75, fontFamily: 'monospace', fontSize: '0.75rem', color: 'text.secondary' }}
                    >
                      {detalle.codigoBarras}
                    </TableCell>
                    <TableCell align="center" sx={{ py: 0.75 }}>
                      <Typography variant="body2" fontWeight={700}>
                        {detalle.cantidadSolicitada}
                      </Typography>
                    </TableCell>
                    {traslado.estado === 'Recibido' && (
                      <TableCell align="center" sx={{ py: 0.75 }}>
                        <Typography
                          variant="body2"
                          fontWeight={700}
                          color={
                            detalle.cantidadRecibida === detalle.cantidadSolicitada
                              ? 'success.main'
                              : 'warning.main'
                          }
                        >
                          {detalle.cantidadRecibida}
                        </Typography>
                      </TableCell>
                    )}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>

          {traslado.detalles.length === 0 && (
            <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', py: 5 }}>
              <SwapHorizIcon sx={{ fontSize: 36, color: 'text.disabled', mr: 1 }} />
              <Typography variant="body2" color="text.secondary">
                Sin productos en este traslado
              </Typography>
            </Box>
          )}
        </Paper>
      </Box>

      {/* Action bar */}
      {editable && accion === null && (
        <Box
          sx={{
            display: 'flex',
            gap: 2,
            justifyContent: 'flex-end',
            p: 2,
            bgcolor: 'background.paper',
            borderRadius: 2,
            border: '1px solid',
            borderColor: 'divider',
          }}
        >
          {traslado.estado === 'Pendiente' && (
            <>
              <Button variant="outlined" color="error" onClick={() => setAccion('cancelacion')}>
                Cancelar Traslado
              </Button>
              <Button variant="contained" onClick={() => setAccion('envio')}>
                Marcar En Tránsito
              </Button>
            </>
          )}
          {traslado.estado === 'EnTransito' && (
            <>
              <Button variant="outlined" color="error" onClick={() => setAccion('rechazo')}>
                Rechazar
              </Button>
              <Button variant="contained" color="success" onClick={() => setAccion('recibo')}>
                Confirmar Recepción
              </Button>
            </>
          )}
        </Box>
      )}

      {accion === 'envio' && (
        <Alert
          severity="warning"
          sx={{ borderRadius: 2 }}
          action={
            <Box sx={{ display: 'flex', gap: 1 }}>
              <Button size="small" onClick={() => setAccion(null)}>
                Cancelar
              </Button>
              <Button
                size="small"
                variant="contained"
                disabled={isPending}
                onClick={() => enviarMutation.mutate()}
              >
                Confirmar Envío
              </Button>
            </Box>
          }
        >
          ¿Confirma el envío de <strong>{traslado.numeroTraslado}</strong>? Quedará En Tránsito.
        </Alert>
      )}

      {accion === 'recibo' && (
        <Alert
          severity="info"
          sx={{ borderRadius: 2 }}
          action={
            <Box sx={{ display: 'flex', gap: 1 }}>
              <Button size="small" onClick={() => setAccion(null)}>
                Cancelar
              </Button>
              <Button
                size="small"
                variant="contained"
                color="success"
                disabled={isPending}
                onClick={() => recibirMutation.mutate()}
              >
                Confirmar Recepción
              </Button>
            </Box>
          }
        >
          ¿Confirma la recepción completa de <strong>{traslado.numeroTraslado}</strong>? Esto actualizará el inventario.
        </Alert>
      )}

      {accion === 'rechazo' && (
        <Paper sx={{ p: 2, borderRadius: 2, border: '1px solid', borderColor: 'error.light' }}>
          <Typography variant="subtitle2" color="error" fontWeight={700} sx={{ mb: 1.5 }}>
            Motivo del Rechazo
          </Typography>
          <TextField
            fullWidth
            multiline
            rows={2}
            size="small"
            value={motivoRechazo}
            onChange={(e) => setMotivoRechazo(e.target.value)}
            placeholder="Describe el motivo del rechazo..."
          />
          <Box sx={{ display: 'flex', gap: 1, justifyContent: 'flex-end', mt: 1.5 }}>
            <Button onClick={() => { setAccion(null); setMotivoRechazo(''); }}>
              Cancelar
            </Button>
            <Button
              variant="contained"
              color="error"
              disabled={!motivoRechazo || isPending}
              onClick={() => rechazarMutation.mutate(motivoRechazo)}
            >
              Confirmar Rechazo
            </Button>
          </Box>
        </Paper>
      )}

      {accion === 'cancelacion' && (
        <Paper sx={{ p: 2, borderRadius: 2, border: '1px solid', borderColor: 'error.light' }}>
          <Typography variant="subtitle2" color="error" fontWeight={700} sx={{ mb: 1.5 }}>
            Motivo de la Cancelación
          </Typography>
          <TextField
            fullWidth
            multiline
            rows={2}
            size="small"
            value={motivoCancelacion}
            onChange={(e) => setMotivoCancelacion(e.target.value)}
            placeholder="Describe el motivo de la cancelación..."
          />
          <Box sx={{ display: 'flex', gap: 1, justifyContent: 'flex-end', mt: 1.5 }}>
            <Button onClick={() => { setAccion(null); setMotivoCancelacion(''); }}>
              Cancelar
            </Button>
            <Button
              variant="contained"
              color="error"
              disabled={!motivoCancelacion || isPending}
              onClick={() => cancelarMutation.mutate(motivoCancelacion)}
            >
              Confirmar Cancelación
            </Button>
          </Box>
        </Paper>
      )}
    </Box>
  );
}
