import { useState } from 'react';
import {
  Box,
  Button,
  Paper,
  TextField,
  Alert,
  Typography,
} from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useSnackbar } from 'notistack';
import { comprasApi } from '@/api/compras';
import type { OrdenCompraDTO } from '@/types/api';

interface AccionProps {
  orden: OrdenCompraDTO;
  onCancel: () => void;
  onDone: () => void;
}

// ── Aprobar ──────────────────────────────────────────────────────────────────

export function AccionAprobar({ orden, onCancel, onDone }: AccionProps) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();
  const [observaciones, setObservaciones] = useState('');

  const mutation = useMutation({
    mutationFn: () => comprasApi.aprobar(orden.id, { observaciones: observaciones || undefined }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['compra', orden.id] });
      queryClient.invalidateQueries({ queryKey: ['compras'] });
      enqueueSnackbar('Orden aprobada exitosamente', { variant: 'success' });
      onDone();
    },
    onError: (error: any) => {
      const statusCode: number = error?.statusCode ?? 0;
      const msg: string = error?.message ?? '';
      let mensaje = 'Error al aprobar la orden';
      if (statusCode === 400) mensaje = msg || 'No se puede aprobar esta orden. Verifica su estado actual.';
      else if (statusCode === 403) mensaje = 'No tienes permisos para aprobar órdenes. Se requiere rol Supervisor.';
      else if (statusCode === 404) mensaje = 'Orden de compra no encontrada.';
      else if (statusCode >= 500) mensaje = msg || 'Error interno del servidor.';
      else if (msg) mensaje = msg;
      enqueueSnackbar(mensaje, { variant: 'error' });
    },
  });

  return (
    <Paper
      variant="outlined"
      sx={{ p: 2.5, borderColor: 'success.main', borderWidth: 1.5, borderRadius: 2 }}
    >
      <Typography variant="subtitle1" fontWeight={700} color="success.dark" sx={{ mb: 1.5 }}>
        Aprobar Orden
      </Typography>
      <Alert severity="info" sx={{ mb: 2 }}>
        ¿Está seguro que desea aprobar la orden <strong>{orden.numeroOrden}</strong>?
      </Alert>
      <TextField
        label="Observaciones (opcional)"
        multiline
        rows={2}
        fullWidth
        size="small"
        value={observaciones}
        onChange={(e) => setObservaciones(e.target.value)}
        sx={{ mb: 2 }}
      />
      <Box sx={{ display: 'flex', gap: 1, justifyContent: 'flex-end' }}>
        <Button onClick={onCancel} disabled={mutation.isPending}>
          Cancelar
        </Button>
        <Button
          variant="contained"
          color="success"
          disabled={mutation.isPending}
          onClick={() => mutation.mutate()}
        >
          {mutation.isPending ? 'Aprobando...' : 'Aprobar'}
        </Button>
      </Box>
    </Paper>
  );
}

// ── Rechazar ─────────────────────────────────────────────────────────────────

