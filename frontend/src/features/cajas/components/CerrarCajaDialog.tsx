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
  Typography,
  Box,
  Divider,
  Alert,
} from '@mui/material';
import { useSnackbar } from 'notistack';
import { cajasApi } from '@/api/cajas';
import type { CajaDTO, CerrarCajaDTO } from '@/types/api';

const cerrarCajaSchema = z.object({
  montoCierre: z.number().min(0, 'El monto debe ser positivo'),
  observaciones: z.string().optional(),
});

type CerrarCajaFormData = z.infer<typeof cerrarCajaSchema>;

interface CerrarCajaDialogProps {
  open: boolean;
  onClose: () => void;
  caja: CajaDTO | null;
}

export function CerrarCajaDialog({ open, onClose, caja }: CerrarCajaDialogProps) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();

  const {
    control,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<CerrarCajaFormData>({
    resolver: zodResolver(cerrarCajaSchema),
    defaultValues: {
      montoCierre: 0,
      observaciones: '',
    },
  });

  const cerrarMutation = useMutation({
    mutationFn: (data: CerrarCajaDTO) => cajasApi.cerrar(caja!.id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['cajas'] });
      enqueueSnackbar('Caja cerrada correctamente', { variant: 'success' });
      reset();
      onClose();
    },
    onError: (error: any) => {
      enqueueSnackbar(
        error.response?.data?.message || 'Error al cerrar la caja',
        { variant: 'error' }
      );
    },
  });

  const onSubmit = (data: CerrarCajaFormData) => {
    cerrarMutation.mutate(data);
  };

  const handleClose = () => {
    reset();
    onClose();
  };

  const formatCurrency = (value: number) => {
    return new Intl.NumberFormat('es-CO', {
      style: 'currency',
      currency: 'COP',
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(value);
  };

  if (!caja) return null;

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Cerrar Caja: {caja.nombre}</DialogTitle>
      <form onSubmit={handleSubmit(onSubmit)}>
        <DialogContent>
          <Alert severity="info" sx={{ mb: 2 }}>
            Verifica el monto de cierre antes de confirmar. Esta acción no se puede deshacer.
          </Alert>

          <Box sx={{ mb: 2 }}>
            <Typography variant="body2" color="text.secondary">
              Sucursal: {caja.nombreSucursal}
            </Typography>
            <Divider sx={{ my: 1 }} />
            <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
              <Typography variant="body2">Monto de Apertura:</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {formatCurrency(caja.montoApertura)}
              </Typography>
            </Box>
            <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
              <Typography variant="body2">Monto Actual:</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {formatCurrency(caja.montoActual)}
              </Typography>
            </Box>
          </Box>

          <Controller
            name="montoCierre"
            control={control}
            render={({ field }) => (
              <TextField
                {...field}
                type="number"
                label="Monto de Cierre (Conteo Real)"
                fullWidth
                margin="normal"
                error={!!errors.montoCierre}
                helperText={
                  errors.montoCierre?.message ||
                  'Ingresa el monto real contado en la caja'
                }
                inputProps={{ min: 0, step: 1000 }}
              />
            )}
          />

          <Controller
            name="observaciones"
            control={control}
            render={({ field }) => (
              <TextField
                {...field}
                label="Observaciones (opcional)"
                fullWidth
                margin="normal"
                multiline
                rows={3}
                error={!!errors.observaciones}
                helperText={errors.observaciones?.message}
              />
            )}
          />
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={handleClose}>Cancelar</Button>
          <Button
            type="submit"
            variant="contained"
            color="error"
            disabled={cerrarMutation.isPending}
          >
            Cerrar Caja
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
