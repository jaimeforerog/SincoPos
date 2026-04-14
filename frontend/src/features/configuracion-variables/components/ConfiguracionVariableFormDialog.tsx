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
  Box,
} from '@mui/material';
import { useSnackbar } from 'notistack';
import { configuracionVariablesApi } from '@/api/configuracionVariables';
import type { ConfiguracionVariableDTO } from '@/types/api';

const schema = z.object({
  nombre: z
    .string()
    .min(1, 'El nombre es requerido')
    .max(100)
    .regex(/^[a-zA-Z0-9_]+$/, 'Solo letras, números y guiones bajos'),
  valor: z.string().min(1, 'El valor es requerido').max(500),
  descripcion: z.string().max(500).optional().or(z.literal('')),
});

type FormData = z.infer<typeof schema>;

interface Props {
  open: boolean;
  onClose: () => void;
  variable: ConfiguracionVariableDTO | null;
}

export function ConfiguracionVariableFormDialog({ open, onClose, variable }: Props) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();
  const isEditing = !!variable;

  const {
    control,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { nombre: '', valor: '', descripcion: '' },
  });

  useEffect(() => {
    if (variable) {
      reset({
        nombre: variable.nombre,
        valor: variable.valor,
        descripcion: variable.descripcion ?? '',
      });
    } else {
      reset({ nombre: '', valor: '', descripcion: '' });
    }
  }, [variable, reset]);

  const createMutation = useMutation({
    mutationFn: (data: FormData) =>
      configuracionVariablesApi.create({
        nombre: data.nombre,
        valor: data.valor,
        descripcion: data.descripcion || undefined,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['configuracion-variables'] });
      queryClient.invalidateQueries({ queryKey: ['configuracion-variable'] });
      enqueueSnackbar('Variable creada correctamente', { variant: 'success' });
      onClose();
    },
    onError: (error: any) => {
      enqueueSnackbar(
        error.response?.data?.detail || error.response?.data?.title || 'Error al crear la variable',
        { variant: 'error' }
      );
    },
  });

  const updateMutation = useMutation({
    mutationFn: (data: FormData) =>
      configuracionVariablesApi.update(variable!.id, {
        nombre: data.nombre,
        valor: data.valor,
        descripcion: data.descripcion || undefined,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['configuracion-variables'] });
      queryClient.invalidateQueries({ queryKey: ['configuracion-variable'] });
      enqueueSnackbar('Variable actualizada correctamente', { variant: 'success' });
      onClose();
    },
    onError: (error: any) => {
      enqueueSnackbar(
        error.response?.data?.detail || error.response?.data?.title || 'Error al actualizar la variable',
        { variant: 'error' }
      );
    },
  });

  const onSubmit = (data: FormData) => {
    if (isEditing) updateMutation.mutate(data);
    else createMutation.mutate(data);
  };

  const handleClose = () => {
    reset();
    onClose();
  };

  const isPending = createMutation.isPending || updateMutation.isPending;

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>{isEditing ? 'Editar Variable' : 'Nueva Variable'}</DialogTitle>
      <form onSubmit={handleSubmit(onSubmit)}>
        <DialogContent>
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, mt: 1 }}>
            <Controller
              name="nombre"
              control={control}
              render={({ field }) => (
                <TextField
                  {...field}
                  label="Nombre *"
                  fullWidth
                  placeholder="ej: AperturaCaja_MontoMax"
                  error={!!errors.nombre}
                  helperText={errors.nombre?.message || 'Clave única. Solo letras, números y guión bajo.'}
                />
              )}
            />

            <Controller
              name="valor"
              control={control}
              render={({ field }) => (
                <TextField
                  {...field}
                  label="Valor *"
                  fullWidth
                  error={!!errors.valor}
                  helperText={errors.valor?.message}
                />
              )}
            />

            <Controller
              name="descripcion"
              control={control}
              render={({ field }) => (
                <TextField
                  {...field}
                  label="Descripción"
                  fullWidth
                  multiline
                  rows={3}
                  error={!!errors.descripcion}
                  helperText={errors.descripcion?.message}
                />
              )}
            />
          </Box>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={handleClose}>Cancelar</Button>
          <Button type="submit" variant="contained" disabled={isPending}>
            {isEditing ? 'Actualizar' : 'Crear'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
