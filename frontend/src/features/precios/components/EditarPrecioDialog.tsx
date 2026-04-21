import { useEffect } from 'react';
import { useForm, Controller } from 'react-hook-form';
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
  InputAdornment,
  Box,
  Typography,
  Chip,
} from '@mui/material';
import { useSnackbar } from 'notistack';
import { preciosApi } from '@/api/precios';
import type { ProductoDTO, PrecioResueltoDTO , ApiError} from '@/types/api';

const precioSchema = z.object({
  precioVenta: z.number({ invalid_type_error: 'Ingrese un precio válido' }).min(0, 'Debe ser mayor o igual a 0'),
  precioMinimo: z.number().min(0, 'Debe ser mayor o igual a 0').optional(),
});

type PrecioFormData = z.infer<typeof precioSchema>;

interface EditarPrecioDialogProps {
  open: boolean;
  producto: ProductoDTO | null;
  sucursalId: number;
  precioActual?: PrecioResueltoDTO;
  onClose: () => void;
  onSuccess: () => void;
}

export function EditarPrecioDialog({
  open,
  producto,
  sucursalId,
  precioActual,
  onClose,
  onSuccess,
}: EditarPrecioDialogProps) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();

  const {
    control,
    handleSubmit,
    reset,
    formState: { errors },
    watch,
  } = useForm<PrecioFormData>({
    resolver: zodResolver(precioSchema),
    defaultValues: {
      precioVenta: 0,
      precioMinimo: undefined,
    },
  });

  useEffect(() => {
    if (open && producto) {
      reset({
        precioVenta: precioActual?.precioVenta || 0,
        precioMinimo: precioActual?.precioMinimo || undefined,
      });
    }
  }, [open, producto, precioActual, reset]);

  const mutation = useMutation({
    mutationFn: async (data: PrecioFormData) => {
      await preciosApi.createOrUpdate({
        productoId: producto!.id,
        sucursalId,
        precioVenta: data.precioVenta,
        precioMinimo: data.precioMinimo,
        origenDato: 'Manual',  // Marcar como configurado manualmente
      });
    },
    onSuccess: () => {
      enqueueSnackbar('Precio actualizado exitosamente', { variant: 'success' });
      queryClient.invalidateQueries({ queryKey: ['precios'] });
      onSuccess();
      onClose();
    },
    onError: (error: ApiError) => {
      const mensaje =
        error.message || 'Error al actualizar el precio';
      enqueueSnackbar(mensaje, { variant: 'error' });
    },
  });

  const onSubmit = (data: PrecioFormData) => {
    mutation.mutate(data);
  };

  const precioVenta = watch('precioVenta');
  const precioCosto = producto?.precioCosto || 0;
  const margen = precioCosto > 0 ? ((precioVenta - precioCosto) / precioCosto) * 100 : 0;

  const formatCurrency = (value: number) => {
    return new Intl.NumberFormat('es-CO', {
      style: 'currency',
      currency: 'COP',
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(value);
  };

  if (!producto) return null;

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>
        <Typography variant="h6" sx={{ fontWeight: 700 }}>
          Configurar Precio
        </Typography>
        <Typography variant="body2" color="text.secondary">
          {producto.nombre}
        </Typography>
      </DialogTitle>

      <form onSubmit={handleSubmit(onSubmit)}>
        <DialogContent>
          <Box sx={{ mb: 3 }}>
            <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 2, mb: 2 }}>
              <Box>
                <Typography variant="caption" color="text.secondary">
                  Código de Barras
                </Typography>
                <Typography variant="body2" sx={{ fontFamily: 'monospace', fontWeight: 600 }}>
                  {producto.codigoBarras}
                </Typography>
              </Box>
              <Box>
                <Typography variant="caption" color="text.secondary">
                  Precio Costo
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {formatCurrency(producto.precioCosto)}
                </Typography>
              </Box>
            </Box>

            {precioActual && (
              <Alert severity="info" sx={{ mb: 2 }}>
                Precio actual: <strong>{formatCurrency(precioActual.precioVenta)}</strong>
                <br />
                Origen: <Chip label={precioActual.origen} size="small" sx={{ ml: 1 }} />
              </Alert>
            )}
          </Box>

          <Box sx={{ display: 'grid', gridTemplateColumns: '1fr', gap: 2 }}>
            {/* Precio Venta */}
            <Controller
              name="precioVenta"
              control={control}
              render={({ field: { value, onChange, ...field } }) => (
                <TextField
                  {...field}
                  type="number"
                  label="Precio Venta *"
                  value={Number.isNaN(value) ? '' : value}
                  onChange={(e) => onChange(e.target.value === '' ? NaN : parseFloat(e.target.value))}
                  error={!!errors.precioVenta}
                  helperText={
                    errors.precioVenta?.message ||
                    (margen !== 0 ? `Margen: ${margen.toFixed(1)}%` : undefined)
                  }
                  fullWidth
                  autoFocus
                  InputProps={{
                    startAdornment: (
                      <InputAdornment position="start">$</InputAdornment>
                    ),
                  }}
                />
              )}
            />

            {/* Precio Mínimo */}
            <Controller
              name="precioMinimo"
              control={control}
              render={({ field: { value, onChange, ...field } }) => (
                <TextField
                  {...field}
                  type="number"
                  label="Precio Mínimo (Opcional)"
                  value={value || ''}
                  onChange={(e) =>
                    onChange(e.target.value ? parseFloat(e.target.value) : undefined)
                  }
                  error={!!errors.precioMinimo}
                  helperText={
                    errors.precioMinimo?.message ||
                    'Piso para descuentos en punto de venta'
                  }
                  fullWidth
                  InputProps={{
                    startAdornment: (
                      <InputAdornment position="start">$</InputAdornment>
                    ),
                  }}
                />
              )}
            />
          </Box>

          {mutation.isError && (
            <Alert severity="error" sx={{ mt: 2 }}>
              {mutation.error instanceof Error
                ? mutation.error.message
                : 'Error al procesar la solicitud'}
            </Alert>
          )}
        </DialogContent>

        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={onClose} disabled={mutation.isPending}>
            Cancelar
          </Button>
          <Button
            type="submit"
            variant="contained"
            disabled={mutation.isPending}
          >
            {mutation.isPending ? 'Guardando...' : 'Guardar'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
