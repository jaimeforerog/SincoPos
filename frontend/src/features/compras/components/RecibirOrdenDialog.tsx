import { useEffect } from 'react';
import { useForm, Controller, useFieldArray } from 'react-hook-form';
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
} from '@mui/material';
import { useSnackbar } from 'notistack';
import { comprasApi } from '@/api/compras';
import type { OrdenCompraDTO, RecibirOrdenCompraDTO } from '@/types/api';

const lineaRecepcionSchema = z.object({
  productoId: z.string(),
  cantidadRecibida: z.number().min(0, 'Cantidad debe ser mayor o igual a 0'),
  observaciones: z.string().optional(),
});

const recibirSchema = z.object({
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
    defaultValues: {
      lineas: [],
    },
  });

  const { fields } = useFieldArray({
    control,
    name: 'lineas',
  });

  // Inicializar líneas con las cantidades pendientes
  useEffect(() => {
    if (open) {
      const lineasIniciales = orden.detalles.map((detalle) => {
        const cantidadPendiente = detalle.cantidadSolicitada - detalle.cantidadRecibida;
        return {
          productoId: detalle.productoId,
          cantidadRecibida: cantidadPendiente, // Pre-rellenar con cantidad pendiente
          observaciones: '',
        };
      });
      reset({ lineas: lineasIniciales });
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
      let mensaje = 'Error al recibir la mercancía';

      if (error.response) {
        const { status, data } = error.response;
        if (status === 400) {
          if (data.errors) {
            // Errores de validación campo por campo
            const errores = Object.entries(data.errors)
              .map(([campo, mensajes]: [string, any]) => `${campo}: ${Array.isArray(mensajes) ? mensajes.join(', ') : mensajes}`)
              .join('; ');
            mensaje = `Errores de validación: ${errores}`;
          } else if (data.error) {
            // Mensaje específico del backend (ej: cantidad excede pendiente)
            mensaje = data.error;
          } else {
            mensaje = 'Datos de recepción inválidos. Verifica las cantidades.';
          }
        } else if (status === 403) {
          mensaje = 'No tienes permisos para recibir mercancía. Se requiere rol Supervisor.';
        } else if (status === 404) {
          mensaje = 'Orden de compra no encontrada.';
        } else {
          mensaje = data.error || data.message || mensaje;
        }
      } else if (error.request) {
        mensaje = 'No se pudo conectar con el servidor.';
      }

      enqueueSnackbar(mensaje, { variant: 'error' });
    },
  });

  const onSubmit = (data: RecibirFormData) => {
    // Filtrar solo las líneas con cantidad > 0
    const lineasRecibidas = data.lineas.filter((l) => l.cantidadRecibida > 0);

    if (lineasRecibidas.length === 0) {
      enqueueSnackbar('Debe recibir al menos un producto', { variant: 'warning' });
      return;
    }

    mutation.mutate({ lineas: lineasRecibidas });
  };

  const handleClose = () => {
    reset();
    onClose();
  };

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="md" fullWidth>
      <DialogTitle>Recibir Mercancía - {orden.numeroOrden}</DialogTitle>

      <form onSubmit={handleSubmit(onSubmit)}>
        <DialogContent>
          <Alert severity="info" sx={{ mb: 2 }}>
            Ingrese las cantidades recibidas para cada producto. Puede ser una recepción parcial.
          </Alert>

          <TableContainer component={Paper} variant="outlined">
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell width="35%">Producto</TableCell>
                  <TableCell align="center">Solicitada</TableCell>
                  <TableCell align="center">Ya Recibida</TableCell>
                  <TableCell align="center">Pendiente</TableCell>
                  <TableCell align="center">Recibir Ahora</TableCell>
                  <TableCell width="25%">Observaciones</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {fields.map((field, index) => {
                  const detalle = orden.detalles[index];
                  const cantidadPendiente = detalle.cantidadSolicitada - detalle.cantidadRecibida;

                  return (
                    <TableRow key={field.id}>
                      <TableCell>
                        <Typography variant="body2">{detalle.nombreProducto}</Typography>
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
                          color={cantidadPendiente > 0 ? 'warning.main' : 'success.main'}
                        >
                          {cantidadPendiente}
                        </Typography>
                      </TableCell>
                      <TableCell align="center">
                        <Controller
                          name={`lineas.${index}.cantidadRecibida`}
                          control={control}
                          render={({ field: { value, onChange, ...field } }) => (
                            <TextField
                              {...field}
                              type="number"
                              value={value}
                              onChange={(e) => onChange(parseFloat(e.target.value) || 0)}
                              error={!!(errors.lineas?.[index] as any)?.cantidadRecibida}
                              helperText={(errors.lineas?.[index] as any)?.cantidadRecibida?.message}
                              size="small"
                              sx={{ width: 100 }}
                              inputProps={{
                                min: 0,
                                max: cantidadPendiente,
                                step: 1,
                              }}
                            />
                          )}
                        />
                      </TableCell>
                      <TableCell>
                        <Controller
                          name={`lineas.${index}.observaciones`}
                          control={control}
                          render={({ field }) => (
                            <TextField
                              {...field}
                              size="small"
                              fullWidth
                              placeholder="Opcional"
                            />
                          )}
                        />
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>

          <Box sx={{ mt: 2 }}>
            <Alert severity="success">
              <Typography variant="body2">
                <strong>Resumen:</strong> Se recibirán{' '}
                {lineas.reduce((sum, l) => sum + (l.cantidadRecibida || 0), 0)} unidades en total
              </Typography>
            </Alert>
          </Box>
        </DialogContent>

        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={handleClose} disabled={mutation.isPending}>
            Cancelar
          </Button>
          <Button
            type="submit"
            variant="contained"
            color="primary"
            disabled={mutation.isPending}
          >
            {mutation.isPending ? 'Recibiendo...' : 'Recibir Mercancía'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
