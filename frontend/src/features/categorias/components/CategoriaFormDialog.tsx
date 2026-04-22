import { useEffect } from 'react';
import { AxiosError } from 'axios';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  Autocomplete,
  CircularProgress,
  Box,
  Typography,
} from '@mui/material';
import { useSnackbar } from 'notistack';
import { categoriasApi } from '@/api/categorias';
import type { CategoriaDTO, CrearCategoriaDTO, ActualizarCategoriaDTO } from '@/types/api';

const categoriaSchema = z.object({
  nombre: z.string().min(1, 'El nombre es requerido').max(100),
  descripcion: z.string().optional(),
  categoriaPadreId: z.number().optional().nullable(),
  margenGanancia: z.number({ message: 'Ingrese un margen válido' }).min(0, 'No puede ser negativo').max(10, 'No puede superar 1000%'),
  cuentaInventario: z.string().regex(/^\d{4,6}$/, 'Debe ser un código PUC (4 a 6 dígitos numéricos)').nullable().optional().or(z.literal('')),
  cuentaCosto: z.string().regex(/^\d{4,6}$/, 'Debe ser un código PUC (4 a 6 dígitos numéricos)').nullable().optional().or(z.literal('')),
  cuentaIngreso: z.string().regex(/^\d{4,6}$/, 'Debe ser un código PUC (4 a 6 dígitos numéricos)').nullable().optional().or(z.literal('')),
  externalId: z.string().optional().nullable().or(z.literal('')),
  origenDatos: z.string().optional(),
});

type CategoriaFormData = z.infer<typeof categoriaSchema>;

interface CategoriaFormDialogProps {
  open: boolean;
  onClose: () => void;
  categoria: CategoriaDTO | null;
  padreId?: number; // Para crear subcategorías directamente
}

