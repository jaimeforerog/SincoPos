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
import type { OrdenCompraDTO, CancelarOrdenCompraDTO } from '@/types/api';

const cancelarSchema = z.object({
  motivo: z.string().min(1, 'El motivo es requerido').min(5, 'Mínimo 5 caracteres'),
});

interface CancelarOrdenDialogProps {
  open: boolean;
  orden: OrdenCompraDTO;
  onClose: () => void;
  onSuccess: () => void;
}

export function CancelarOrdenDialog({
  open,
  orden,
  onClose,
  onSuccess,
}: CancelarOrdenDialogProps) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();

  const {
    control,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<CancelarOrdenCompraDTO>({
    resolver: zodResolver(cancelarSchema),
    defaultValues: {
      motivo: '',
    },
  });

  const mutation = useMutation({
    mutationFn: (data: CancelarOrdenCompraDTO) => comprasApi.cancelar(orden.id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['compras'] });
      enqueueSnackbar('Orden cancelada', { variant: 'success' });
      reset();
      onSuccess();
      onClose();
    },
    onError: (error: any) => {
      let mensaje = 'Error al cancelar la orden';

      if (error.response) {
        const { status, data } = error.response;
        if (status === 400) {
          if (data.errors?.Motivo) {
            mensaje = `Motivo de cancelación inválido: ${data.errors.Motivo.join(', ')}`;
          } else {
            mensaje = data.error || 'No se puede cancelar esta orden. Verifica su estado o si tiene recepciones.';
          }
        } else if (status === 403) {
          mensaje = 'No tienes permisos para cancelar órdenes. Se requiere rol Supervisor.';
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

  const onSubmit = (data: CancelarOrdenCompraDTO) => {
    mutation.mutate(data);
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Cancelar Orden de Compra</DialogTitle>

      <form onSubmit={handleSubmit(onSubmit)}>
        <DialogContent>
          <Alert severity="warning" sx={{ mb: 2 }}>
            ¿Está seguro que desea cancelar la orden {orden.numeroOrden}?
          </Alert>

          <Controller
            name="motivo"
            control={control}
            render={({ field }) => (
              <TextField
                {...field}
                label="Motivo de Cancelación *"
                multiline
                rows={4}
                fullWidth
                error={!!errors.motivo}
                helperText={errors.motivo?.message}
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
            color="warning"
            disabled={mutation.isPending}
          >
            {mutation.isPending ? 'Cancelando...' : 'Cancelar Orden'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
