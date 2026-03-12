import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Box,
  Card,
  CardContent,
  Typography,
  CircularProgress,
  Alert,
  Stack,
  Button,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Chip,
  FormControlLabel,
  Switch,
  TextField,
} from '@mui/material';
import { ReportePageHeader } from '../components/ReportePageHeader';
import { reportesApi } from '@/api/reportes';
import { sucursalesApi } from '@/api/sucursales';
import { categoriasApi } from '@/api/categorias';
import { formatCurrency, formatNumber } from '@/utils/format';
import SearchIcon from '@mui/icons-material/Search';
import DownloadIcon from '@mui/icons-material/Download';
import { exportarReporteInventario } from '@/utils/exportReportes';

export function ReporteInventarioPage() {
  const [sucursalId, setSucursalId] = useState<number | ''>('');
  const [categoriaId, setCategoriaId] = useState<number | ''>('');
  const [soloConStock, setSoloConStock] = useState(true);
  const [busqueda, setBusqueda] = useState('');

  const { data: sucursales = [] } = useQuery({
    queryKey: ['sucursales'],
    queryFn: () => sucursalesApi.getAll(true),
  });

  const { data: categorias = [] } = useQuery({
    queryKey: ['categorias'],
    queryFn: () => categoriasApi.getAll(),
  });

  const {
    data: reporte,
    isLoading,
    refetch,
  } = useQuery({
    queryKey: ['reportes', 'inventario-valorizado', sucursalId, categoriaId, soloConStock],
    queryFn: () =>
      reportesApi.inventarioValorizado({
        sucursalId: sucursalId || undefined,
        categoriaId: categoriaId || undefined,
        soloConStock,
      }),
  });

  const productosFiltrados =
    reporte?.productos.filter(
      (p) =>
        p.nombre.toLowerCase().includes(busqueda.toLowerCase()) ||
        p.codigoBarras.toLowerCase().includes(busqueda.toLowerCase())
    ) || [];

  if (isLoading) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight="60vh">
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box>
      <ReportePageHeader
        title="Inventario Valorizado"
        subtitle="Resumen del valor total del stock actual en almacén por categoría"
        breadcrumbs={[
          { label: 'Reportes', path: '/reportes' },
          { label: 'Inventario Valorizado' },
        ]}
        color="#1976d2"
      />

      {/* Filtros */}
      <Card sx={{ mb: 3 }}>
        <CardContent>
          <Typography variant="h6" gutterBottom fontWeight={600}>
            Filtros de Búsqueda
          </Typography>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ mb: 2 }}>
            <FormControl sx={{ flex: 1 }}>
              <InputLabel>Sucursal</InputLabel>
              <Select
                value={sucursalId}
                onChange={(e) => setSucursalId(e.target.value as number | '')}
                label="Sucursal"
              >
                <MenuItem value="">Todas</MenuItem>
                {sucursales.map((s) => (
                  <MenuItem key={s.id} value={s.id}>
                    {s.nombre}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <FormControl sx={{ flex: 1 }}>
              <InputLabel>Categoría</InputLabel>
              <Select
                value={categoriaId}
                onChange={(e) => setCategoriaId(e.target.value as number | '')}
                label="Categoría"
              >
                <MenuItem value="">Todas</MenuItem>
                {categorias.map((c) => (
                  <MenuItem key={c.id} value={c.id}>
                    {c.rutaCompleta}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <Box sx={{ flex: 1, display: 'flex', alignItems: 'center' }}>
              <FormControlLabel
                control={
                  <Switch
                    checked={soloConStock}
                    onChange={(e) => setSoloConStock(e.target.checked)}
                  />
                }
                label="Solo con stock"
              />
            </Box>
          </Stack>
          <Stack direction="row" spacing={2}>
            <Button
              variant="contained"
              startIcon={<SearchIcon />}
              onClick={() => refetch()}
              sx={{ flex: 1 }}
            >
              Generar Reporte
            </Button>
            <Button
              variant="outlined"
              startIcon={<DownloadIcon />}
              onClick={() => reporte && exportarReporteInventario(reporte, productosFiltrados)}
              disabled={!reporte}
            >
              Exportar Excel
            </Button>
          </Stack>
        </CardContent>
      </Card>

      {!reporte ? (
        <Alert severity="info">Configure los filtros y haga clic en "Generar Reporte"</Alert>
      ) : (
        <Stack spacing={3}>
          {/* Resumen de Métricas */}
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
            <Card sx={{ flex: 1 }}>
              <CardContent>
                <Typography color="text.secondary" variant="body2">
                  Total Productos
                </Typography>
                <Typography variant="h4" fontWeight={600}>
                  {reporte.totalProductos}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {formatNumber(reporte.totalUnidades, 0)} unidades
                </Typography>
              </CardContent>
            </Card>
            <Card sx={{ flex: 1 }}>
              <CardContent>
                <Typography color="text.secondary" variant="body2">
                  Costo Total
                </Typography>
                <Typography variant="h4" fontWeight={600} color="error.main">
                  {formatCurrency(reporte.totalCosto)}
                </Typography>
              </CardContent>
            </Card>
            <Card sx={{ flex: 1 }}>
              <CardContent>
                <Typography color="text.secondary" variant="body2">
                  Valor Venta
                </Typography>
                <Typography variant="h4" fontWeight={600} color="success.main">
                  {formatCurrency(reporte.totalVenta)}
                </Typography>
              </CardContent>
            </Card>
            <Card sx={{ flex: 1 }}>
              <CardContent>
                <Typography color="text.secondary" variant="body2">
                  Utilidad Potencial
                </Typography>
                <Typography variant="h4" fontWeight={600} color="primary.main">
                  {formatCurrency(reporte.utilidadPotencial)}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {reporte.totalVenta > 0
                    ? `${((reporte.utilidadPotencial / reporte.totalVenta) * 100).toFixed(1)}%`
                    : '0%'}
                </Typography>
              </CardContent>
            </Card>
          </Stack>

          {/* Búsqueda */}
          <Card>
            <CardContent>
              <TextField
                label="Buscar producto"
                placeholder="Nombre o código de barras..."
                value={busqueda}
                onChange={(e) => setBusqueda(e.target.value)}
                fullWidth
                size="small"
              />
            </CardContent>
          </Card>

          {/* Tabla de Productos */}
          <Card>
            <CardContent>
              <Typography variant="h6" fontWeight={600} gutterBottom>
                Productos en Inventario ({productosFiltrados.length})
              </Typography>
              <TableContainer component={Paper} variant="outlined" sx={{ maxHeight: 600 }}>
                <Table size="small" stickyHeader>
                  <TableHead>
                    <TableRow>
                      <TableCell>Código</TableCell>
                      <TableCell>Producto</TableCell>
                      <TableCell>Categoría</TableCell>
                      <TableCell>Sucursal</TableCell>
                      <TableCell align="right">Stock</TableCell>
                      <TableCell align="right">Costo Unit.</TableCell>
                      <TableCell align="right">Costo Total</TableCell>
                      <TableCell align="right">Precio Venta</TableCell>
                      <TableCell align="right">Valor Venta</TableCell>
                      <TableCell align="right">Utilidad</TableCell>
                      <TableCell align="right">Margen %</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {productosFiltrados.map((producto) => (
                      <TableRow key={`${producto.productoId}-${producto.sucursalId}`}>
                        <TableCell>{producto.codigoBarras}</TableCell>
                        <TableCell>{producto.nombre}</TableCell>
                        <TableCell>{producto.categoria || '-'}</TableCell>
                        <TableCell>{producto.nombreSucursal}</TableCell>
                        <TableCell align="right">{formatNumber(producto.cantidad, 0)}</TableCell>
                        <TableCell align="right">
                          {formatCurrency(producto.costoPromedio)}
                        </TableCell>
                        <TableCell align="right">{formatCurrency(producto.costoTotal)}</TableCell>
                        <TableCell align="right">
                          {formatCurrency(producto.precioVenta)}
                        </TableCell>
                        <TableCell align="right">{formatCurrency(producto.valorVenta)}</TableCell>
                        <TableCell align="right">
                          {formatCurrency(producto.utilidadPotencial)}
                        </TableCell>
                        <TableCell align="right">
                          <Chip
                            label={`${producto.margenPorcentaje.toFixed(1)}%`}
                            color={
                              producto.margenPorcentaje > 30
                                ? 'success'
                                : producto.margenPorcentaje > 15
                                  ? 'warning'
                                  : 'error'
                            }
                            size="small"
                          />
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            </CardContent>
          </Card>
        </Stack>
      )}
    </Box>
  );
}
