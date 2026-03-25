import { useState, useEffect } from 'react';
import { useForm, Controller, useFieldArray } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
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
import LocalShippingIcon from '@mui/icons-material/LocalShipping';
import { useSnackbar } from 'notistack';
import { comprasApi } from '@/api/compras';
import type { OrdenCompraDTO } from '@/types/api';

const HERO_COLOR = '#1565c0';

type Accion = 'aprobar' | 'rechazar' | 'cancelar' | 'recibir' | null;

const ESTADO_META: Record<string, { color: 'warning' | 'info' | 'primary' | 'success' | 'error' | 'default'; label: string }> = {
  Pendiente:        { color: 'warning', label: 'Pendiente' },
  Aprobada:         { color: 'info',    label: 'Aprobada' },
  RecibidaParcial:  { color: 'primary', label: 'Rec. Parcial' },
  RecibidaCompleta: { color: 'success', label: 'Recibida Completa' },
  Rechazada:        { color: 'error',   label: 'Rechazada' },
  Cancelada:        { color: 'default', label: 'Cancelada' },
};

const ESTADOS_TERMINALES = ['RecibidaCompleta', 'Rechazada', 'Cancelada'];

const formatFecha = (fecha?: string) => {
  if (!fecha) return '—';
  return new Date(fecha).toLocaleDateString('es-CO');
};

// ── Schemas para recibir mercancía ──────────────────────────────────────────
const lineaRecepcionSchema = z.object({
  productoId: z.string(),
  cantidadRecibida: z.number().min(0, 'Cantidad debe ser mayor o igual a 0'),
  observaciones: z.string().optional(),
  numeroLote: z.string().optional(),
  fechaVencimiento: z.string().optional(),
});

const recibirSchema = z.object({
  fechaRecepcion: z.string().min(1, 'La fecha de recepción es requerida'),
  lineas: z.array(lineaRecepcionSchema),
});

type RecibirFormData = z.infer<typeof recibirSchema>;

interface Props {
  ordenId: number;
  onBack: () => void;
}

// ── Componente InfoRow ───────────────────────────────────────────────────────
function InfoRow({ label, value }: { label: string; value?: React.ReactNode }) {
  if (!value) return null;
  return (
    <Box sx={{ mb: 1.25 }}>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', lineHeight: 1.2, mb: 0.25 }}>
        {label}
      </Typography>
      <Typography variant="body2" fontWeight={500}>
        {value}
      </Typography>
    </Box>
  );
}

