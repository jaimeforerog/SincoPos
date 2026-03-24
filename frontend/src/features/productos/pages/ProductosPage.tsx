import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Box,
  Container,
  Paper,
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Button,
  IconButton,
  TextField,
  MenuItem,
  Chip,
  Alert,
  CircularProgress,
  Tooltip,
  InputAdornment,
  TablePagination,
  alpha,
} from '@mui/material';

const HERO_COLOR = '#1565c0';
import AddIcon from '@mui/icons-material/Add';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import SearchIcon from '@mui/icons-material/Search';
import { useSnackbar } from 'notistack';
import { useAuth } from '@/hooks/useAuth';
import { useAuthStore } from '@/stores/auth.store';
import { productosApi } from '@/api/productos';
import { categoriasApi } from '@/api/categorias';
import { ProductoFormDialog } from '../components/ProductoFormDialog';
import { ReportePageHeader } from '@/features/reportes/components/ReportePageHeader';
import type { ProductoDTO } from '@/types/api';

export function ProductosPage() {
  const { enqueueSnackbar } = useSnackbar();
  const { isSupervisor, isAdmin } = useAuth();
  const { activeEmpresaId } = useAuthStore();
  const queryClient = useQueryClient();

  const [selectedProducto, setSelectedProducto] = useState<ProductoDTO | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [busqueda, setBusqueda] = useState('');
  const [categoriaFiltro, setCategoriaFiltro] = useState<number | undefined>(undefined);
  const [mostrarInactivos, setMostrarInactivos] = useState(false);
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(50);

  // Cargar productos
  const { data: response, isLoading } = useQuery({
    queryKey: ['productos', activeEmpresaId, busqueda, categoriaFiltro, mostrarInactivos, page, pageSize],
    queryFn: () =>
      productosApi.getAll({
        query: busqueda || undefined,
        categoriaId: categoriaFiltro,
        incluirInactivos: mostrarInactivos,
        page: page + 1,
        pageSize: pageSize,
      }),
    refetchInterval: 30000,
  });

  const productos = response?.items ?? [];
  const totalCount = response?.totalCount ?? 0;

  // Cargar categorías para filtro
  const { data: categorias = [] } = useQuery({
    queryKey: ['categorias', activeEmpresaId],
    queryFn: () => categoriasApi.getAll(false),
    staleTime: 0,
  });

  // Mutación para desactivar producto
  const desactivarMutation = useMutation({
    mutationFn: async (producto: ProductoDTO) => {
      await productosApi.delete(producto.id);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['productos'] });
      enqueueSnackbar('Producto desactivado exitosamente', { variant: 'success' });
    },
    onError: (error: any) => {
      const mensaje =
        error.response?.data?.error || 'Error al desactivar el producto';
      enqueueSnackbar(mensaje, { variant: 'error' });
    },
  });

  const handleNuevo = () => {
    setSelectedProducto(null);
    setFormOpen(true);
  };

  const handleEditar = (producto: ProductoDTO) => {
    setSelectedProducto(producto);
    setFormOpen(true);
  };

  const handleDesactivar = (producto: ProductoDTO) => {
    if (
      window.confirm(
        `¿Estás seguro de desactivar el producto "${producto.nombre}"?`
      )
    ) {
      desactivarMutation.mutate(producto);
    }
  };

  const handleSuccess = () => {
    queryClient.invalidateQueries({ queryKey: ['productos'] });
  };

  const formatCurrency = (value: number) => {
    return new Intl.NumberFormat('es-CO', {
      style: 'currency',
      currency: 'COP',
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(value);
  };

  return (
    <Container maxWidth="xl">
      <ReportePageHeader
        title="Productos"
        subtitle="Catálogo de productos, códigos de barras y costos"
        breadcrumbs={[
          { label: 'Configuración', path: '/configuracion' },
          { label: 'Productos' },
        ]}
        backPath="/configuracion"
        color="#1565c0"
        action={
          isSupervisor() ? (
            <Button
              variant="contained"
              startIcon={<AddIcon />}
              onClick={handleNuevo}
              sx={{
                bgcolor: 'rgba(255,255,255,0.15)',
                color: '#fff',
                border: '1px solid rgba(255,255,255,0.35)',
                fontWeight: 700,
                '&:hover': { bgcolor: 'rgba(255,255,255,0.25)', borderColor: '#fff' },
              }}
            >
              Nuevo Producto
            </Button>
          ) : undefined
        }
      />

      {/* Filtros */}
      <Paper sx={{ p: 2, mb: 3 }}>
        <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(250px, 1fr))', gap: 2 }}>
          <TextField
            label="Buscar"
            value={busqueda}
            onChange={(e) => setBusqueda(e.target.value)}
            placeholder="Nombre o código de barras"
            size="small"
            InputProps={{
              startAdornment: (
                <InputAdornment position="start">
                  <SearchIcon />
                </InputAdornment>
              ),
            }}
          />

          <TextField
            select
            label="Categoría"
            value={categoriaFiltro || ''}
            onChange={(e) => setCategoriaFiltro(e.target.value ? Number(e.target.value) : undefined)}
            size="small"
          >
            <MenuItem value="">Todas</MenuItem>
            {categorias
              .sort((a, b) => a.rutaCompleta.localeCompare(b.rutaCompleta))
              .map((cat) => (
                <MenuItem key={cat.id} value={cat.id}>
                  {cat.rutaCompleta}
                </MenuItem>
              ))}
          </TextField>

          <TextField
            select
            label="Estado"
            value={mostrarInactivos ? 'todos' : 'activos'}
            onChange={(e) => setMostrarInactivos(e.target.value === 'todos')}
            size="small"
          >
            <MenuItem value="activos">Solo Activos</MenuItem>
            <MenuItem value="todos">Todos</MenuItem>
          </TextField>

          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <Typography variant="body2" color="text.secondary">
              Total: {totalCount} producto(s)
            </Typography>
          </Box>
        </Box>
      </Paper>

      {/* Tabla de Productos */}
      {isLoading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', p: 4 }}>
          <CircularProgress />
        </Box>
      ) : productos.length === 0 ? (
        <Alert severity="info">
          No se encontraron productos con los filtros seleccionados.
        </Alert>
      ) : (
        <TableContainer component={Paper}>
          <Table>
            <TableHead>
              <TableRow
                sx={{
                  background: `linear-gradient(90deg, ${alpha(HERO_COLOR, 0.08)} 0%, ${alpha(HERO_COLOR, 0.04)} 100%)`,
                  '& .MuiTableCell-head': {
                    color: HERO_COLOR, fontWeight: 700,
                    fontSize: '0.75rem', textTransform: 'uppercase',
                    letterSpacing: '0.04em',
                    borderBottom: `2px solid ${alpha(HERO_COLOR, 0.2)}`,
                  },
                }}
              >
                <TableCell>Código de Barras</TableCell>
                <TableCell>Nombre</TableCell>
                <TableCell>Categoría</TableCell>
                <TableCell align="right">P. Costo</TableCell>
                <TableCell align="center">Estado</TableCell>
                {(isSupervisor() || isAdmin()) && (
                  <TableCell align="center">Acciones</TableCell>
                )}
              </TableRow>
            </TableHead>
            <TableBody>
              {productos.map((producto) => {
                return (
                  <TableRow
                    key={producto.id}
                    hover
                    sx={{
                      '&:last-child td, &:last-child th': { border: 0 },
                      opacity: producto.activo ? 1 : 0.6,
                    }}
                  >
                    <TableCell>
                      <Typography
                        variant="body2"
                        sx={{ fontFamily: 'monospace', fontWeight: 600 }}
                      >
                        {producto.codigoBarras}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>
                        {producto.nombre}
                      </Typography>
                      {producto.descripcion && (
                        <Typography variant="caption" color="text.secondary">
                          {producto.descripcion.length > 50
                            ? `${producto.descripcion.substring(0, 50)}...`
                            : producto.descripcion}
                        </Typography>
                      )}
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" color="text.secondary">
                        {categorias.find((c) => c.id === producto.categoriaId)?.rutaCompleta || 'Sin categoría'}
                      </Typography>
                    </TableCell>
                    <TableCell align="right">
                      {formatCurrency(producto.precioCosto)}
                    </TableCell>
                    <TableCell align="center">
                      <Chip
                        label={producto.activo ? 'Activo' : 'Inactivo'}
                        color={producto.activo ? 'success' : 'default'}
                        size="small"
                      />
                    </TableCell>
                    {(isSupervisor() || isAdmin()) && (
                      <TableCell align="center">
                        <Box sx={{ display: 'flex', gap: 0.5, justifyContent: 'center' }}>
                          {isSupervisor() && producto.activo && (
                            <Tooltip title="Editar">
                              <IconButton
                                size="small"
                                onClick={() => handleEditar(producto)}
                                color="primary"
                              >
                                <EditIcon fontSize="small" />
                              </IconButton>
                            </Tooltip>
                          )}
                          {isAdmin() && producto.activo && (
                            <Tooltip title="Desactivar">
                              <IconButton
                                size="small"
                                onClick={() => handleDesactivar(producto)}
                                color="error"
                              >
                                <DeleteIcon fontSize="small" />
                              </IconButton>
                            </Tooltip>
                          )}
                        </Box>
                      </TableCell>
                    )}
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
          <TablePagination
            component="div"
            count={totalCount}
            page={page}
            onPageChange={(_, newPage) => setPage(newPage)}
            rowsPerPage={pageSize}
            onRowsPerPageChange={(e) => {
              setPageSize(parseInt(e.target.value, 10));
              setPage(0);
            }}
            labelRowsPerPage="Productos por página"
            labelDisplayedRows={({ from, to, count }) =>
              `${from}-${to} de ${count !== -1 ? count : `más de ${to}`}`
            }
          />
        </TableContainer>
      )}

      {/* Diálogo de Formulario */}
      <ProductoFormDialog
        open={formOpen}
        producto={selectedProducto}
        onClose={() => setFormOpen(false)}
        onSuccess={handleSuccess}
      />
    </Container>
  );
}
