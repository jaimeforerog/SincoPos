import { useState, useEffect, useRef } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
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
  Alert,
  CircularProgress,
  Tooltip,
  InputAdornment,
  Chip,
  alpha,
} from '@mui/material';

const HERO_COLOR = '#1565c0';
import EditIcon from '@mui/icons-material/Edit';
import SearchIcon from '@mui/icons-material/Search';
import UploadFileIcon from '@mui/icons-material/UploadFile';
import { useSnackbar } from 'notistack';
import { useAuth } from '@/hooks/useAuth';
import { productosApi } from '@/api/productos';
import { preciosApi } from '@/api/precios';
import { sucursalesApi } from '@/api/sucursales';
import { EditarPrecioDialog } from '../components/EditarPrecioDialog';
import { ImportarPreciosDialog } from '../components/ImportarPreciosDialog';
import { ReportePageHeader } from '@/features/reportes/components/ReportePageHeader';
import type { ProductoDTO, PrecioResueltoDTO } from '@/types/api';

interface ProductoConPrecio extends ProductoDTO {
  precioResuelto?: PrecioResueltoDTO;
  cargandoPrecio?: boolean;
}

export function PreciosPage() {
  const { enqueueSnackbar } = useSnackbar();
  const { isSupervisor, user, activeEmpresaId } = useAuth();
  const queryClient = useQueryClient();

  const [selectedSucursalId, setSelectedSucursalId] = useState<number | null>(null);
  const [busqueda, setBusqueda] = useState('');
  const [selectedProducto, setSelectedProducto] = useState<ProductoDTO | null>(null);
  const [precioActual, setPrecioActual] = useState<PrecioResueltoDTO | undefined>(undefined);
  const [formOpen, setFormOpen] = useState(false);
  const [importOpen, setImportOpen] = useState(false);

  // Limpiar búsqueda cuando cambia la sucursal
  useEffect(() => {
    setBusqueda('');
  }, [selectedSucursalId]);

  const { data: todasSucursales = [] } = useQuery({
    queryKey: ['sucursales'],
    queryFn: () => sucursalesApi.getAll(),
    staleTime: 5 * 60 * 1000,
  });

  const sucursales = todasSucursales.filter(
    (s) =>
      (activeEmpresaId == null || s.empresaId === activeEmpresaId || s.empresaId == null) &&
      (!user?.sucursalesDisponibles?.length || user.sucursalesDisponibles.some((sd) => sd.id === s.id))
  );

  // Cargar productos activos (solo si hay sucursal seleccionada)
  const { data: productosData, isLoading: loadingProductos } = useQuery({
    queryKey: ['productos', busqueda, activeEmpresaId],
    queryFn: () =>
      productosApi.getAll({
        query: busqueda || undefined,
        incluirInactivos: false,
      }),
    enabled: selectedSucursalId !== null,
    staleTime: 30000, // Evitar recargas innecesarias
    refetchOnWindowFocus: false, // No recargar al cambiar de ventana
  });
  const productos = productosData?.items || [];

  // Cargar precios resueltos para los productos visibles
  const [productosConPrecios, setProductosConPrecios] = useState<ProductoConPrecio[]>([]);
  const lastLoadKeyRef = useRef<string>('');

  useEffect(() => {
    // Limpiar productos si no hay sucursal seleccionada
    if (!selectedSucursalId) {
      if (productosConPrecios.length > 0 || lastLoadKeyRef.current !== '') {
        setProductosConPrecios([]);
        lastLoadKeyRef.current = '';
      }
      return;
    }

    // Crear clave única para evitar recargas innecesarias
    const loadKey = `${selectedSucursalId}-${productos.map(p => p.id).sort().join(',')}`;

    // Si ya cargamos estos datos, no hacer nada
    if (loadKey === lastLoadKeyRef.current) {
      return;
    }

    // Solo cargar si hay productos y sucursal
    if (productos.length > 0 && selectedSucursalId) {
      lastLoadKeyRef.current = loadKey;

      const productosIniciales: ProductoConPrecio[] = productos.map((p) => ({
        ...p,
        cargandoPrecio: true,
      }));
      setProductosConPrecios(productosIniciales);

      // Cargar precios en paralelo - usando el mismo loadKey para validar
      const currentLoadKey = loadKey;
      Promise.all(
        productos.map((producto) =>
          preciosApi
            .resolver(producto.id, selectedSucursalId)
            .then((precio) => ({ productoId: producto.id, precio }))
            .catch(() => ({ productoId: producto.id, precio: undefined }))
        )
      ).then((resultados) => {
        // Solo actualizar si seguimos en el mismo contexto
        if (currentLoadKey === lastLoadKeyRef.current) {
          setProductosConPrecios((prev) =>
            prev.map((p) => {
              const resultado = resultados.find((r) => r.productoId === p.id);
              return {
                ...p,
                precioResuelto: resultado?.precio,
                cargandoPrecio: false,
              };
            })
          );
        }
      });
    } else if (productos.length === 0) {
      if (productosConPrecios.length > 0 || lastLoadKeyRef.current !== '') {
        setProductosConPrecios([]);
        lastLoadKeyRef.current = '';
      }
    }
  }, [productos, selectedSucursalId, productosConPrecios.length]);

  const handleEditarPrecio = async (producto: ProductoConPrecio) => {
    setSelectedProducto(producto);
    setPrecioActual(producto.precioResuelto);
    setFormOpen(true);
  };

  const handleSuccess = () => {
    queryClient.invalidateQueries({ queryKey: ['precios'] });
    // Forzar recarga limpiando el lastLoadKeyRef
    lastLoadKeyRef.current = '';
  };

  const handleImportPrecios = async (
    precios: Array<{ productoId: string; precioVenta: number; precioMinimo?: number }>
  ) => {
    if (!selectedSucursalId) return;

    let importados = 0;
    let errores = 0;

    for (const precio of precios) {
      try {
        await preciosApi.createOrUpdate({
          productoId: precio.productoId,
          sucursalId: selectedSucursalId,
          precioVenta: precio.precioVenta,
          precioMinimo: precio.precioMinimo,
          origenDato: 'Migrado',  // Marcar como migrado desde Excel
        });
        importados++;
      } catch (error) {
        errores++;
      }
    }

    if (errores > 0) {
      enqueueSnackbar(
        `Importados: ${importados}, Errores: ${errores}`,
        { variant: 'warning' }
      );
    }

    handleSuccess();
  };

  const formatCurrency = (value: number) => {
    return new Intl.NumberFormat('es-CO', {
      style: 'currency',
      currency: 'COP',
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(value);
  };

  const calcularMargen = (costo: number, venta: number) => {
    if (costo === 0) return 0;
    return ((venta - costo) / costo) * 100;
  };

  const sucursalSeleccionada = sucursales.find((s) => s.id === selectedSucursalId);

  // Solo mostrar productos si hay sucursal seleccionada
  const productosAMostrar = selectedSucursalId ? productosConPrecios : [];

  // Crear mapa de precios actuales para la plantilla
  const preciosActualesMap = new Map(
    productosConPrecios.map((p) => [
      p.id,
      {
        precioVenta: p.precioResuelto?.precioVenta || 0,
        precioMinimo: p.precioResuelto?.precioMinimo,
      },
    ])
  );

  return (
    <Container maxWidth="xl">
      <ReportePageHeader
        title="Precios Sucursal"
        subtitle="Configuración de precios por sucursal e importación masiva"
        breadcrumbs={[
          { label: 'Configuración', path: '/configuracion' },
          { label: 'Precios Sucursal' },
        ]}
        backPath="/configuracion"
        color="#1565c0"
      />

      {/* Selector de Sucursal */}
      <Paper sx={{ p: 2, mb: 3 }}>
        <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(250px, 1fr))', gap: 2 }}>
          <TextField
            select
            label="Sucursal *"
            value={selectedSucursalId || ''}
            onChange={(e) => setSelectedSucursalId(Number(e.target.value))}
            size="small"
            required
          >
            {sucursales.map((suc) => (
              <MenuItem key={suc.id} value={suc.id}>
                {suc.nombre}
              </MenuItem>
            ))}
          </TextField>

          <TextField
            label="Buscar Producto"
            value={busqueda}
            onChange={(e) => setBusqueda(e.target.value)}
            placeholder="Nombre o código de barras"
            size="small"
            disabled={!selectedSucursalId}
            InputProps={{
              startAdornment: (
                <InputAdornment position="start">
                  <SearchIcon />
                </InputAdornment>
              ),
            }}
          />

          {isSupervisor() && selectedSucursalId && (
            <Box sx={{ display: 'flex', gap: 1, justifyContent: 'flex-end' }}>
              <Button
                variant="contained"
                startIcon={<UploadFileIcon />}
                onClick={() => setImportOpen(true)}
              >
                Importar desde Excel
              </Button>
            </Box>
          )}
        </Box>
      </Paper>

      {!selectedSucursalId ? (
        <Alert severity="info">Selecciona una sucursal para gestionar precios</Alert>
      ) : loadingProductos ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', p: 4 }}>
          <CircularProgress />
        </Box>
      ) : productosAMostrar.length === 0 ? (
        <Alert severity="info">
          No se encontraron productos{busqueda ? ' con los filtros seleccionados' : ''}.
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
                <TableCell>Código Barras</TableCell>
                <TableCell>Producto</TableCell>
                <TableCell align="right">P. Costo</TableCell>
                <TableCell align="right">P. Venta</TableCell>
                <TableCell align="right">P. Mínimo</TableCell>
                <TableCell align="right">Margen</TableCell>
                <TableCell align="center">Origen</TableCell>
                {isSupervisor() && <TableCell align="center">Acciones</TableCell>}
              </TableRow>
            </TableHead>
            <TableBody>
              {productosAMostrar.map((producto) => {
                const precioVenta = producto.precioResuelto?.precioVenta || 0;
                const margen = calcularMargen(producto.precioCosto, precioVenta);

                return (
                  <TableRow key={producto.id} hover>
                    <TableCell sx={{ fontFamily: 'monospace', fontWeight: 600 }}>
                      {producto.codigoBarras}
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
                    <TableCell align="right">
                      {formatCurrency(producto.precioCosto)}
                    </TableCell>
                    <TableCell align="right">
                      {producto.cargandoPrecio ? (
                        <CircularProgress size={20} />
                      ) : (
                        <Typography variant="body2" sx={{ fontWeight: 600 }}>
                          {formatCurrency(precioVenta)}
                        </Typography>
                      )}
                    </TableCell>
                    <TableCell align="right">
                      {producto.precioResuelto?.precioMinimo
                        ? formatCurrency(producto.precioResuelto.precioMinimo)
                        : '-'}
                    </TableCell>
                    <TableCell align="right">
                      <Chip
                        label={`${margen.toFixed(1)}%`}
                        size="small"
                        color={
                          margen > 30 ? 'success' : margen > 15 ? 'warning' : 'default'
                        }
                      />
                    </TableCell>
                    <TableCell align="center">
                      {producto.precioResuelto ? (
                        <Box sx={{ display: 'flex', gap: 0.5, justifyContent: 'center', flexWrap: 'wrap' }}>
                          <Chip
                            label={producto.precioResuelto.origenDato || producto.precioResuelto.origen}
                            size="small"
                            color={
                              producto.precioResuelto.origenDato === 'Migrado'
                                ? 'warning'
                                : producto.precioResuelto.origenDato === 'Manual'
                                ? 'primary'
                                : producto.precioResuelto.origen === 'Sucursal'
                                ? 'primary'
                                : producto.precioResuelto.origen === 'Producto'
                                ? 'secondary'
                                : 'default'
                            }
                          />
                        </Box>
                      ) : (
                        '-'
                      )}
                    </TableCell>
                    {isSupervisor() && (
                      <TableCell align="center">
                        <Tooltip title="Editar precio">
                          <IconButton
                            size="small"
                            onClick={() => handleEditarPrecio(producto)}
                            color="primary"
                          >
                            <EditIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      </TableCell>
                    )}
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Diálogo de Edición */}
      <EditarPrecioDialog
        open={formOpen}
        producto={selectedProducto}
        sucursalId={selectedSucursalId || 0}
        precioActual={precioActual}
        onClose={() => setFormOpen(false)}
        onSuccess={handleSuccess}
      />

      {/* Diálogo de Importación */}
      <ImportarPreciosDialog
        open={importOpen}
        sucursalId={selectedSucursalId || 0}
        sucursalNombre={sucursalSeleccionada?.nombre || 'Sucursal'}
        productos={productos}
        preciosActuales={preciosActualesMap}
        onClose={() => setImportOpen(false)}
        onImport={handleImportPrecios}
      />
    </Container>
  );
}