// ── Sección de acción: Aprobar ───────────────────────────────────────────────
function AccionAprobar({
  orden,
  onCancel,
  onDone,
}: {
  orden: OrdenCompraDTO;
  onCancel: () => void;
  onDone: () => void;
}) {
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

// ── Sección de acción: Rechazar ──────────────────────────────────────────────
function AccionRechazar({
  orden,
  onCancel,
  onDone,
}: {
  orden: OrdenCompraDTO;
  onCancel: () => void;
  onDone: () => void;
}) {
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

// ── Sección de acción: Cancelar ──────────────────────────────────────────────
function AccionCancelar({
  orden,
  onCancel,
  onDone,
}: {
  orden: OrdenCompraDTO;
  onCancel: () => void;
  onDone: () => void;
}) {
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

// ── Sección de acción: Recibir Mercancía ─────────────────────────────────────
function AccionRecibir({
  orden,
  onCancel,
  onDone,
}: {
  orden: OrdenCompraDTO;
  onCancel: () => void;
  onDone: () => void;
}) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();
  const today = new Date().toISOString().split('T')[0];
  const tieneLotes = orden.detalles.some((d) => d.manejaLotes);

  const {
    control,
    handleSubmit,
    reset,
    watch,
    formState: { errors },
  } = useForm<RecibirFormData>({
    resolver: zodResolver(recibirSchema),
    defaultValues: { fechaRecepcion: today, lineas: [] },
  });

  const { fields } = useFieldArray({ control, name: 'lineas' });

  useEffect(() => {
    const defaultFecha = (() => {
      if (orden.fechaEntregaEsperada) {
        const fe = orden.fechaEntregaEsperada.split('T')[0];
        return fe <= today ? fe : today;
      }
      return today;
    })();

    reset({
      fechaRecepcion: defaultFecha,
      lineas: orden.detalles.map((d) => {
        let fechaVencimientoDefault = '';
        if (d.manejaLotes && d.diasVidaUtil) {
          const fecha = new Date();
          fecha.setDate(fecha.getDate() + d.diasVidaUtil);
          fechaVencimientoDefault = fecha.toISOString().split('T')[0];
        }
        return {
          productoId: d.productoId,
          cantidadRecibida: d.cantidadSolicitada - d.cantidadRecibida,
          observaciones: '',
          numeroLote: '',
          fechaVencimiento: fechaVencimientoDefault,
        };
      }),
    });
  }, [orden, reset]); // eslint-disable-line react-hooks/exhaustive-deps

  const lineas = watch('lineas');
  const totalUnidades = lineas.reduce((sum, l) => sum + (l.cantidadRecibida || 0), 0);

  const mutation = useMutation({
    mutationFn: (data: RecibirFormData) => {
      const lineasRecibidas = data.lineas
        .filter((l) => l.cantidadRecibida > 0)
        .map((l) => ({
          productoId: l.productoId,
          cantidadRecibida: l.cantidadRecibida,
          observaciones: l.observaciones || undefined,
          numeroLote: l.numeroLote || undefined,
          fechaVencimiento: l.fechaVencimiento || undefined,
        }));
      return comprasApi.recibir(orden.id, { lineas: lineasRecibidas, fechaRecepcion: data.fechaRecepcion });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['compra', orden.id] });
      queryClient.invalidateQueries({ queryKey: ['compras'] });
      enqueueSnackbar('Mercancía recibida exitosamente', { variant: 'success' });
      onDone();
    },
    onError: (error: any) => {
      const statusCode: number = error?.statusCode ?? 0;
      const msg: string = error?.message ?? '';
      const errores: Record<string, string[]> | undefined = error?.errors;
      let mensaje = 'Error al recibir la mercancía';
      if (statusCode === 400) {
        if (errores && Object.keys(errores).length > 0) {
          const detalle = Object.entries(errores)
            .map(([campo, msgs]) => `${campo}: ${msgs.join(', ')}`)
            .join('\n');
          mensaje = `Errores de validación:\n${detalle}`;
        } else {
          mensaje = msg || 'Datos de recepción inválidos. Verifica las cantidades.';
        }
      } else if (statusCode === 403) {
        mensaje = 'No tienes permisos para recibir mercancía. Se requiere rol Supervisor.';
      } else if (statusCode === 404) {
        mensaje = msg || 'Orden de compra no encontrada.';
      } else if (statusCode >= 500) {
        mensaje = msg || 'Error interno del servidor.';
      } else if (statusCode === 0) {
        mensaje = msg || 'No se pudo conectar con el servidor.';
      } else if (msg) {
        mensaje = msg;
      }
      enqueueSnackbar(mensaje, { variant: 'error' });
    },
  });

  const onSubmit = (data: RecibirFormData) => {
    const lineasRecibidas = data.lineas.filter((l) => l.cantidadRecibida > 0);
    if (lineasRecibidas.length === 0) {
      enqueueSnackbar('Debe recibir al menos un producto', { variant: 'warning' });
      return;
    }
    mutation.mutate(data);
  };

  return (
    <Paper
      variant="outlined"
      sx={{ p: 2.5, borderColor: 'primary.main', borderWidth: 1.5, borderRadius: 2 }}
    >
      <Typography variant="subtitle1" fontWeight={700} color="primary.dark" sx={{ mb: 1.5 }}>
        Recibir Mercancía — {orden.numeroOrden}
      </Typography>

      <form onSubmit={handleSubmit(onSubmit)}>
        <Alert severity="info" sx={{ mb: 2 }}>
          Ingrese las cantidades recibidas para esta orden de <strong>{orden.formaPago}</strong>
          {orden.formaPago === 'Credito' ? ` (${orden.diasPlazo} días)` : ''}.
          {tieneLotes && (
            <> Los productos marcados con <Chip label="Lote" size="small" color="warning" sx={{ mx: 0.5 }} /> requieren número de lote y fecha de vencimiento.</>
          )}
        </Alert>

        <Controller
          name="fechaRecepcion"
          control={control}
          render={({ field }) => (
            <TextField
              {...field}
              type="date"
              label="Fecha de Recepción *"
              InputLabelProps={{ shrink: true }}
              inputProps={{ max: today }}
              error={!!errors.fechaRecepcion}
              helperText={errors.fechaRecepcion?.message}
              sx={{ mb: 2, width: 260 }}
              size="small"
            />
          )}
        />

        <TableContainer component={Paper} variant="outlined" sx={{ mb: 2 }}>
          <Table size="small">
            <TableHead>
              <TableRow
                sx={{
                  '& th': {
                    bgcolor: 'grey.50',
                    fontWeight: 700,
                    fontSize: '0.72rem',
                    textTransform: 'uppercase',
                    letterSpacing: '0.04em',
                    color: 'text.secondary',
                  },
                }}
              >
                <TableCell>Producto</TableCell>
                <TableCell align="center">Solicitada</TableCell>
                <TableCell align="center">Ya Recibida</TableCell>
                <TableCell align="center">Pendiente</TableCell>
                <TableCell align="center" width={110}>Recibir Ahora</TableCell>
                {tieneLotes && <TableCell width={140}>Nº Lote</TableCell>}
                {tieneLotes && <TableCell width={150}>Vencimiento</TableCell>}
                <TableCell>Obs.</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {fields.map((field, index) => {
                const detalle = orden.detalles[index];
                const pendiente = detalle.cantidadSolicitada - detalle.cantidadRecibida;

                return (
                  <TableRow key={field.id}>
                    <TableCell>
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                        <Typography variant="body2">{detalle.nombreProducto}</Typography>
                        {detalle.manejaLotes && (
                          <Chip label="Lote" size="small" color="warning" variant="outlined" />
                        )}
                      </Box>
                    </TableCell>
                    <TableCell align="center">{detalle.cantidadSolicitada}</TableCell>
                    <TableCell align="center">
                      <Typography
                        variant="body2"
                        color={detalle.cantidadRecibida > 0 ? 'success.main' : 'text.secondary'}
                      >
                        {detalle.cantidadRecibida}
                      </Typography>
                    </TableCell>
                    <TableCell align="center">
                      <Typography
                        variant="body2"
                        fontWeight="medium"
                        color={pendiente > 0 ? 'warning.main' : 'success.main'}
                      >
                        {pendiente}
                      </Typography>
                    </TableCell>
                    <TableCell align="center">
                      <Controller
                        name={`lineas.${index}.cantidadRecibida`}
                        control={control}
                        render={({ field: { value, onChange, ...f } }) => (
                          <TextField
                            {...f}
                            type="number"
                            value={value}
                            onChange={(e) => onChange(parseFloat(e.target.value) || 0)}
                            size="small"
                            sx={{ width: 90 }}
                            inputProps={{ min: 0, max: pendiente, step: 1 }}
                          />
                        )}
                      />
                    </TableCell>
                    {tieneLotes && (
                      <TableCell>
                        <Controller
                          name={`lineas.${index}.numeroLote`}
                          control={control}
                          render={({ field }) => (
                            <TextField
                              {...field}
                              size="small"
                              fullWidth
                              placeholder={detalle.manejaLotes ? 'Requerido' : '—'}
                              disabled={!detalle.manejaLotes}
                            />
                          )}
                        />
                      </TableCell>
                    )}
                    {tieneLotes && (
                      <TableCell>
                        <Controller
                          name={`lineas.${index}.fechaVencimiento`}
                          control={control}
                          render={({ field }) => (
                            <TextField
                              {...field}
                              type="date"
                              size="small"
                              fullWidth
                              disabled={!detalle.manejaLotes}
                              InputLabelProps={{ shrink: true }}
                              inputProps={{ min: today }}
                            />
                          )}
                        />
                      </TableCell>
                    )}
                    <TableCell>
                      <Controller
                        name={`lineas.${index}.observaciones`}
                        control={control}
                        render={({ field }) => (
                          <TextField {...field} size="small" fullWidth placeholder="Opcional" />
                        )}
                      />
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>

        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1, mb: 2 }}>
          <Alert severity="success">
            <strong>Resumen:</strong> Se recibirán <strong>{totalUnidades}</strong> unidades en total
          </Alert>
          <Alert severity="info" variant="outlined">
            <strong>Integración ERP Sinco:</strong> Al confirmar esta recepción, el sistema encolará automáticamente el respectivo comprobante y actualizará el estado en la tabla de órdenes de compra al procesarlo.
          </Alert>
        </Box>

        <Box sx={{ display: 'flex', gap: 1, justifyContent: 'flex-end' }}>
          <Button onClick={onCancel} disabled={mutation.isPending}>
            Cancelar
          </Button>
          <Button
            type="submit"
            variant="contained"
            color="primary"
            startIcon={<LocalShippingIcon />}
            disabled={mutation.isPending}
          >
            {mutation.isPending ? 'Recibiendo...' : 'Recibir Mercancía'}
          </Button>
        </Box>
      </form>
    </Paper>
  );
}

// ── Componente principal ─────────────────────────────────────────────────────
export function OrdenCompraDetalleView({ ordenId, onBack }: Props) {
  const [accion, setAccion] = useState<Accion>(null);

  const { data: orden, isLoading } = useQuery({
    queryKey: ['compra', ordenId],
    queryFn: () => comprasApi.getById(ordenId),
  });

  const handleAccionDone = () => {
    setAccion(null);
    // La query se invalida dentro de cada AccionXxx → se re-carga automáticamente
    // Si la acción cierra el flujo (rechazar/cancelar/recibir completa), volvemos atrás
    onBack();
  };

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: 300 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (!orden) {
    return (
      <Box sx={{ p: 4 }}>
        <Alert severity="error">No se pudo cargar la orden de compra.</Alert>
        <Button startIcon={<ArrowBackIcon />} onClick={onBack} sx={{ mt: 2 }}>
          Volver
        </Button>
      </Box>
    );
  }

  const estadoMeta = ESTADO_META[orden.estado] ?? { color: 'default' as const, label: orden.estado };
  const esTerminal = ESTADOS_TERMINALES.includes(orden.estado);
  const mostrarBarra = !esTerminal && accion === null;

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'grey.50' }}>
      {/* Hero */}
      <Box
        sx={{
          background: `linear-gradient(135deg, ${HERO_COLOR} 0%, #0d47a1 50%, #01579b 100%)`,
          px: { xs: 3, md: 4 },
          py: { xs: 1.5, md: 2 },
          mb: 3,
          position: 'relative',
          overflow: 'hidden',
          '&::before': {
            content: '""', position: 'absolute', top: -60, right: -60,
            width: 200, height: 200, borderRadius: '50%', background: 'rgba(255,255,255,0.05)',
          },
          '&::after': {
            content: '""', position: 'absolute', bottom: -40, right: 80,
            width: 120, height: 120, borderRadius: '50%', background: 'rgba(255,255,255,0.05)',
          },
        }}
      >
        <Box
          sx={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            position: 'relative',
            zIndex: 1,
            flexWrap: 'wrap',
            gap: 1,
          }}
        >
          {/* Izquierda: back + número + chip */}
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
            <IconButton
              aria-label="regresar"
              onClick={onBack}
              sx={{
                color: '#fff',
                bgcolor: 'rgba(255,255,255,0.12)',
                '&:hover': { bgcolor: 'rgba(255,255,255,0.22)' },
              }}
            >
              <ArrowBackIcon />
            </IconButton>
            <Typography
              variant="h5"
              fontWeight={700}
              sx={{ color: '#fff', fontFamily: 'monospace', letterSpacing: '0.05em' }}
            >
              {orden.numeroOrden}
            </Typography>
            <Chip
              label={estadoMeta.label}
              color={estadoMeta.color}
              size="small"
              sx={{ fontWeight: 700, bgcolor: 'rgba(255,255,255,0.18)', color: '#fff', border: '1px solid rgba(255,255,255,0.3)' }}
            />
          </Box>

          {/* Derecha: proveedor + fecha */}
          <Box sx={{ textAlign: { xs: 'left', md: 'right' } }}>
            <Typography variant="body1" fontWeight={600} sx={{ color: '#fff' }}>
              {orden.nombreProveedor}
            </Typography>
            <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.75)' }}>
              {new Date(orden.fechaOrden).toLocaleDateString('es-CO')}
            </Typography>
          </Box>
        </Box>
      </Box>

      {/* Body */}
      <Box sx={{ px: { xs: 2, md: 4 }, pb: 4 }}>
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: { xs: '1fr', md: '280px 1fr' },
            gap: 3,
            alignItems: 'start',
          }}
        >
          {/* Panel izquierdo — Información */}
          <Paper variant="outlined" sx={{ p: 2.5, borderRadius: 2 }}>
            <Typography
              variant="caption"
              fontWeight={700}
              color="text.secondary"
              sx={{ letterSpacing: '0.08em', textTransform: 'uppercase', display: 'block', mb: 2 }}
            >
              Información
            </Typography>

            <InfoRow label="Número" value={<span style={{ fontFamily: 'monospace' }}>{orden.numeroOrden}</span>} />
            <InfoRow label="Sucursal" value={orden.nombreSucursal} />
            <InfoRow label="Proveedor" value={orden.nombreProveedor} />
            <InfoRow label="Fecha de Orden" value={formatFecha(orden.fechaOrden)} />
            <InfoRow
              label="Entrega Esperada"
              value={orden.fechaEntregaEsperada ? formatFecha(orden.fechaEntregaEsperada) : '—'}
            />
            <InfoRow
              label="Forma de Pago"
              value={orden.formaPago === 'Credito' ? `Crédito (${orden.diasPlazo} días)` : 'Contado'}
            />

            {orden.aprobadoPor && (
              <>
                <InfoRow label="Aprobado por" value={orden.aprobadoPor} />
                {orden.fechaAprobacion && (
                  <InfoRow label="Fecha aprobación" value={formatFecha(orden.fechaAprobacion)} />
                )}
              </>
            )}

            {orden.recibidoPor && (
              <>
                <InfoRow label="Recibido por" value={orden.recibidoPor} />
                {orden.fechaRecepcion && (
                  <InfoRow label="Fecha recepción" value={formatFecha(orden.fechaRecepcion)} />
                )}
              </>
            )}

            {orden.observaciones && (
              <>
                <Divider sx={{ my: 1.5 }} />
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
                  Observaciones
                </Typography>
                <Typography variant="body2" sx={{ color: 'text.secondary', fontStyle: 'italic' }}>
                  {orden.observaciones}
                </Typography>
              </>
            )}

            {/* ERP status — solo en RecibidaParcial o RecibidaCompleta */}
            {(orden.estado === 'RecibidaParcial' || orden.estado === 'RecibidaCompleta') && (
              <>
                <Divider sx={{ my: 1.5 }} />
                <Typography
                  variant="caption"
                  fontWeight={700}
                  color="text.secondary"
                  sx={{ display: 'block', mb: 1, textTransform: 'uppercase', letterSpacing: '0.06em' }}
                >
                  ERP Sinco
                </Typography>
                {orden.sincronizadoErp ? (
                  <Chip
                    label={`Sincronizado • Ref: ${orden.erpReferencia ?? '—'}`}
                    color="success"
                    size="small"
                    variant="outlined"
                    sx={{ fontWeight: 600 }}
                  />
                ) : orden.errorSincronizacion ? (
                  <Chip
                    label="Error ERP"
                    color="error"
                    size="small"
                    variant="outlined"
                    sx={{ fontWeight: 600 }}
                  />
                ) : (
                  <Chip
                    label="Pendiente ERP"
                    color="default"
                    size="small"
                    variant="outlined"
                    sx={{ fontWeight: 600 }}
                  />
                )}
                {orden.errorSincronizacion && (
                  <Typography variant="caption" color="error" sx={{ display: 'block', mt: 0.5 }}>
                    {orden.errorSincronizacion}
                  </Typography>
                )}
              </>
            )}

            {orden.motivoRechazo && (
              <>
                <Divider sx={{ my: 1.5 }} />
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
                  Motivo Rechazo / Cancelación
                </Typography>
                <Typography variant="body2" color="error.main" fontWeight={500}>
                  {orden.motivoRechazo}
                </Typography>
              </>
            )}
          </Paper>

          {/* Panel derecho — Productos */}
          <Paper variant="outlined" sx={{ borderRadius: 2, overflow: 'hidden' }}>
            {/* Encabezado tabla */}
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

            {/* Footer totales */}
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
        </Box>

        {/* Barra de acciones */}
        {mostrarBarra && (
          <Paper
            variant="outlined"
            sx={{
              mt: 3,
              p: 2,
              borderRadius: 2,
              borderColor: alpha(HERO_COLOR, 0.3),
              display: 'flex',
              alignItems: 'center',
              gap: 1.5,
              flexWrap: 'wrap',
            }}
          >
            <Typography variant="body2" color="text.secondary" fontWeight={500} sx={{ mr: 'auto' }}>
              Acciones disponibles
            </Typography>

            {orden.estado === 'Pendiente' && (
              <>
                <Button variant="outlined" color="error" size="small" onClick={() => setAccion('rechazar')}>
                  Rechazar
                </Button>
                <Button variant="outlined" color="warning" size="small" onClick={() => setAccion('cancelar')}>
                  Cancelar
                </Button>
                <Button variant="contained" color="success" size="small" onClick={() => setAccion('aprobar')}>
                  Aprobar
                </Button>
              </>
            )}

            {orden.estado === 'Aprobada' && (
              <>
                <Button variant="outlined" color="warning" size="small" onClick={() => setAccion('cancelar')}>
                  Cancelar
                </Button>
                <Button
                  variant="contained"
                  color="primary"
                  size="small"
                  startIcon={<LocalShippingIcon />}
                  onClick={() => setAccion('recibir')}
                >
                  Recibir Mercancía
                </Button>
              </>
            )}

            {orden.estado === 'RecibidaParcial' && (
              <Button
                variant="contained"
                color="primary"
                size="small"
                startIcon={<LocalShippingIcon />}
                onClick={() => setAccion('recibir')}
              >
                Recibir Mercancía
              </Button>
            )}
          </Paper>
        )}

        {/* Paneles de acción inline */}
        {accion !== null && (
          <Box sx={{ mt: 3 }}>
            {accion === 'aprobar' && (
              <AccionAprobar
                orden={orden}
                onCancel={() => setAccion(null)}
                onDone={handleAccionDone}
              />
            )}
            {accion === 'rechazar' && (
              <AccionRechazar
                orden={orden}
                onCancel={() => setAccion(null)}
                onDone={handleAccionDone}
              />
            )}
            {accion === 'cancelar' && (
              <AccionCancelar
                orden={orden}
                onCancel={() => setAccion(null)}
                onDone={handleAccionDone}
              />
            )}
            {accion === 'recibir' && (
              <AccionRecibir
                orden={orden}
                onCancel={() => setAccion(null)}
                onDone={handleAccionDone}
              />
            )}
          </Box>
        )}
      </Box>
    </Box>
  );
}