export function CategoriaFormDialog({
  open,
  onClose,
  categoria,
  padreId,
}: CategoriaFormDialogProps) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();
  const isEditing = !!categoria;

  const {
    control,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<CategoriaFormData>({
    resolver: zodResolver(categoriaSchema),
    defaultValues: {
      nombre: '',
      descripcion: '',
      categoriaPadreId: padreId || null,
      margenGanancia: 0.30,
      cuentaInventario: '',
      cuentaCosto: '',
      cuentaIngreso: '',
      externalId: '',
      origenDatos: 'Local',
    },
  });

  // Cargar categorías para el selector de padre (solo raíz y nivel 1)
  const { data: categoriasPosibles = [], isLoading: loadingCategorias } = useQuery({
    queryKey: ['categorias', 'posibles'],
    queryFn: () => categoriasApi.getAll(false),
    enabled: open,
    select: (cats) => {
      // Filtrar categorías que pueden ser padres (solo nivel 0 y 1)
      // y excluir la categoría actual si estamos editando
      return cats.filter(
        (c) => c.nivel < 2 && (!isEditing || c.id !== categoria?.id)
      );
    },
  });

  useEffect(() => {
    if (open && categoria) {
      reset({
        nombre: categoria.nombre,
        descripcion: categoria.descripcion || '',
        categoriaPadreId: categoria.categoriaPadreId || null,
        margenGanancia: categoria.margenGanancia ?? 0.30,
        cuentaInventario: categoria.cuentaInventario || '',
        cuentaCosto: categoria.cuentaCosto || '',
        cuentaIngreso: categoria.cuentaIngreso || '',
        externalId: categoria.externalId || '',
        origenDatos: categoria.origenDatos || 'Local',
      });
    } else if (open && padreId) {
      reset({
        nombre: '',
        descripcion: '',
        categoriaPadreId: padreId,
        margenGanancia: 0.30,
        cuentaInventario: '',
        cuentaCosto: '',
        cuentaIngreso: '',
        externalId: '',
        origenDatos: 'Local',
      });
    } else if (open) {
      reset({
        nombre: '',
        descripcion: '',
        categoriaPadreId: null,
        margenGanancia: 0.30,
        cuentaInventario: '',
        cuentaCosto: '',
        cuentaIngreso: '',
        externalId: '',
        origenDatos: 'Local',
      });
    }
  }, [open, categoria, padreId, reset]);

  const createMutation = useMutation({
    mutationFn: (data: CrearCategoriaDTO) => categoriasApi.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['categorias'] });
      enqueueSnackbar('Categoría creada correctamente', { variant: 'success' });
      onClose();
    },
    onError: (error: Error) => {
      const axiosError = error as AxiosError<{ error?: string; errors?: { Nombre?: string[] } }>;
      const message = axiosError.response?.data?.error || axiosError.response?.data?.errors?.Nombre?.[0] || 'Error al crear la categoría';
      enqueueSnackbar(message, { variant: 'error' });
    },
  });

  const updateMutation = useMutation({
    mutationFn: (data: { id: number; categoria: ActualizarCategoriaDTO }) =>
      categoriasApi.update(data.id, data.categoria),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['categorias'] });
      enqueueSnackbar('Categoría actualizada correctamente', { variant: 'success' });
      onClose();
    },
    onError: (error: Error) => {
      const axiosError = error as AxiosError<{ error?: string; errors?: { Nombre?: string[] } }>;
      const message = axiosError.response?.data?.error || axiosError.response?.data?.errors?.Nombre?.[0] || 'Error al actualizar la categoría';
      enqueueSnackbar(message, { variant: 'error' });
    },
  });

  const onSubmit = (data: CategoriaFormData) => {
    const categoriaData = {
      nombre: data.nombre,
      descripcion: data.descripcion || undefined,
      categoriaPadreId: data.categoriaPadreId || undefined,
      margenGanancia: data.margenGanancia,
      cuentaInventario: data.cuentaInventario || undefined,
      cuentaCosto: data.cuentaCosto || undefined,
      cuentaIngreso: data.cuentaIngreso || undefined,
      externalId: data.externalId || undefined,
      origenDatos: data.origenDatos || 'Local',
    };

    if (isEditing) {
      updateMutation.mutate({ id: categoria!.id, categoria: categoriaData });
    } else {
      createMutation.mutate(categoriaData);
    }
  };

  const isLoading = createMutation.isPending || updateMutation.isPending;

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>
        {isEditing ? 'Editar Categoría' : padreId ? 'Nueva Subcategoría' : 'Nueva Categoría'}
      </DialogTitle>

      <form onSubmit={handleSubmit(onSubmit)}>
        <DialogContent>
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, mt: 1 }}>
            <Controller
              name="nombre"
              control={control}
              render={({ field }) => (
                <TextField
                  {...field}
                  label="Nombre"
                  error={!!errors.nombre}
                  helperText={errors.nombre?.message}
                  required
                  autoFocus
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
                  multiline
                  rows={3}
                  error={!!errors.descripcion}
                  helperText={errors.descripcion?.message}
                />
              )}
            />

            <Controller
              name="margenGanancia"
              control={control}
              render={({ field: { value, onChange, ...field } }) => (
                <TextField
                  {...field}
                  type="number"
                  label="Margen de ganancia (%)"
                  value={Number.isNaN(value) ? '' : Math.round(value * 100)}
                  onChange={(e) => onChange(e.target.value === '' ? NaN : Number(e.target.value) / 100)}
                  error={!!errors.margenGanancia}
                  helperText={errors.margenGanancia?.message || 'Fallback cuando el producto no tiene precio configurado. Ej: 30 = 30%'}
                  inputProps={{ min: 0, max: 1000, step: 1 }}
                  fullWidth
                />
              )}
            />

            <Controller
              name="categoriaPadreId"
              control={control}
              render={({ field: { onChange, value, ...field } }) => (
                <Autocomplete
                  {...field}
                  options={categoriasPosibles}
                  getOptionLabel={(option) =>
                    typeof option === 'number'
                      ? categoriasPosibles.find((c) => c.id === option)?.rutaCompleta || ''
                      : option.rutaCompleta
                  }
                  value={categoriasPosibles.find((c) => c.id === value) || null}
                  onChange={(_, newValue) => onChange(newValue?.id || null)}
                  loading={loadingCategorias}
                  disabled={!!padreId}
                  renderInput={(params) => (
                    <TextField
                      {...params}
                      label="Categoría Padre (opcional)"
                      InputProps={{
                        ...params.InputProps,
                        endAdornment: (
                          <>
                            {loadingCategorias && <CircularProgress color="inherit" size={20} />}
                            {params.InputProps.endAdornment}
                          </>
                        ),
                      }}
                    />
                  )}
                />
              )}
            />

            {padreId && (
              <Typography variant="caption" color="text.secondary">
                Esta categoría se creará como subcategoría de la categoría seleccionada
              </Typography>
            )}

            <Typography variant="subtitle2" sx={{ mt: 1 }}>Configuración Integración Contable (ERP)</Typography>
            
            <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 2 }}>
              <Controller
                name="cuentaInventario"
                control={control}
                render={({ field }) => (
                  <TextField
                    {...field}
                    label="Cuenta Inventario (PUC)"
                    helperText={errors.cuentaInventario?.message || 'Ej. 1435'}
                    error={!!errors.cuentaInventario}
                  />
                )}
              />

              <Controller
                name="cuentaCosto"
                control={control}
                render={({ field }) => (
                  <TextField
                    {...field}
                    label="Cuenta Costo (PUC)"
                    helperText={errors.cuentaCosto?.message || 'Ej. 6135'}
                    error={!!errors.cuentaCosto}
                  />
                )}
              />

              <Controller
                name="cuentaIngreso"
                control={control}
                render={({ field }) => (
                  <TextField
                    {...field}
                    label="Cuenta Ingreso (PUC)"
                    helperText={errors.cuentaIngreso?.message || 'Ej. 4135'}
                    error={!!errors.cuentaIngreso}
                  />
                )}
              />

              <Controller
                name="externalId"
                control={control}
                render={({ field }) => (
                  <TextField
                    {...field}
                    label="ID Externo (ERP)"
                    helperText={errors.externalId?.message}
                    error={!!errors.externalId}
                  />
                )}
              />
            </Box>

            {!padreId && (
              <Typography variant="caption" color="text.secondary">
                Las categorías pueden tener hasta 3 niveles de profundidad
              </Typography>
            )}
          </Box>
        </DialogContent>

        <DialogActions>
          <Button onClick={onClose} disabled={isLoading}>
            Cancelar
          </Button>
          <Button type="submit" variant="contained" disabled={isLoading}>
            {isLoading && <CircularProgress size={20} sx={{ mr: 1 }} />}
            {isEditing ? 'Actualizar' : 'Crear'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
