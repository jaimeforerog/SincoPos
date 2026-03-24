import { useState } from 'react';
import { AxiosError } from 'axios';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Box,
  Button,
  Card,
  CardContent,
  Container,
  Switch,
  FormControlLabel,
  CircularProgress,
  Typography,
} from '@mui/material';
import { Add as AddIcon } from '@mui/icons-material';
import { categoriasApi } from '@/api/categorias';
import type { CategoriaDTO, CategoriaArbolDTO } from '@/types/api';
import { useSnackbar } from 'notistack';
import { useAuthStore } from '@/stores/auth.store';
import { CategoriaTreeView } from '../components/CategoriaTreeView';
import { CategoriaFormDialog } from '../components/CategoriaFormDialog';
import { MoverCategoriaDialog } from '../components/MoverCategoriaDialog';
import { ReportePageHeader } from '@/features/reportes/components/ReportePageHeader';

export function CategoriasPage() {
  const [openFormDialog, setOpenFormDialog] = useState(false);
  const [openMoverDialog, setOpenMoverDialog] = useState(false);
  const [selectedCategoria, setSelectedCategoria] = useState<CategoriaDTO | CategoriaArbolDTO | null>(null);
  const [padreIdParaNueva, setPadreIdParaNueva] = useState<number | undefined>();
  const [incluirInactivas, setIncluirInactivas] = useState(false);
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();
  const { activeEmpresaId } = useAuthStore();

  // Cargar árbol de categorías
  const { data: categorias = [], isLoading } = useQuery({
    queryKey: ['categorias', 'arbol', activeEmpresaId, incluirInactivas],
    queryFn: () => categoriasApi.getArbol(incluirInactivas),
    staleTime: 0,
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => categoriasApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['categorias'] });
      enqueueSnackbar('Categoría eliminada correctamente', { variant: 'success' });
    },
    onError: (error: Error) => {
      const axiosError = error as AxiosError<{ error?: string }>;
      const message = axiosError.response?.data?.error || 'Error al eliminar la categoría';
      enqueueSnackbar(message, { variant: 'error' });
    },
  });

  const handleCreate = () => {
    setSelectedCategoria(null);
    setPadreIdParaNueva(undefined);
    setOpenFormDialog(true);
  };

  const handleAddSubcategoria = (padreId: number) => {
    setSelectedCategoria(null);
    setPadreIdParaNueva(padreId);
    setOpenFormDialog(true);
  };

  const handleEdit = (categoria: CategoriaDTO | CategoriaArbolDTO) => {
    setSelectedCategoria(categoria);
    setPadreIdParaNueva(undefined);
    setOpenFormDialog(true);
  };

  const handleMover = (categoria: CategoriaDTO | CategoriaArbolDTO) => {
    setSelectedCategoria(categoria);
    setOpenMoverDialog(true);
  };

  const handleDelete = (id: number) => {
    if (confirm('¿Está seguro de eliminar esta categoría?')) {
      deleteMutation.mutate(id);
    }
  };

  const handleCloseFormDialog = () => {
    setOpenFormDialog(false);
    setSelectedCategoria(null);
    setPadreIdParaNueva(undefined);
  };

  const handleCloseMoverDialog = () => {
    setOpenMoverDialog(false);
    setSelectedCategoria(null);
  };

  return (
    <Container maxWidth="xl">
      <ReportePageHeader
        title="Categorías"
        subtitle="Estructura jerárquica de categorías y márgenes de ganancia"
        breadcrumbs={[
          { label: 'Configuración', path: '/configuracion' },
          { label: 'Categorías' },
        ]}
        backPath="/configuracion"
        color="#1565c0"
        action={
          <Button
            variant="contained"
            startIcon={<AddIcon />}
            onClick={handleCreate}
            sx={{
              bgcolor: 'rgba(255,255,255,0.15)',
              color: '#fff',
              border: '1px solid rgba(255,255,255,0.35)',
              fontWeight: 700,
              '&:hover': { bgcolor: 'rgba(255,255,255,0.25)', borderColor: '#fff' },
            }}
          >
            Nueva Categoría
          </Button>
        }
      />

      <Card>
        <CardContent>
          <Box mb={2} sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <FormControlLabel
              control={
                <Switch
                  checked={incluirInactivas}
                  onChange={(e) => setIncluirInactivas(e.target.checked)}
                />
              }
              label="Incluir categorías inactivas"
            />

            <Typography variant="caption" color="text.secondary">
              Máximo 3 niveles de profundidad
            </Typography>
          </Box>

          {isLoading ? (
            <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
              <CircularProgress />
            </Box>
          ) : (
            <CategoriaTreeView
              categorias={categorias}
              onEdit={handleEdit}
              onDelete={handleDelete}
              onMover={handleMover}
              onAddSubcategoria={handleAddSubcategoria}
            />
          )}
        </CardContent>
      </Card>

      <CategoriaFormDialog
        open={openFormDialog}
        onClose={handleCloseFormDialog}
        categoria={selectedCategoria as CategoriaDTO | null}
        padreId={padreIdParaNueva}
      />

      <MoverCategoriaDialog
        open={openMoverDialog}
        onClose={handleCloseMoverDialog}
        categoria={selectedCategoria}
      />
    </Container>
  );
}
