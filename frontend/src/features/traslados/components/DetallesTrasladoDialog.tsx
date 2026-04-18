import { useState } from 'react';
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
  CircularProgress,
  TextField,
  Alert,
} from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useSnackbar } from 'notistack';
import { trasladosApi } from '@/api/traslados';
import type { ApiError } from '@/types/api';

interface Props {
  open: boolean;
  trasladoId: number;
  onClose: () => void;
}

export function DetallesTrasladoDialog({ open, trasladoId, onClose }: Props) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();
  const [motivoRechazo, setMotivoRechazo] = useState('');
  const [motivoCancelacion, setMotivoCancelacion] = useState('');
  const [mostrarRechazo, setMostrarRechazo] = useState(false);
  const [mostrarCancelacion, setMostrarCancelacion] = useState(false);
  const [confirmarEnvio, setConfirmarEnvio] = useState(false);
  const [confirmarRecibo, setConfirmarRecibo] = useState(false);

  const { data: traslado, isLoading } = useQuery({
    queryKey: ['traslado', trasladoId],
    queryFn: () => trasladosApi.obtener(trasladoId),
    enabled: open,
  });

  const enviarMutation = useMutation({
    mutationFn: () => trasladosApi.enviar(trasladoId),
    onSuccess: () => {
      enqueueSnackbar('Traslado enviado exitosamente', { variant: 'success' });
      queryClient.invalidateQueries({ queryKey: ['traslado', trasladoId] });
      queryClient.invalidateQueries({ queryKey: ['traslados'] });
    },
    onError: (error: ApiError) => {
      const errorMessage = error?.response?.data?.error || 'Error al enviar el traslado';
      enqueueSnackbar(errorMessage, { variant: 'error' });
    },
  });

  const recibirMutation = useMutation({
    mutationFn: () => {
      if (!traslado) throw new Error('No traslado data');

      const dto = {
        lineas: traslado.detalles.map((d) => ({
          productoId: d.productoId,
          cantidadRecibida: d.cantidadSolicitada,
          observaciones: null,
        })),
        observaciones: null,
      };

      return trasladosApi.recibir(trasladoId, dto);
    },
    onSuccess: () => {
      enqueueSnackbar('Traslado recibido exitosamente', { variant: 'success' });
      queryClient.invalidateQueries({ queryKey: ['traslado', trasladoId] });
      queryClient.invalidateQueries({ queryKey: ['traslados'] });
      onClose();
    },
    onError: (error: ApiError) => {
      const errorMessage = error?.response?.data?.error || 'Error al recibir el traslado';
      enqueueSnackbar(errorMessage, { variant: 'error' });
    },
  });

  const rechazarMutation = useMutation({
    mutationFn: (motivo: string) => trasladosApi.rechazar(trasladoId, { motivo }),
    onSuccess: () => {
      enqueueSnackbar('Traslado rechazado', { variant: 'info' });
      queryClient.invalidateQueries({ queryKey: ['traslado', trasladoId] });
      queryClient.invalidateQueries({ queryKey: ['traslados'] });
      setMostrarRechazo(false);
      setMotivoRechazo('');
      onClose();
    },
    onError: (error: ApiError) => {
      const errorMessage = error?.response?.data?.error || 'Error al rechazar el traslado';
      enqueueSnackbar(errorMessage, { variant: 'error' });
    },
  });

  const cancelarMutation = useMutation({
    mutationFn: (motivo: string) => trasladosApi.cancelar(trasladoId, { motivo }),
    onSuccess: () => {
      enqueueSnackbar('Traslado cancelado', { variant: 'info' });
      queryClient.invalidateQueries({ queryKey: ['traslado', trasladoId] });
      queryClient.invalidateQueries({ queryKey: ['traslados'] });
      setMostrarCancelacion(false);
      setMotivoCancelacion('');
      onClose();
    },
    onError: (error: ApiError) => {
      const errorMessage = error?.response?.data?.error || 'Error al cancelar el traslado';
      enqueueSnackbar(errorMessage, { variant: 'error' });
    },
  });

  const formatFecha = (fecha?: string) => {
    if (!fecha) return '-';
    return new Date(fecha).toLocaleString('es-CO');
  };

  if (isLoading || !traslado) {
    return (
      <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
        <DialogContent>
          <Box sx={{ display: 'flex', justifyContent: 'center', p: 4 }}>
            <CircularProgress />
          </Box>
        </DialogContent>
      </Dialog>
    );
  }

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <Typography variant="h6">Detalles del Traslado</Typography>
          <Chip label={traslado.estado} color="primary" />
        </Box>
      </DialogTitle>
      <DialogContent>
        <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 2, mb: 3 }}>
          <Box>
            <Typography variant="caption" color="text.secondary">
              Número de Traslado
            </Typography>
            <Typography variant="body1" fontFamily="monospace" fontWeight={600}>
              {traslado.numeroTraslado}
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" color="text.secondary">
              Fecha de Creación
            </Typography>
            <Typography variant="body1">{formatFecha(traslado.fechaTraslado)}</Typography>
          </Box>
          <Box>
            <Typography variant="caption" color="text.secondary">
              Sucursal Origen
            </Typography>
            <Typography variant="body1" fontWeight={600}>
              {traslado.nombreSucursalOrigen}
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" color="text.secondary">
              Sucursal Destino
            </Typography>
            <Typography variant="body1" fontWeight={600}>
              {traslado.nombreSucursalDestino}
            </Typography>
          </Box>
          {traslado.fechaEnvio && (
            <Box>
              <Typography variant="caption" color="text.secondary">
                Fecha de Envío
              </Typography>
              <Typography variant="body1">{formatFecha(traslado.fechaEnvio)}</Typography>
            </Box>
          )}
          {traslado.fechaRecepcion && (
            <Box>
              <Typography variant="caption" color="text.secondary">
                Fecha de Recepción
              </Typography>
              <Typography variant="body1">{formatFecha(traslado.fechaRecepcion)}</Typography>
            </Box>
          )}
        </Box>

        {traslado.observaciones && (
          <Box sx={{ mb: 3 }}>
            <Typography variant="caption" color="text.secondary">
              Observaciones
            </Typography>
            <Typography variant="body2">{traslado.observaciones}</Typography>
          </Box>
        )}

        <Divider sx={{ my: 2 }} />

        <Typography variant="subtitle1" sx={{ mb: 2, fontWeight: 600 }}>
          Productos ({traslado.detalles.length})
        </Typography>

        <TableContainer component={Paper} variant="outlined">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Producto</TableCell>
                <TableCell>Código</TableCell>
                <TableCell align="center">Cantidad Solicitada</TableCell>
                {traslado.estado === 'Recibido' && (
                  <TableCell align="center">Cantidad Recibida</TableCell>
                )}
              </TableRow>
            </TableHead>
            <TableBody>
              {traslado.detalles.map((detalle) => (
                <TableRow key={detalle.id}>
                  <TableCell>{detalle.nombreProducto}</TableCell>
                  <TableCell sx={{ fontFamily: 'monospace' }}>{detalle.codigoBarras}</TableCell>
                  <TableCell align="center">{detalle.cantidadSolicitada}</TableCell>
                  {traslado.estado === 'Recibido' && (
                    <TableCell align="center">{detalle.cantidadRecibida}</TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>

        {mostrarRechazo && (
          <Box sx={{ mt: 3 }}>
            <TextField
              fullWidth
              label="Motivo del Rechazo"
              multiline
              rows={2}
              value={motivoRechazo}
              onChange={(e) => setMotivoRechazo(e.target.value)}
              required
            />
          </Box>
        )}

        {mostrarCancelacion && (
          <Box sx={{ mt: 3 }}>
            <TextField
              fullWidth
              label="Motivo de la Cancelación"
              multiline
              rows={2}
              value={motivoCancelacion}
              onChange={(e) => setMotivoCancelacion(e.target.value)}
              required
            />
          </Box>
        )}
      </DialogContent>
      <DialogActions>
        {!mostrarRechazo && !mostrarCancelacion && !confirmarEnvio && !confirmarRecibo && (
          <>
            <Button onClick={onClose}>Cerrar</Button>
            {traslado.estado === 'Pendiente' && (
              <>
                <Button
                  variant="outlined"
                  color="error"
                  onClick={() => setMostrarCancelacion(true)}
                >
                  Cancelar Traslado
                </Button>
                <Button
                  variant="contained"
                  onClick={() => setConfirmarEnvio(true)}
                >
                  Enviar
                </Button>
              </>
            )}
            {traslado.estado === 'EnTransito' && (
              <>
                <Button
                  variant="outlined"
                  color="error"
                  onClick={() => setMostrarRechazo(true)}
                >
                  Rechazar
                </Button>
                <Button
                  variant="contained"
                  color="success"
                  onClick={() => setConfirmarRecibo(true)}
                >
                  Recibir
                </Button>
              </>
            )}
          </>
        )}

        {confirmarEnvio && (
          <>
            <Alert severity="warning" sx={{ flex: 1, mr: 1 }}>
              ¿Confirma el envío de <strong>{traslado.numeroTraslado}</strong>? El traslado quedará En Tránsito.
            </Alert>
            <Button onClick={() => setConfirmarEnvio(false)}>
              Cancelar
            </Button>
            <Button
              variant="contained"
              onClick={() => { setConfirmarEnvio(false); enviarMutation.mutate(); }}
              disabled={enviarMutation.isPending}
            >
              Confirmar Envío
            </Button>
          </>
        )}

        {confirmarRecibo && (
          <>
            <Alert severity="warning" sx={{ flex: 1, mr: 1 }}>
              ¿Confirma la recepción completa de <strong>{traslado.numeroTraslado}</strong>? Esto actualizará el inventario.
            </Alert>
            <Button onClick={() => setConfirmarRecibo(false)}>
              Cancelar
            </Button>
            <Button
              variant="contained"
              color="success"
              onClick={() => { setConfirmarRecibo(false); recibirMutation.mutate(); }}
              disabled={recibirMutation.isPending}
            >
              Confirmar Recepción
            </Button>
          </>
        )}

        {mostrarRechazo && (
          <>
            <Button onClick={() => { setMostrarRechazo(false); setMotivoRechazo(''); }}>
              Cancelar
            </Button>
            <Button
              variant="contained"
              color="error"
              onClick={() => rechazarMutation.mutate(motivoRechazo)}
              disabled={!motivoRechazo || rechazarMutation.isPending}
            >
              Confirmar Rechazo
            </Button>
          </>
        )}

        {mostrarCancelacion && (
          <>
            <Button onClick={() => { setMostrarCancelacion(false); setMotivoCancelacion(''); }}>
              Cancelar
            </Button>
            <Button
              variant="contained"
              color="error"
              onClick={() => cancelarMutation.mutate(motivoCancelacion)}
              disabled={!motivoCancelacion || cancelarMutation.isPending}
            >
              Confirmar Cancelación
            </Button>
          </>
        )}
      </DialogActions>
    </Dialog>
  );
}
