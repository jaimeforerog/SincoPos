import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Box,
  Card,
  CardContent,
  Typography,
  Tabs,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Chip,
  CircularProgress,
  TextField,
  MenuItem,
  Stack,
  Button,
  Alert,
  Tooltip,
  Pagination,
} from '@mui/material';
import { ReportePageHeader } from '@/features/reportes/components/ReportePageHeader';
import { inventarioApi } from '@/api/inventario';
import { sucursalesApi } from '@/api/sucursales';
import { formatCurrency, formatDate, formatNumber } from '@/utils/format';
import WarningIcon from '@mui/icons-material/Warning';
import AddIcon from '@mui/icons-material/Add';
import EditIcon from '@mui/icons-material/Edit';
import SwapHorizIcon from '@mui/icons-material/SwapHoriz';
import HistoryIcon from '@mui/icons-material/History';
import { EntradaInventarioDialog } from '../components/EntradaInventarioDialog';
import { AjusteInventarioDialog } from '../components/AjusteInventarioDialog';
import { LotesTab } from '../components/LotesTab';
import { useAuth } from '@/hooks/useAuth';
import BarcodeIcon from '@mui/icons-material/QrCode2';

interface TabPanelProps {
  children?: React.ReactNode;
  index: number;
  value: number;
}

function TabPanel(props: TabPanelProps) {
  const { children, value, index, ...other } = props;

  return (
    <div
      role="tabpanel"
      hidden={value !== index}
      id={`inventario-tabpanel-${index}`}
      aria-labelledby={`inventario-tab-${index}`}
      {...other}
    >
      {value === index && <Box sx={{ py: 3 }}>{children}</Box>}
    </div>
  );
}

