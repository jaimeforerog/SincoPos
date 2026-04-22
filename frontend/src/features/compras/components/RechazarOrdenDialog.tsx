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
import type { OrdenCompraDTO, RechazarOrdenCompraDTO, PaginatedResult , ApiError} from '@/types/api';

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
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: ['compras'] });
      const snapshots = queryClient.getQueriesData<PaginatedResult<OrdenCompraDTO>>({ queryKey: ['compras'] });
      queryClient.setQueriesData<PaginatedResult<OrdenCompraDTO>>(
        { queryKey: ['compras'] },
        (old) => old ? { ...old, items: old.items.map(o => o.id === orden.id ? { ...o, estado: 'Rechazada' } : o) } : old,
      );
      return { snapshots };
    },
    onError: (error: ApiError, _vars, ctx) => {
      ctx?.snapshots.forEach(([key, val]) => queryClient.setQueryData(key, val));
      enqueueSnackbar(error.message || 'Error al rechazar la orden', { variant: 'error' });
    },
    onSuccess: () => {
      enqueueSnackbar('Orden rechazada', { variant: 'success' });
      reset();
      onSuccess();
      onClose();
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: ['compras'] });
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
