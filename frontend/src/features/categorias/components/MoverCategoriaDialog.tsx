import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Autocomplete,
  TextField,
  CircularProgress,
  Box,
  Typography,
  Alert,
} from '@mui/material';
import { useSnackbar } from 'notistack';
import { categoriasApi } from '@/api/categorias';
import type { CategoriaDTO, CategoriaArbolDTO , ApiError} from '@/types/api';

interface MoverCategoriaDialogProps {
  open: boolean;
  onClose: () => void;
  categoria: CategoriaDTO | CategoriaArbolDTO | null;
}

export function MoverCategoriaDialog({
  open,
  onClose,
  categoria,
}: MoverCategoriaDialogProps) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();
  const [nuevoPadreId, setNuevoPadreId] = useState<number | null>(null);

  // Cargar todas las categorías para el selector
  const { data: categoriasPosibles = [], isLoading: loadingCategorias } = useQuery({
    queryKey: ['categorias', 'posibles'],
    queryFn: () => categoriasApi.getAll(false),
    enabled: open,
    select: (cats) => {
      if (!categoria) return [];

      // Filtrar:
      // 1. No puede ser la misma categoría
      // 2. No puede ser una subcategoría de sí misma
      // 3. Solo nivel 0 y 1 pueden ser padres (para no exceder 3 niveles)
      return cats.filter((c) => {
        if (c.id === categoria.id) return false;

        // Si es CategoriaArbolDTO, verificar subcategorías
        if ('subCategorias' in categoria) {
          const esSubcategoria = (cat: CategoriaArbolDTO, buscarId: number): boolean => {
            if (cat.id === buscarId) return true;
            return cat.subCategorias.some((sub) => esSubcategoria(sub, buscarId));
          };
          if (esSubcategoria(categoria, c.id)) return false;
        }

        // Solo permitir nivel 0 y 1 como padres (máximo 3 niveles)
        return c.nivel < 2;
      });
    },
  });

  useEffect(() => {
    if (open && categoria) {
      setNuevoPadreId(categoria.categoriaPadreId || null);
    }
  }, [open, categoria]);

  const moverMutation = useMutation({
    mutationFn: () =>
      categoriasApi.mover({
        categoriaId: categoria!.id,
        nuevaCategoriaPadreId: nuevoPadreId || undefined,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['categorias'] });
      enqueueSnackbar('Categoría movida correctamente', { variant: 'success' });
      onClose();
    },
    onError: (error: ApiError) => {
      const message = error.message || 'Error al mover la categoría';
      enqueueSnackbar(message, { variant: 'error' });
    },
  });

  const handleMover = () => {
    moverMutation.mutate();
  };

  if (!categoria) return null;

  const padreActual = categoriasPosibles.find((c) => c.id === categoria.categoriaPadreId);
  const nuevoPadre = categoriasPosibles.find((c) => c.id === nuevoPadreId);
  const hayCambios = nuevoPadreId !== (categoria.categoriaPadreId || null);

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Mover Categoría</DialogTitle>

      <DialogContent>
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, mt: 1 }}>
          <Alert severity="info">
            Moviendo: <strong>{categoria.nombre}</strong>
            {categoria.categoriaPadreId && padreActual && (
              <>
                <br />
                Actualmente en: <strong>{padreActual.rutaCompleta}</strong>
              </>
            )}
          </Alert>

          <Autocomplete
            options={[{ id: null, nombre: '(Raíz)', rutaCompleta: 'Raíz' }, ...categoriasPosibles]}
            getOptionLabel={(option) =>
              option.id === null ? 'Raíz (sin categoría padre)' : option.rutaCompleta
            }
            value={
              nuevoPadreId === null
                ? { id: null, nombre: '(Raíz)', rutaCompleta: 'Raíz' }
                : categoriasPosibles.find((c) => c.id === nuevoPadreId) || null
            }
            onChange={(_, newValue) => {
              setNuevoPadreId(newValue?.id || null);
            }}
            loading={loadingCategorias}
            renderInput={(params) => (
              <TextField
                {...params}
                label="Nueva categoría padre"
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

          {hayCambios && nuevoPadre && (
            <Alert severity="success">
              Nueva ubicación: <strong>{nuevoPadre.rutaCompleta} &gt; {categoria.nombre}</strong>
            </Alert>
          )}

          {hayCambios && nuevoPadreId === null && (
            <Alert severity="success">
              Nueva ubicación: <strong>{categoria.nombre}</strong> (raíz)
            </Alert>
          )}

          <Typography variant="caption" color="text.secondary">
            Las subcategorías de esta categoría se moverán automáticamente con ella.
          </Typography>
        </Box>
      </DialogContent>

      <DialogActions>
        <Button onClick={onClose} disabled={moverMutation.isPending}>
          Cancelar
        </Button>
        <Button
          onClick={handleMover}
          variant="contained"
          disabled={moverMutation.isPending || !hayCambios}
        >
          {moverMutation.isPending && <CircularProgress size={20} sx={{ mr: 1 }} />}
          Mover
        </Button>
      </DialogActions>
    </Dialog>
  );
}
