import { useState } from 'react';
import { Controller, type Control, type FieldErrors } from 'react-hook-form';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Box,
  Button,
  TextField,
  IconButton,
  Tooltip,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import { useSnackbar } from 'notistack';
import { categoriasApi } from '@/api/categorias';
import type { CategoriaDTO, ApiError } from '@/types/api';
import type { CrearProductoFormData } from './productoSchema';

interface CategoriaSelectorFieldProps {
  control: Control<CrearProductoFormData>;
  errors: FieldErrors<CrearProductoFormData>;
  categorias: CategoriaDTO[];
}

export function CategoriaSelectorField({ control, errors, categorias }: CategoriaSelectorFieldProps) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();
  const [showCategoriaInput, setShowCategoriaInput] = useState(false);
  const [nuevaCategoria, setNuevaCategoria] = useState('');

  const crearCategoriaMutation = useMutation({
    mutationFn: (nombre: string) => categoriasApi.create({ nombre }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['categorias'] });
      enqueueSnackbar('Categoría creada exitosamente', { variant: 'success' });
      setShowCategoriaInput(false);
      setNuevaCategoria('');
    },
    onError: (error: ApiError) => {
      enqueueSnackbar(error.message || 'Error al crear la categoría', { variant: 'error' });
    },
  });

  const handleCrearCategoria = () => {
    if (nuevaCategoria.trim()) {
      crearCategoriaMutation.mutate(nuevaCategoria.trim());
    }
  };

  if (showCategoriaInput) {
    return (
      <Box sx={{ display: 'flex', gap: 1 }}>
        <TextField
          label="Nueva Categoría"
          value={nuevaCategoria}
          onChange={(e) => setNuevaCategoria(e.target.value)}
          fullWidth
          autoFocus
        />
        <Button
          onClick={handleCrearCategoria}
          variant="contained"
          disabled={!nuevaCategoria.trim() || crearCategoriaMutation.isPending}
        >
          Crear
        </Button>
        <Button onClick={() => setShowCategoriaInput(false)}>Cancelar</Button>
      </Box>
    );
  }

  return (
    <Box sx={{ display: 'flex', gap: 1 }}>
      <Controller
        name="categoriaId"
        control={control}
        render={({ field: { value, onChange, ...field } }) => (
          <FormControl fullWidth error={!!errors.categoriaId}>
            <InputLabel>Categoría *</InputLabel>
            <Select
              {...field}
              value={value || ''}
              onChange={(e) => onChange(Number(e.target.value))}
              label="Categoría *"
            >
              <MenuItem value="">
                <em>Selecciona una categoría</em>
              </MenuItem>
              {categorias
                .sort((a, b) => a.rutaCompleta.localeCompare(b.rutaCompleta))
                .map((cat) => (
                  <MenuItem
                    key={cat.id}
                    value={cat.id}
                    sx={{ pl: cat.nivel * 2 + 2 }}
                  >
                    {cat.rutaCompleta}
                  </MenuItem>
                ))}
            </Select>
            {errors.categoriaId && (
              <Box component="span" sx={{ color: 'error.main', fontSize: '0.75rem', mt: 0.5 }}>
                {errors.categoriaId.message}
              </Box>
            )}
          </FormControl>
        )}
      />
      <Tooltip title="Crear nueva categoría">
        <IconButton onClick={() => setShowCategoriaInput(true)} color="primary">
          <AddIcon />
        </IconButton>
      </Tooltip>
    </Box>
  );
}
