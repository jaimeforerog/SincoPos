import { useEffect, useState } from 'react';
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
  MenuItem,
  Autocomplete,
  CircularProgress,
  Box,
} from '@mui/material';
import { useSnackbar } from 'notistack';
import { sucursalesApi } from '@/api/sucursales';
import { getPaises, getCiudadesPorPais } from '@/api/paises';
import type { SucursalDTO, CrearSucursalDTO, ActualizarSucursalDTO } from '@/types/api';

const sucursalSchema = z.object({
  nombre: z.string().min(1, 'El nombre es requerido').max(100),
  direccion: z.string().optional(),
  codigoPais: z.string().optional(),
  nombrePais: z.string().optional(),
  ciudad: z.string().optional(),
  telefono: z.string().optional(),
  email: z.string().email('Email inválido').optional().or(z.literal('')),
  centroCosto: z.string().optional(),
  metodoCosteo: z.enum(['PromedioPonderado', 'PEPS', 'UEPS']).optional(),
});

type SucursalFormData = z.infer<typeof sucursalSchema>;

interface SucursalFormDialogProps {
  open: boolean;
  onClose: () => void;
  sucursal: SucursalDTO | null;
}

export function SucursalFormDialog({ open, onClose, sucursal }: SucursalFormDialogProps) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();
  const isEditing = !!sucursal;
  const [selectedCodigoPais, setSelectedCodigoPais] = useState<string>('CO');

  const {
    control,
    handleSubmit,
    reset,
    setValue,
    formState: { errors },
  } = useForm<SucursalFormData>({
    resolver: zodResolver(sucursalSchema),
    defaultValues: {
      nombre: '',
      direccion: '',
      codigoPais: 'CO',
      nombrePais: 'Colombia',
      ciudad: '',
      telefono: '',
      email: '',
      centroCosto: '',
      metodoCosteo: 'PromedioPonderado',
    },
  });

  // Query para obtener países
  const { data: paises = [], isLoading: loadingPaises } = useQuery({
    queryKey: ['paises'],
    queryFn: getPaises,
    enabled: open,
  });

  // Query para obtener ciudades del país seleccionado
  const { data: ciudades = [], isLoading: loadingCiudades } = useQuery({
    queryKey: ['ciudades', selectedCodigoPais],
    queryFn: () => getCiudadesPorPais(selectedCodigoPais),
    enabled: open && !!selectedCodigoPais,
  });

  useEffect(() => {
    if (sucursal) {
      const codigoPais = sucursal.codigoPais || 'CO';
      setSelectedCodigoPais(codigoPais);
      reset({
        nombre: sucursal.nombre,
        direccion: sucursal.direccion || '',
        codigoPais,
        nombrePais: sucursal.nombrePais || 'Colombia',
        ciudad: sucursal.ciudad || '',
        telefono: sucursal.telefono || '',
        email: sucursal.email || '',
        centroCosto: sucursal.centroCosto || '',
        metodoCosteo: sucursal.metodoCosteo as SucursalFormData['metodoCosteo'],
      });
    } else {
      setSelectedCodigoPais('CO');
      reset({
        nombre: '',
        direccion: '',
        codigoPais: 'CO',
        nombrePais: 'Colombia',
        ciudad: '',
        telefono: '',
        email: '',
        centroCosto: '',
        metodoCosteo: 'PromedioPonderado',
      });
    }
  }, [sucursal, reset]);

  const createMutation = useMutation({
    mutationFn: (data: CrearSucursalDTO) => sucursalesApi.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['sucursales'] });
      enqueueSnackbar('Sucursal creada correctamente', { variant: 'success' });
      onClose();
    },
    onError: (error: any) => {
      enqueueSnackbar(
        error.response?.data?.error || 'Error al crear la sucursal',
        { variant: 'error' }
      );
    },
  });

  const updateMutation = useMutation({
    mutationFn: (data: ActualizarSucursalDTO) =>
      sucursalesApi.update(sucursal!.id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['sucursales'] });
      enqueueSnackbar('Sucursal actualizada correctamente', { variant: 'success' });
      onClose();
    },
    onError: (error: any) => {
      enqueueSnackbar(
        error.response?.data?.error || 'Error al actualizar la sucursal',
        { variant: 'error' }
      );
    },
  });

  const onSubmit = (data: SucursalFormData) => {
    const payload = {
      ...data,
      email: data.email || undefined,
      direccion: data.direccion || undefined,
      codigoPais: data.codigoPais || undefined,
      nombrePais: data.nombrePais || undefined,
      ciudad: data.ciudad || undefined,
      telefono: data.telefono || undefined,
      centroCosto: data.centroCosto || undefined,
    };

    if (isEditing) {
      updateMutation.mutate(payload);
    } else {
      createMutation.mutate(payload);
    }
  };

  const handleClose = () => {
    reset();
    onClose();
  };

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="md" fullWidth>
      <DialogTitle>
        {isEditing ? 'Editar Sucursal' : 'Nueva Sucursal'}
      </DialogTitle>
      <form onSubmit={handleSubmit(onSubmit)}>
        <DialogContent>
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: 'repeat(12, 1fr)',
              gap: 2,
              mt: 1,
            }}
          >
            <Box sx={{ gridColumn: { xs: 'span 12', sm: 'span 6' } }}>
              <Controller
                name="nombre"
                control={control}
                render={({ field }) => (
                  <TextField
                    {...field}
                    label="Nombre *"
                    fullWidth
                    error={!!errors.nombre}
                    helperText={errors.nombre?.message}
                  />
                )}
              />
            </Box>

            <Box sx={{ gridColumn: 'span 12' }}>
              <Controller
                name="codigoPais"
                control={control}
                render={({ field }) => (
                  <Autocomplete
                    options={paises}
                    getOptionLabel={(option) =>
                      typeof option === 'string' ? option : option.nombre
                    }
                    loading={loadingPaises}
                    value={paises.find(p => p.iso2 === field.value) || null}
                    onChange={(_, newValue) => {
                      if (newValue) {
                        field.onChange(newValue.iso2);
                        setValue('nombrePais', newValue.nombre);
                        setSelectedCodigoPais(newValue.iso2);
                        setValue('ciudad', ''); // Limpiar ciudad al cambiar país
                      }
                    }}
                    renderOption={(props, option) => (
                      <li {...props} key={option.iso2}>
                        {option.nombre}
                      </li>
                    )}
                    renderInput={(params) => (
                      <TextField
                        {...params}
                        label="País"
                        placeholder="Buscar país..."
                        error={!!errors.codigoPais}
                        helperText={errors.codigoPais?.message || 'Seleccione el país de la sucursal'}
                        InputProps={{
                          ...params.InputProps,
                          endAdornment: (
                            <>
                              {loadingPaises ? <CircularProgress size={20} /> : null}
                              {params.InputProps.endAdornment}
                            </>
                          ),
                        }}
                      />
                    )}
                  />
                )}
              />
            </Box>

            <Box sx={{ gridColumn: 'span 12' }}>
              <Controller
                name="ciudad"
                control={control}
                render={({ field }) => (
                  <Autocomplete
                    options={ciudades}
                    getOptionLabel={(option) =>
                      typeof option === 'string' ? option : option.nombre
                    }
                    loading={loadingCiudades}
                    freeSolo
                    value={field.value || null}
                    onChange={(_, newValue) => {
                      if (typeof newValue === 'string') {
                        field.onChange(newValue);
                      } else if (newValue) {
                        field.onChange(newValue.nombre);
                      }
                    }}
                    onInputChange={(_, newInputValue) => {
                      field.onChange(newInputValue);
                    }}
                    disabled={!selectedCodigoPais}
                    renderInput={(params) => (
                      <TextField
                        {...params}
                        label="Ciudad"
                        placeholder={ciudades.length > 0 ? "Seleccione o escriba una ciudad..." : "Escriba el nombre de la ciudad..."}
                        error={!!errors.ciudad}
                        helperText={
                          errors.ciudad?.message ||
                          (!selectedCodigoPais
                            ? 'Primero seleccione un país'
                            : ciudades.length > 0
                              ? `${ciudades.length} ciudades disponibles - Puede escribir una diferente`
                              : 'No hay ciudades predefinidas - Escriba el nombre de la ciudad')
                        }
                        InputProps={{
                          ...params.InputProps,
                          endAdornment: (
                            <>
                              {loadingCiudades ? <CircularProgress size={20} /> : null}
                              {params.InputProps.endAdornment}
                            </>
                          ),
                        }}
                      />
                    )}
                  />
                )}
              />
            </Box>

            <Box sx={{ gridColumn: 'span 12' }}>
              <Controller
                name="direccion"
                control={control}
                render={({ field }) => (
                  <TextField
                    {...field}
                    label="Dirección"
                    fullWidth
                    error={!!errors.direccion}
                    helperText={errors.direccion?.message}
                  />
                )}
              />
            </Box>

            <Box sx={{ gridColumn: { xs: 'span 12', sm: 'span 6' } }}>
              <Controller
                name="telefono"
                control={control}
                render={({ field }) => (
                  <TextField
                    {...field}
                    label="Teléfono"
                    fullWidth
                    error={!!errors.telefono}
                    helperText={errors.telefono?.message}
                  />
                )}
              />
            </Box>

            <Box sx={{ gridColumn: { xs: 'span 12', sm: 'span 6' } }}>
              <Controller
                name="email"
                control={control}
                render={({ field }) => (
                  <TextField
                    {...field}
                    label="Email"
                    type="email"
                    fullWidth
                    error={!!errors.email}
                    helperText={errors.email?.message}
                  />
                )}
              />
            </Box>

            <Box sx={{ gridColumn: { xs: 'span 12', sm: 'span 6' } }}>
              <Controller
                name="centroCosto"
                control={control}
                render={({ field }) => (
                  <TextField
                    {...field}
                    label="Centro de Costo (ERP)"
                    fullWidth
                    error={!!errors.centroCosto}
                    helperText={errors.centroCosto?.message}
                  />
                )}
              />
            </Box>

            <Box sx={{ gridColumn: { xs: 'span 12', sm: 'span 6' } }}>
              <Controller
                name="metodoCosteo"
                control={control}
                render={({ field }) => (
                  <TextField
                    {...field}
                    select
                    label="Método de Costeo"
                    fullWidth
                    error={!!errors.metodoCosteo}
                    helperText={errors.metodoCosteo?.message}
                  >
                    <MenuItem value="PromedioPonderado">Promedio Ponderado</MenuItem>
                    <MenuItem value="PEPS">PEPS (Primero en Entrar, Primero en Salir)</MenuItem>
                    <MenuItem value="UEPS">UEPS (Último en Entrar, Primero en Salir)</MenuItem>
                  </TextField>
                )}
              />
            </Box>
          </Box>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={handleClose}>Cancelar</Button>
          <Button
            type="submit"
            variant="contained"
            disabled={createMutation.isPending || updateMutation.isPending}
          >
            {isEditing ? 'Actualizar' : 'Crear'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
