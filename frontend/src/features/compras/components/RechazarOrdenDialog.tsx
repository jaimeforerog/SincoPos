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
} from '@mui/material';
import { useSnackbar } from 'notistack';
import { comprasApi } from '@/api/compras';
import type { OrdenCompraDTO, RechazarOrdenCompraDTO } from '@/types/api';

const rechazarSchema = z.object({
  motivoRechazo: z.string().min(1, 'El motivo es requerido').min(5, 'Mínimo 5 caracteres'),
});

interface RechazarOrdenDialogProps {
  open: boolean;
  orden: OrdenCompraDTO;
  onClose: () => void;
  onSuccess: () => void;
}

export function RechazarOrdenDialog({
  open,
  orden,
  onClose,
  onSuccess,
}: RechazarOrdenDialogProps) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();

  const {
    control,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<RechazarOrdenCompraDTO>({
    resolver: zodResolver(rechazarSchema),
    defaultValues: {
      motivoRechazo: '',
    },
  });

  const mutation = useMutation({
    mutationFn: (data: RechazarOrdenCompraDTO) => comprasApi.rechazar(orden.id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['compras'] });
      enqueueSnackbar('Orden rechazada', { variant: 'success' });
      reset();
      onSuccess();
      onClose();
    },
    onError: (error: any) => {
      let mensaje = 'Error al rechazar la orden';

      if (error.response) {
        const { status, data } = error.response;
        if (status === 400) {
          if (data.errors?.MotivoRechazo) {
            mensaje = `Motivo de rechazo inválido: ${data.errors.MotivoRechazo.join(', ')}`;
          } else {
            mensaje = data.error || 'No se puede rechazar esta orden. Verifica su estado actual.';
          }
        } else if (status === 403) {
          mensaje = 'No tienes permisos para rechazar órdenes. Se requiere rol Supervisor.';
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

  const onSubmit = (data: RechazarOrdenCompraDTO) => {
    mutation.mutate(data);
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Rechazar Orden de Compra</DialogTitle>

      <form onSubmit={handleSubmit(onSubmit)}>
        <DialogContent>
          <Alert severity="warning" sx={{ mb: 2 }}>
            ¿Está seguro que desea rechazar la orden {orden.numeroOrden}?
          </Alert>

          <Controller
            name="motivoRechazo"
            control={control}
            render={({ field }) => (
              <TextField
                {...field}
                label="Motivo de Rechazo *"
                multiline
                rows={4}
                fullWidth
                error={!!errors.motivoRechazo}
                helperText={errors.motivoRechazo?.message}
              />
            )}
          />
        </DialogContent>

        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={onClose} disabled={mutation.isPending}>
            Cancelar
          </Button>
          <Button
            type="submit"
            variant="contained"
            color="error"
            disabled={mutation.isPending}
          >
            {mutation.isPending ? 'Rechazando...' : 'Rechazar'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