export function InventarioPage() {
  const { isSupervisor, activeSucursalId, user, activeEmpresaId } = useAuth();
  const esSupervisor = isSupervisor();
  const [tabValue, setTabValue] = useState(0);
  const [sucursalId, setSucursalId] = useState<number | ''>(activeSucursalId || '');
  const [soloConStock, setSoloConStock] = useState(false);

  // Dialogs
  const [entradaDialogOpen, setEntradaDialogOpen] = useState(false);
  const [ajusteDialogOpen, setAjusteDialogOpen] = useState(false);

  // Movimientos date filters
  const [movFechaDesde, setMovFechaDesde] = useState(() =>
    new Date(Date.now() - 30 * 86400000).toISOString().slice(0, 10)
  );
  const [movFechaHasta, setMovFechaHasta] = useState(() =>
    new Date().toISOString().slice(0, 10)
  );
  const [movApplied, setMovApplied] = useState(() => ({
    sucursalId: activeSucursalId || ('' as number | ''),
    fechaDesde: new Date(Date.now() - 30 * 86400000).toISOString().slice(0, 10),
    fechaHasta: new Date().toISOString().slice(0, 10),
    page: 1,
  }));

  const { data: todasSucursales = [] } = useQuery({
    queryKey: ['sucursales'],
    queryFn: () => sucursalesApi.getAll(),
    staleTime: 5 * 60 * 1000,
  });

  const sucursales = todasSucursales.filter(
    (s) =>
      (activeEmpresaId == null || s.empresaId === activeEmpresaId) &&
      (!user?.sucursalesDisponibles?.length || user.sucursalesDisponibles.some((sd) => sd.id === s.id))
  );

  // Cargar stock
  const { data: stock = [], isLoading: loadingStock, refetch: refetchStock } = useQuery({
    queryKey: ['inventario', 'stock', sucursalId, soloConStock],
    queryFn: () =>
      inventarioApi.getStock({
        sucursalId: sucursalId || undefined,
        soloConStock,
      }),
  });

  // Cargar alertas
  const { data: alertas = [], isLoading: loadingAlertas } = useQuery({
    queryKey: ['inventario', 'alertas', sucursalId],
    queryFn: () => inventarioApi.getAlertas(sucursalId || undefined),
  });

  // Cargar movimientos
  const { data: movData, isLoading: loadingMovimientos } = useQuery({
    queryKey: ['inventario', 'movimientos', movApplied],
    queryFn: () =>
      inventarioApi.getMovimientos({
        sucursalId: movApplied.sucursalId !== '' ? (movApplied.sucursalId as number) : undefined,
        fechaDesde: movApplied.fechaDesde ? `${movApplied.fechaDesde}T00:00:00Z` : undefined,
        fechaHasta: movApplied.fechaHasta ? `${movApplied.fechaHasta}T23:59:59Z` : undefined,
        page: movApplied.page,
        pageSize: 50,
      }),
  });
  const movimientos = movData?.items ?? [];

  const getTipoMovimientoColor = (tipo: string) => {
    switch (tipo) {
      case 'EntradaCompra':
        return 'success';
      case 'SalidaVenta':
        return 'error';
      case 'AjustePositivo':
        return 'info';
      case 'AjusteNegativo':
        return 'warning';
      default:
        return 'default';
    }
  };

  const getTipoMovimientoLabel = (tipo: string) => {
    const labels: Record<string, string> = {
      EntradaCompra: 'Entrada',
      SalidaVenta: 'Venta',
      AjustePositivo: 'Ajuste +',
      AjusteNegativo: 'Ajuste -',
      StockMinimoActualizado: 'Stock Mín.',
    };
    return labels[tipo] || tipo;
  };

  return (
    <Box>
      <ReportePageHeader
        title="Gestión de Inventario"
        subtitle="Consulta de stock, alertas y movimientos por sucursal"
        breadcrumbs={[
          { label: 'Reportes', path: '/reportes' },
          { label: 'Gestión de Inventario' }
        ]}
        backPath="/reportes"
      />

      {/* Filtros y Acciones */}
      <Card sx={{ mb: 3 }}>
        <CardContent>
          <Stack direction="row" spacing={2} justifyContent="space-between" alignItems="center">
            <Stack direction="row" spacing={2} flex={1}>
              <TextField
                select
                label="Sucursal"
                value={sucursalId}
                onChange={(e) => setSucursalId(e.target.value as number | '')}
                size="small"
                sx={{ minWidth: 200 }}
              >
                <MenuItem value="">Todas</MenuItem>
                {sucursales.map((s) => (
                  <MenuItem key={s.id} value={s.id}>
                    {s.nombre}
                  </MenuItem>
                ))}
              </TextField>

              {tabValue === 0 && (
                <TextField
                  select
                  label="Mostrar"
                  value={soloConStock ? 'con-stock' : 'todos'}
                  onChange={(e) => setSoloConStock(e.target.value === 'con-stock')}
                  size="small"
                  sx={{ minWidth: 150 }}
                >
                  <MenuItem value="todos">Todos</MenuItem>
                  <MenuItem value="con-stock">Solo con stock</MenuItem>
                </TextField>
              )}
            </Stack>

            {esSupervisor && (
              <Stack direction="row" spacing={1}>
                <Tooltip title="Registrar entrada de mercancía">
                  <Button
                    variant="contained"
                    color="success"
                    startIcon={<AddIcon />}
                    onClick={() => setEntradaDialogOpen(true)}
                    size="small"
                  >
                    Entrada
                  </Button>
                </Tooltip>
                <Tooltip title="Ajustar inventario (conteo físico)">
                  <Button
                    variant="outlined"
                    startIcon={<EditIcon />}
                    onClick={() => setAjusteDialogOpen(true)}
                    size="small"
                  >
                    Ajuste
                  </Button>
                </Tooltip>
              </Stack>
            )}
          </Stack>
        </CardContent>
      </Card>

      {/* Tabs */}
      <Card>
        <Box sx={{ borderBottom: 1, borderColor: 'divider' }}>
          <Tabs value={tabValue} onChange={(_, newValue) => setTabValue(newValue)}>
            <Tab label="Stock Actual" icon={<SwapHorizIcon />} iconPosition="start" />
            <Tab
              label={`Alertas (${alertas.length})`}
              icon={<WarningIcon />}
              iconPosition="start"
            />
            <Tab label="Movimientos" icon={<HistoryIcon />} iconPosition="start" />
            <Tab label="Lotes" icon={<BarcodeIcon />} iconPosition="start" />
          </Tabs>
        </Box>

        {/* Tab 1: Stock Actual */}
        <TabPanel value={tabValue} index={0}>
          {loadingStock ? (
            <Box display="flex" justifyContent="center" py={5}>
              <CircularProgress />
            </Box>
          ) : stock.length === 0 ? (
            <Alert severity="info">No hay productos en inventario</Alert>
          ) : (
            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Código</TableCell>
                    <TableCell>Producto</TableCell>
                    <TableCell>Sucursal</TableCell>
                    <TableCell align="right">Stock</TableCell>
                    <TableCell align="right">Mínimo</TableCell>
                    <TableCell align="right">Costo Promedio</TableCell>
                    <TableCell align="right">Valor Total</TableCell>
                    <TableCell>Última Actualización</TableCell>
                    <TableCell align="center">Estado</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {stock.map((item) => {
                    const valorTotal = item.cantidad * item.costoPromedio;
                    const stockBajo = item.cantidad <= item.stockMinimo;
                    return (
                      <TableRow key={item.id} sx={{ bgcolor: stockBajo ? 'error.50' : 'inherit' }}>
                        <TableCell>{item.codigoBarras}</TableCell>
                        <TableCell>{item.nombreProducto}</TableCell>
                        <TableCell>{item.nombreSucursal}</TableCell>
                        <TableCell align="right">
                          <Typography
                            fontWeight={600}
                            color={stockBajo ? 'error.main' : 'text.primary'}
                          >
                            {formatNumber(item.cantidad, 0)}
                          </Typography>
                        </TableCell>
                        <TableCell align="right">{formatNumber(item.stockMinimo, 0)}</TableCell>
                        <TableCell align="right">{formatCurrency(item.costoPromedio)}</TableCell>
                        <TableCell align="right">{formatCurrency(valorTotal)}</TableCell>
                        <TableCell>{formatDate(item.ultimaActualizacion)}</TableCell>
                        <TableCell align="center">
                          {stockBajo ? (
                            <Chip
                              label="Bajo"
                              color="error"
                              size="small"
                              icon={<WarningIcon />}
                            />
                          ) : (
                            <Chip label="OK" color="success" size="small" />
                          )}
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </TabPanel>

        {/* Tab 2: Alertas */}
        <TabPanel value={tabValue} index={1}>
          {loadingAlertas ? (
            <Box display="flex" justifyContent="center" py={5}>
              <CircularProgress />
            </Box>
          ) : alertas.length === 0 ? (
            <Alert severity="success">
              No hay alertas de stock. Todos los productos están por encima del mínimo.
            </Alert>
          ) : (
            <>
              <Alert severity="warning" sx={{ mb: 2 }}>
                <strong>{alertas.length}</strong> producto(s) con stock por debajo del mínimo
              </Alert>
              <TableContainer component={Paper} variant="outlined">
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Código</TableCell>
                      <TableCell>Producto</TableCell>
                      <TableCell>Sucursal</TableCell>
                      <TableCell align="right">Stock Actual</TableCell>
                      <TableCell align="right">Stock Mínimo</TableCell>
                      <TableCell align="right">Faltante</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {alertas.map((alerta, index) => {
                      const faltante = alerta.stockMinimo - alerta.cantidadActual;
                      return (
                        <TableRow key={index}>
                          <TableCell>{alerta.codigoBarras}</TableCell>
                          <TableCell>{alerta.nombreProducto}</TableCell>
                          <TableCell>{alerta.nombreSucursal}</TableCell>
                          <TableCell align="right">
                            <Typography fontWeight={600} color="error.main">
                              {formatNumber(alerta.cantidadActual, 0)}
                            </Typography>
                          </TableCell>
                          <TableCell align="right">
                            {formatNumber(alerta.stockMinimo, 0)}
                          </TableCell>
                          <TableCell align="right">
                            <Chip
                              label={`-${formatNumber(faltante, 0)}`}
                              color="error"
                              size="small"
                            />
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </TableContainer>
            </>
          )}
        </TabPanel>

        {/* Tab 3: Movimientos */}
        <TabPanel value={tabValue} index={2}>
          <Stack direction="row" spacing={2} sx={{ mb: 2 }} flexWrap="wrap" alignItems="center">
            <TextField
              label="Desde"
              type="date"
              size="small"
              value={movFechaDesde}
              onChange={(e) => setMovFechaDesde(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
              sx={{ minWidth: 160 }}
            />
            <TextField
              label="Hasta"
              type="date"
              size="small"
              value={movFechaHasta}
              onChange={(e) => setMovFechaHasta(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
              sx={{ minWidth: 160 }}
            />
            <Button
              variant="contained"
              size="small"
              onClick={() => setMovApplied({ sucursalId, fechaDesde: movFechaDesde, fechaHasta: movFechaHasta, page: 1 })}
            >
              Buscar
            </Button>
            {movData && (
              <Typography variant="caption" color="text.secondary" sx={{ ml: 1 }}>
                {movData.totalCount} movimiento{movData.totalCount !== 1 ? 's' : ''}
              </Typography>
            )}
          </Stack>
          {loadingMovimientos ? (
            <Box display="flex" justifyContent="center" py={5}>
              <CircularProgress />
            </Box>
          ) : movimientos.length === 0 ? (
            <Alert severity="info">No hay movimientos en el período seleccionado</Alert>
          ) : (
            <>
              <TableContainer component={Paper} variant="outlined" sx={{ maxHeight: 560 }}>
                <Table size="small" stickyHeader>
                  <TableHead>
                    <TableRow>
                      <TableCell>Fecha</TableCell>
                      <TableCell>Tipo</TableCell>
                      <TableCell>Producto</TableCell>
                      <TableCell>Sucursal</TableCell>
                      <TableCell align="right">Cantidad</TableCell>
                      <TableCell align="right">Costo Unit.</TableCell>
                      <TableCell align="right">Costo Total</TableCell>
                      <TableCell>Referencia</TableCell>
                      <TableCell>Tercero</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {movimientos.map((mov) => (
                      <TableRow key={`${mov.productoId}-${mov.sucursalId}-${mov.id}-${mov.fechaMovimiento}`}>
                        <TableCell>{formatDate(mov.fechaMovimiento)}</TableCell>
                        <TableCell>
                          <Chip
                            label={getTipoMovimientoLabel(mov.tipoMovimiento)}
                            color={getTipoMovimientoColor(mov.tipoMovimiento)}
                            size="small"
                          />
                        </TableCell>
                        <TableCell>{mov.nombreProducto}</TableCell>
                        <TableCell>{mov.nombreSucursal}</TableCell>
                        <TableCell align="right">{formatNumber(mov.cantidad, 0)}</TableCell>
                        <TableCell align="right">{formatCurrency(mov.costoUnitario)}</TableCell>
                        <TableCell align="right">{formatCurrency(mov.costoTotal)}</TableCell>
                        <TableCell>{mov.referencia || '-'}</TableCell>
                        <TableCell>{mov.nombreTercero || '-'}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
              {movData && movData.totalPages > 1 && (
                <Box sx={{ display: 'flex', justifyContent: 'center', pt: 2 }}>
                  <Pagination
                    count={movData.totalPages}
                    page={movApplied.page}
                    onChange={(_, p) => setMovApplied(prev => ({ ...prev, page: p }))}
                    size="small"
                    color="primary"
                  />
                </Box>
              )}
            </>
          )}
        </TabPanel>

        {/* Tab 4: Lotes */}
        <TabPanel value={tabValue} index={3}>
          <LotesTab sucursales={sucursales} activeSucursalId={activeSucursalId || undefined} />
        </TabPanel>
      </Card>

      {/* Dialogs */}
      <EntradaInventarioDialog
        open={entradaDialogOpen}
        onClose={() => setEntradaDialogOpen(false)}
        onSuccess={() => refetchStock()}
      />
      <AjusteInventarioDialog
        open={ajusteDialogOpen}
        onClose={() => setAjusteDialogOpen(false)}
        onSuccess={() => refetchStock()}
      />
    </Box>
  );
}
