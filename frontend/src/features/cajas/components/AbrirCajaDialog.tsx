import { useEffect } from 'react';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient, useQuery } from '@tanstack/react-query';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  MenuItem,
  Alert,
} from '@mui/material';
import { useSnackbar } from 'notistack';
import { cajasApi } from '@/api/cajas';
import { sucursalesApi } from '@/api/sucursales';
import { useAuth } from '@/hooks/useAuth';
import type { CrearCajaDTO, AbrirCajaDTO } from '@/types/api';

const abrirCajaSchema = z.object({
  nombre: z.string().min(1, 'El nombre es requerido'),
  sucursalId: z.number().min(1, 'Debes seleccionar una sucursal'),
  montoApertura: z.number().min(0, 'El monto debe ser positivo'),
});

type AbrirCajaFormData = z.infer<typeof abrirCajaSchema>;

interface AbrirCajaDialogProps {
  open: boolean;
  onClose: () => void;
  defaultSucursalId?: number;
}

export function AbrirCajaDialog({ open, onClose, defaultSucursalId }: AbrirCajaDialogProps) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();
  const { user, activeEmpresaId } = useAuth();

  const { data: todasSucursales = [] } = useQuery({
    queryKey: ['sucursales', activeEmpresaId],
    queryFn: () => sucursalesApi.getAll(),
    staleTime: 0,
  });

  const sucursales = todasSucursales.filter((s) => {
    if (activeEmpresaId != null && s.empresaId != null && s.empresaId !== activeEmpresaId) return false;
    if (user?.sucursalesDisponibles?.length) {
      const asignadasEnEmpresa = user.sucursalesDisponibles.filter(
        (sd) => sd.empresaId === activeEmpresaId || sd.empresaId == null
      );
      if (asignadasEnEmpresa.length > 0) return asignadasEnEmpresa.some((sd) => sd.id === s.id);
    }
    return true;
  });

  const {
    control,
    handleSubmit,
    reset,
    setValue,
    formState: { errors },
  } = useForm<AbrirCajaFormData>({
    resolver: zodResolver(abrirCajaSchema),
    defaultValues: {
      nombre: '',
      sucursalId: defaultSucursalId || 0,
      montoApertura: 0,
    },
  });

  // Pre-seleccionar sucursal si se proporciona
  useEffect(() => {
    if (defaultSucursalId && open) {
      setValue('sucursalId', defaultSucursalId);
    }
  }, [defaultSucursalId, open, setValue]);

  const abrirMutation = useMutation({
    mutationFn: async (data: AbrirCajaFormData) => {
      // Primero crear la caja
      const crearDto: CrearCajaDTO = {
        nombre: data.nombre,
        sucursalId: data.sucursalId,
      };
      console.log('[AbrirCaja] crearDto:', JSON.stringify(crearDto));
      const cajaCreada = await cajasApi.crear(crearDto);

      // Luego abrirla inmediatamente
      const abrirDto: AbrirCajaDTO = {
        montoApertura: data.montoApertura,
      };
      await cajasApi.abrir(cajaCreada.id, abrirDto);

      return cajaCreada;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['cajas'] });
      enqueueSnackbar('Caja creada y abierta correctamente', { variant: 'success' });
      reset();
      onClose();
    },
    onError: (error: Error & { errors?: Record<string, string[]> }) => {
      const fieldErrors = error.errors
        ? Object.entries(error.errors).map(([k, v]) => `${k}: ${v.join(', ')}`).join(' | ')
        : null;
      const msg = fieldErrors ? `${error.message} → ${fieldErrors}` : (error.message || 'Error al crear/abrir la caja');
      enqueueSnackbar(msg, { variant: 'error' });
    },
  });

  const onSubmit = (data: AbrirCajaFormData) => {
    console.log('[AbrirCaja] submit data:', JSON.stringify(data));
    abrirMutation.mutate(data);
  };

  const handleClose = () => {
    reset();
    onClose();
  };

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Crear y Abrir Caja</DialogTitle>
      <form onSubmit={handleSubmit(onSubmit)}>
        <DialogContent>
          {sucursales.length === 0 && (
            <Alert severity="warning" sx={{ mb: 2 }}>
              No hay sucursales disponibles. Debes crear una sucursal primero.
            </Alert>
          )}

          <Controller
            name="nombre"
            control={control}
            render={({ field }) => (
              <TextField
                {...field}
                label="Nombre de la Caja"
                fullWidth
                margin="normal"
                error={!!errors.nombre}
                helperText={errors.nombre?.message}
                placeholder="Ej: Caja 1"
              />
            )}
          />

          <Controller
            name="sucursalId"
            control={control}
            render={({ field }) => (
              <TextField
                {...field}
                onChange={(e) => field.onChange(Number(e.target.value))}
                select
                label="Sucursal"
                fullWidth
                margin="normal"
                error={!!errors.sucursalId}
                helperText={errors.sucursalId?.message}
                disabled={sucursales.length === 0}
              >
                <MenuItem value={0} disabled>
                  {sucursales.length === 0 ? 'No hay sucursales' : 'Seleccione una sucursal'}
                </MenuItem>
                {sucursales.map((sucursal) => (
                  <MenuItem key={sucursal.id} value={sucursal.id}>
                    {sucursal.nombre}
                  </MenuItem>
                ))}
              </TextField>
            )}
          />

          <Controller
            name="montoApertura"
            control={control}
            render={({ field: { onChange, value, ...field } }) => (
              <TextField
                {...field}
                type="number"
                label="Monto de Apertura"
                fullWidth
                margin="normal"
                value={value}
                onChange={(e) => onChange(e.target.value === '' ? 0 : Number(e.target.value))}
                error={!!errors.montoApertura}
                helperText={errors.montoApertura?.message}
                inputProps={{ min: 0, step: 1000 }}
              />
            )}
          />
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={handleClose}>Cancelar</Button>
          <Button
            type="submit"
            variant="contained"
            disabled={abrirMutation.isPending}
          >
            Abrir Caja
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