export function AccionRechazar({ orden, onCancel, onDone }: AccionProps) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();
  const [motivo, setMotivo] = useState('');
  const [motivoError, setMotivoError] = useState('');

  const mutation = useMutation({
    mutationFn: () => comprasApi.rechazar(orden.id, { motivoRechazo: motivo }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['compra', orden.id] });
      queryClient.invalidateQueries({ queryKey: ['compras'] });
      enqueueSnackbar('Orden rechazada', { variant: 'info' });
      onDone();
    },
    onError: (error: any) => {
      const statusCode: number = error?.statusCode ?? 0;
      const msg: string = error?.message ?? '';
      let mensaje = 'Error al rechazar la orden';
      if (statusCode === 400) mensaje = msg || 'No se puede rechazar esta orden.';
      else if (statusCode === 403) mensaje = 'No tienes permisos para rechazar órdenes.';
      else if (statusCode === 404) mensaje = 'Orden de compra no encontrada.';
      else if (statusCode >= 500) mensaje = msg || 'Error interno del servidor.';
      else if (msg) mensaje = msg;
      enqueueSnackbar(mensaje, { variant: 'error' });
    },
  });

  const handleSubmit = () => {
    if (motivo.trim().length < 5) {
      setMotivoError('El motivo debe tener al menos 5 caracteres');
      return;
    }
    setMotivoError('');
    mutation.mutate();
  };

  return (
    <Paper
      variant="outlined"
      sx={{ p: 2.5, borderColor: 'error.main', borderWidth: 1.5, borderRadius: 2 }}
    >
      <Typography variant="subtitle1" fontWeight={700} color="error.dark" sx={{ mb: 1.5 }}>
        Rechazar Orden
      </Typography>
      <Alert severity="warning" sx={{ mb: 2 }}>
        Esta acción rechazará la orden <strong>{orden.numeroOrden}</strong>. No se podrá deshacer.
      </Alert>
      <TextField
        label="Motivo de rechazo *"
        multiline
        rows={2}
        fullWidth
        size="small"
        value={motivo}
        onChange={(e) => { setMotivo(e.target.value); if (motivoError) setMotivoError(''); }}
        error={!!motivoError}
        helperText={motivoError}
        sx={{ mb: 2 }}
      />
      <Box sx={{ display: 'flex', gap: 1, justifyContent: 'flex-end' }}>
        <Button onClick={onCancel} disabled={mutation.isPending}>
          Cancelar
        </Button>
        <Button
          variant="contained"
          color="error"
          disabled={mutation.isPending}
          onClick={handleSubmit}
        >
          {mutation.isPending ? 'Rechazando...' : 'Rechazar'}
        </Button>
      </Box>
    </Paper>
  );
}

// ── Cancelar ─────────────────────────────────────────────────────────────────

export function AccionCancelar({ orden, onCancel, onDone }: AccionProps) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();
  const [motivo, setMotivo] = useState('');
  const [motivoError, setMotivoError] = useState('');

  const mutation = useMutation({
    mutationFn: () => comprasApi.cancelar(orden.id, { motivo }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['compra', orden.id] });
      queryClient.invalidateQueries({ queryKey: ['compras'] });
      enqueueSnackbar('Orden cancelada', { variant: 'info' });
      onDone();
    },
    onError: (error: any) => {
      const statusCode: number = error?.statusCode ?? 0;
      const msg: string = error?.message ?? '';
      let mensaje = 'Error al cancelar la orden';
      if (statusCode === 400) mensaje = msg || 'No se puede cancelar esta orden.';
      else if (statusCode === 403) mensaje = 'No tienes permisos para cancelar órdenes.';
      else if (statusCode === 404) mensaje = 'Orden de compra no encontrada.';
      else if (statusCode >= 500) mensaje = msg || 'Error interno del servidor.';
      else if (msg) mensaje = msg;
      enqueueSnackbar(mensaje, { variant: 'error' });
    },
  });

  const handleSubmit = () => {
    if (motivo.trim().length < 5) {
      setMotivoError('El motivo debe tener al menos 5 caracteres');
      return;
    }
    setMotivoError('');
    mutation.mutate();
  };

  return (
    <Paper
      variant="outlined"
      sx={{ p: 2.5, borderColor: 'warning.main', borderWidth: 1.5, borderRadius: 2 }}
    >
      <Typography variant="subtitle1" fontWeight={700} color="warning.dark" sx={{ mb: 1.5 }}>
        Cancelar Orden
      </Typography>
      <Alert severity="warning" sx={{ mb: 2 }}>
        Esta acción cancelará la orden <strong>{orden.numeroOrden}</strong>. No se podrá deshacer.
      </Alert>
      <TextField
        label="Motivo de cancelación *"
        multiline
        rows={2}
        fullWidth
        size="small"
        value={motivo}
        onChange={(e) => { setMotivo(e.target.value); if (motivoError) setMotivoError(''); }}
        error={!!motivoError}
        helperText={motivoError}
        sx={{ mb: 2 }}
      />
      <Box sx={{ display: 'flex', gap: 1, justifyContent: 'flex-end' }}>
        <Button onClick={onCancel} disabled={mutation.isPending}>
          Cancelar
        </Button>
        <Button
          variant="contained"
          color="warning"
          disabled={mutation.isPending}
          onClick={handleSubmit}
        >
          {mutation.isPending ? 'Cancelando...' : 'Cancelar Orden'}
        </Button>
      </Box>
    </Paper>
  );
}
