import { useForm, Controller } from 'react-hook-form';
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
import type { OrdenCompraDTO, AprobarOrdenCompraDTO } from '@/types/api';

interface AprobarOrdenDialogProps {
  open: boolean;
  orden: OrdenCompraDTO;
  onClose: () => void;
  onSuccess: () => void;
}

export function AprobarOrdenDialog({
  open,
  orden,
  onClose,
  onSuccess,
}: AprobarOrdenDialogProps) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();

  const { control, handleSubmit, reset } = useForm<AprobarOrdenCompraDTO>({
    defaultValues: {
      observaciones: '',
    },
  });

  const mutation = useMutation({
    mutationFn: (data: AprobarOrdenCompraDTO) => comprasApi.aprobar(orden.id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['compras'] });
      enqueueSnackbar('Orden aprobada exitosamente', { variant: 'success' });
      reset();
      onSuccess();
      onClose();
    },
    onError: (error: any) => {
      let mensaje = 'Error al aprobar la orden';

      if (error.response) {
        const { status, data } = error.response;
        if (status === 400) {
          mensaje = data.error || 'No se puede aprobar esta orden. Verifica su estado actual.';
        } else if (status === 403) {
          mensaje = 'No tienes permisos para aprobar órdenes. Se requiere rol Supervisor.';
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

  const onSubmit = (data: AprobarOrdenCompraDTO) => {
    mutation.mutate(data);
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Aprobar Orden de Compra</DialogTitle>

      <form onSubmit={handleSubmit(onSubmit)}>
        <DialogContent>
          <Alert severity="info" sx={{ mb: 2 }}>
            ¿Está seguro que desea aprobar la orden {orden.numeroOrden}?
          </Alert>

          <Controller
            name="observaciones"
            control={control}
            render={({ field }) => (
              <TextField
                {...field}
                label="Observaciones (opcional)"
                multiline
                rows={3}
                fullWidth
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
            color="success"
            disabled={mutation.isPending}
          >
            {mutation.isPending ? 'Aprobando...' : 'Aprobar'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
