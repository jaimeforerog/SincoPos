import { useEffect } from 'react';
import { useForm, Controller, useFieldArray, type FieldError } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  Alert,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Typography,
  Box,
  Chip,
} from '@mui/material';
import { useSnackbar } from 'notistack';
import { comprasApi } from '@/api/compras';
import type { OrdenCompraDTO, RecibirOrdenCompraDTO } from '@/types/api';

type LineaRecepcionError = { cantidadRecibida?: FieldError };

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

interface RecibirOrdenDialogProps {
  open: boolean;
  orden: OrdenCompraDTO;
  onClose: () => void;
  onSuccess: () => void;
}

export function RecibirOrdenDialog({
  open,
  orden,
  onClose,
  onSuccess,
}: RecibirOrdenDialogProps) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();

  const {
    control,
    handleSubmit,
    reset,
    watch,
    formState: { errors },
  } = useForm<RecibirFormData>({
    resolver: zodResolver(recibirSchema),
    defaultValues: { fechaRecepcion: new Date().toISOString().split('T')[0], lineas: [] },
  });

  const { fields } = useFieldArray({ control, name: 'lineas' });

  // Default: fecha de entrega esperada si está en el pasado/hoy, si no hoy
  const today = new Date().toISOString().split('T')[0];
  const defaultFechaRecepcion = (() => {
    if (orden.fechaEntregaEsperada) {
      const fe = orden.fechaEntregaEsperada.split('T')[0];
      return fe <= today ? fe : today;
    }
    return today;
  })();

  useEffect(() => {
    if (open) {
      reset({
        fechaRecepcion: defaultFechaRecepcion,
        lineas: orden.detalles.map((d) => {
          // Pre-calcular fecha de vencimiento si el producto tiene diasVidaUtil configurado
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
    }
  }, [open, orden, reset]);

  const lineas = watch('lineas');

  const mutation = useMutation({
    mutationFn: (data: RecibirOrdenCompraDTO) => comprasApi.recibir(orden.id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['compras'] });
      enqueueSnackbar('Mercancía recibida exitosamente', { variant: 'success' });
      onSuccess();
      onClose();
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
        mensaje = msg || 'Error interno del servidor. Contacta al administrador.';
      } else if (statusCode === 0) {
        mensaje = msg || 'No se pudo conectar con el servidor.';
      } else if (msg) {
        mensaje = msg;
      }

      enqueueSnackbar(mensaje, { variant: 'error' });
    },
  });

  const onSubmit = (data: RecibirFormData) => {
    const lineasRecibidas = data.lineas
      .filter((l) => l.cantidadRecibida > 0)
      .map((l) => ({
        productoId: l.productoId,
        cantidadRecibida: l.cantidadRecibida,
        observaciones: l.observaciones || undefined,
        numeroLote: l.numeroLote || undefined,
        fechaVencimiento: l.fechaVencimiento || undefined,
      }));

    if (lineasRecibidas.length === 0) {
      enqueueSnackbar('Debe recibir al menos un producto', { variant: 'warning' });
      return;
    }

    mutation.mutate({ lineas: lineasRecibidas, fechaRecepcion: data.fechaRecepcion });
  };

  const handleClose = () => { reset({ fechaRecepcion: today, lineas: [] }); onClose(); };

  const tieneLotes = orden.detalles.some((d) => d.manejaLotes);

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="lg" fullWidth>
      <DialogTitle>Recibir Mercancía — {orden.numeroOrden}</DialogTitle>

      <form onSubmit={handleSubmit(onSubmit)}>
        <DialogContent>
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

          <TableContainer component={Paper} variant="outlined">
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell width="25%">Producto</TableCell>
                  <TableCell align="center">Solicitada</TableCell>
                  <TableCell align="center">Recibida</TableCell>
                  <TableCell align="center">Pendiente</TableCell>
                  <TableCell align="center" width="110px">Recibir Ahora</TableCell>
                  {tieneLotes && <TableCell width="130px">Nº Lote</TableCell>}
                  {tieneLotes && <TableCell width="140px">Vencimiento</TableCell>}
                  <TableCell>Observaciones</TableCell>
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
                        <Typography variant="body2" color={detalle.cantidadRecibida > 0 ? 'success.main' : 'text.secondary'}>
                          {detalle.cantidadRecibida}
                        </Typography>
                      </TableCell>
                      <TableCell align="center">
                        <Typography variant="body2" fontWeight="medium" color={pendiente > 0 ? 'warning.main' : 'success.main'}>
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
                              error={!!(errors.lineas?.[index] as LineaRecepcionError | undefined)?.cantidadRecibida}
                              helperText={(errors.lineas?.[index] as LineaRecepcionError | undefined)?.cantidadRecibida?.message}
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
                                inputProps={{ min: new Date().toISOString().split('T')[0] }}
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

          <Box sx={{ mt: 2, display: 'flex', flexDirection: 'column', gap: 1 }}>
            <Alert severity="success">
              <strong>Resumen:</strong> Se recibirán{' '}
              {lineas.reduce((sum, l) => sum + (l.cantidadRecibida || 0), 0)} unidades en total
            </Alert>
            <Alert severity="info" variant="outlined">
              <strong>Integración ERP Sinco:</strong> Al confirmar esta recepción, el sistema encolará automáticamente el respectivo comprobante y actualizará el estado en la tabla de órdenes de compra al procesarlo.
            </Alert>
          </Box>
        </DialogContent>

        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={handleClose} disabled={mutation.isPending}>Cancelar</Button>
          <Button type="submit" variant="contained" color="primary" disabled={mutation.isPending}>
            {mutation.isPending ? 'Recibiendo...' : 'Recibir Mercancía'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
