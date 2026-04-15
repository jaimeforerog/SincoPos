import { useEffect } from 'react';
import { useForm, Controller, useFieldArray } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
  Box,
  Button,
  Paper,
  Typography,
  Chip,
  Alert,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
} from '@mui/material';
import LocalShippingIcon from '@mui/icons-material/LocalShipping';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useSnackbar } from 'notistack';
import { comprasApi } from '@/api/compras';
import type { OrdenCompraDTO } from '@/types/api';
import { localDateStr } from '@/utils/dates';

// ── Schemas ──────────────────────────────────────────────────────────────────

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

// ── Componente ────────────────────────────────────────────────────────────────

interface Props {
  orden: OrdenCompraDTO;
  onCancel: () => void;
  onDone: () => void;
}

export function AccionRecibir({ orden, onCancel, onDone }: Props) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();
  const today = localDateStr();
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
          fechaVencimientoDefault = localDateStr(fecha);
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
