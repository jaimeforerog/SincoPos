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
import type { OrdenCompraDTO, AprobarOrdenCompraDTO, PaginatedResult , ApiError} from '@/types/api';

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
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: ['compras'] });
      const snapshots = queryClient.getQueriesData<PaginatedResult<OrdenCompraDTO>>({ queryKey: ['compras'] });
      queryClient.setQueriesData<PaginatedResult<OrdenCompraDTO>>(
        { queryKey: ['compras'] },
        (old) => old ? { ...old, items: old.items.map(o => o.id === orden.id ? { ...o, estado: 'Aprobada' } : o) } : old,
      );
      return { snapshots };
    },
    onError: (error: ApiError, _vars, ctx) => {
      ctx?.snapshots.forEach(([key, val]) => queryClient.setQueryData(key, val));
      enqueueSnackbar(error.message || 'Error al aprobar la orden', { variant: 'error' });
    },
    onSuccess: () => {
      enqueueSnackbar('Orden aprobada exitosamente', { variant: 'success' });
      reset();
      onSuccess();
      onClose();
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: ['compras'] });
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
