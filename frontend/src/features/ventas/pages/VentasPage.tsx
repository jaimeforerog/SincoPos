import { useState, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
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
  Chip,
  IconButton,
  TextField,
  MenuItem,
  Alert,
  CircularProgress,
  Tooltip,
  Autocomplete,
  Stack,
} from '@mui/material';
import VisibilityIcon from '@mui/icons-material/Visibility';
import SearchIcon from '@mui/icons-material/Search';
import { useAuth } from '@/hooks/useAuth';
import { ventasApi } from '@/api/ventas';
import { sucursalesApi } from '@/api/sucursales';
import { VentaDetalleDialog } from '../components/VentaDetalleDialog';
import type { VentaDTO } from '@/types/api';

// Función helper para formatear fecha a YYYY-MM-DD
const formatDateForInput = (date: Date): string => {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
};

// Función helper para obtener fecha hace N días
const getDaysAgo = (days: number): string => {
  const date = new Date();
  date.setDate(date.getDate() - days);
  return formatDateForInput(date);
};

export function VentasPage() {
  const { activeSucursalId } = useAuth();
  const [selectedVenta, setSelectedVenta] = useState<VentaDTO | null>(null);
  const [detalleOpen, setDetalleOpen] = useState(false);
  const [filtroSucursal, setFiltroSucursal] = useState<number | ''>(
    activeSucursalId || ''
  );
  const [filtroEstado, setFiltroEstado] = useState<string>('');
  const [fechaDesde, setFechaDesde] = useState<string>(getDaysAgo(5)); // 5 días atrás
  const [fechaHasta, setFechaHasta] = useState<string>(formatDateForInput(new Date())); // Hoy
  const [busquedaVenta, setBusquedaVenta] = useState<VentaDTO | null>(null);

  // Cargar sucursales para el filtro
  const { data: sucursales = [] } = useQuery({
    queryKey: ['sucursales'],
    queryFn: () => sucursalesApi.getAll(true),
  });

  // Cargar ventas
  const { data: ventas = [], isLoading } = useQuery({
    queryKey: ['ventas', filtroSucursal, filtroEstado, fechaDesde, fechaHasta],
    queryFn: () =>
      ventasApi.getAll({
        sucursalId: filtroSucursal || undefined,
        estado: filtroEstado || undefined,
        desde: fechaDesde ? `${fechaDesde}T00:00:00Z` : undefined,
        hasta: fechaHasta ? `${fechaHasta}T23:59:59Z` : undefined,
        limite: 100,
      }),
    refetchInterval: 30000, // Refrescar cada 30 segundos
  });

  // Filtrar ventas para el autocomplete (si hay búsqueda)
  const ventasFiltradas = useMemo(() => {
    if (busquedaVenta) {
      return ventas.filter((v) => v.id === busquedaVenta.id);
    }
    return ventas;
  }, [ventas, busquedaVenta]);

  const handleVerDetalle = (venta: VentaDTO) => {
    setSelectedVenta(venta);
    setDetalleOpen(true);
  };

  const formatCurrency = (value: number) => {
    return new Intl.NumberFormat('es-CO', {
      style: 'currency',
      currency: 'COP',
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(value);
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleString('es-CO', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  const getEstadoColor = (estado: string) => {
    switch (estado) {
      case 'Completada':
        return 'success';
      case 'Cancelada':
      case 'Anulada':
        return 'error';
      case 'DevueltaParcial':
        return 'warning';
      case 'DevueltaTotal':
        return 'error';
      default:
        return 'default';
    }
  };

  return (
    <Container maxWidth="xl">
      <Typography variant="h4" sx={{ mb: 3, fontWeight: 700 }}>
        Historial de Ventas
      </Typography>

      {/* Filtros */}
      <Paper sx={{ p: 2, mb: 3 }}>
        <Stack spacing={2}>
          {/* Fila 1: Filtros de Fecha y otros */}
          <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'center' }}>
            <TextField
              label="Fecha Desde"
              type="date"
              value={fechaDesde}
              onChange={(e) => {
                setFechaDesde(e.target.value);
                setBusquedaVenta(null); // Limpiar búsqueda al cambiar fechas
              }}
              size="small"
              sx={{ minWidth: 170 }}
              InputLabelProps={{ shrink: true }}
            />

            <TextField
              label="Fecha Hasta"
              type="date"
              value={fechaHasta}
              onChange={(e) => {
                setFechaHasta(e.target.value);
                setBusquedaVenta(null); // Limpiar búsqueda al cambiar fechas
              }}
              size="small"
              sx={{ minWidth: 170 }}
              InputLabelProps={{ shrink: true }}
            />

            <TextField
              select
              label="Sucursal"
              value={filtroSucursal}
              onChange={(e) => setFiltroSucursal(e.target.value as number | '')}
              sx={{ minWidth: 200 }}
              size="small"
            >
              <MenuItem value="">Todas</MenuItem>
              {sucursales.map((sucursal) => (
                <MenuItem key={sucursal.id} value={sucursal.id}>
                  {sucursal.nombre}
                </MenuItem>
              ))}
            </TextField>

            <TextField
              select
              label="Estado"
              value={filtroEstado}
              onChange={(e) => setFiltroEstado(e.target.value)}
              sx={{ minWidth: 150 }}
              size="small"
            >
              <MenuItem value="">Todos</MenuItem>
              <MenuItem value="Completada">Completada</MenuItem>
              <MenuItem value="Cancelada">Cancelada</MenuItem>
              <MenuItem value="Anulada">Anulada</MenuItem>
            </TextField>

            <Box sx={{ flexGrow: 1 }} />

            <Typography variant="body2" color="text.secondary">
              {isLoading ? 'Cargando...' : `${ventas.length} venta(s) encontradas`}
            </Typography>
          </Box>

          {/* Fila 2: Buscador de ventas */}
          <Box sx={{ display: 'flex', gap: 2, alignItems: 'center' }}>
            <SearchIcon color="action" />
            <Autocomplete
              fullWidth
              options={ventas}
              getOptionLabel={(option) => option.numeroVenta}
              value={busquedaVenta}
              onChange={(_, newValue) => {
                setBusquedaVenta(newValue);
              }}
              loading={isLoading}
              renderInput={(params) => (
                <TextField
                  {...params}
                  label="Buscar venta por número"
                  placeholder="V-000001"
                  size="small"
                  helperText="Las opciones se actualizan según las fechas y filtros seleccionados"
                  InputProps={{
                    ...params.InputProps,
                    endAdornment: (
                      <>
                        {isLoading ? <CircularProgress color="inherit" size={20} /> : null}
                        {params.InputProps.endAdornment}
                      </>
                    ),
                  }}
                />
              )}
              renderOption={(props, option) => {
                const { key, ...restProps } = props as any;
                return (
                  <Box component="li" key={key} {...restProps}>
                    <Box sx={{ flexGrow: 1 }}>
                      <Typography variant="body2" sx={{ fontWeight: 600, fontFamily: 'monospace' }}>
                        {option.numeroVenta}
                      </Typography>
                      <Typography variant="caption" color="text.secondary">
                        {formatDate(option.fechaVenta)} - {formatCurrency(option.total)}
                      </Typography>
                    </Box>
                    <Chip
                      label={option.estado}
                      color={getEstadoColor(option.estado) as any}
                      size="small"
                    />
                  </Box>
                );
              }}
              noOptionsText={isLoading ? "Cargando ventas..." : "No se encontraron ventas en el rango de fechas seleccionado"}
            />
          </Box>

          {/* Fila 3: Indicador de resultados */}
          <Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>
            <Typography variant="body2" color="text.secondary">
              {busquedaVenta
                ? `Mostrando: ${busquedaVenta.numeroVenta}`
                : `Total en tabla: ${ventasFiltradas.length} venta(s)`
              }
            </Typography>
          </Box>
        </Stack>
      </Paper>

      {/* Tabla de Ventas */}
      {isLoading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', p: 4 }}>
          <CircularProgress />
        </Box>
      ) : ventasFiltradas.length === 0 ? (
        <Alert severity="info">
          No se encontraron ventas con los filtros seleccionados.
        </Alert>
      ) : (
        <TableContainer component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell sx={{ fontWeight: 700 }}>Número</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Fecha</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Sucursal</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Caja</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Cliente</TableCell>
                <TableCell sx={{ fontWeight: 700 }} align="right">Total</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Método Pago</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Estado</TableCell>
                <TableCell sx={{ fontWeight: 700 }} align="center">Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {ventasFiltradas.map((venta) => (
                <TableRow
                  key={venta.id}
                  hover
                  sx={{ '&:last-child td, &:last-child th': { border: 0 } }}
                >
                  <TableCell>
                    <Typography variant="body2" sx={{ fontFamily: 'monospace', fontWeight: 600 }}>
                      {venta.numeroVenta}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2">
                      {formatDate(venta.fechaVenta)}
                    </Typography>
                  </TableCell>
                  <TableCell>{venta.nombreSucursal}</TableCell>
                  <TableCell>{venta.nombreCaja}</TableCell>
                  <TableCell>
                    {venta.nombreCliente || (
                      <Typography variant="body2" color="text.secondary">
                        Sin cliente
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell align="right">
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      {formatCurrency(venta.total)}
                    </Typography>
                  </TableCell>
                  <TableCell>{venta.metodoPago}</TableCell>
                  <TableCell>
                    <Chip
                      label={venta.estado}
                      color={getEstadoColor(venta.estado) as any}
                      size="small"
                    />
                  </TableCell>
                  <TableCell align="center">
                    <Tooltip title="Ver detalle">
                      <IconButton
                        size="small"
                        onClick={() => handleVerDetalle(venta)}
                        color="primary"
                      >
                        <VisibilityIcon />
                      </IconButton>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Diálogo de Detalle */}
      <VentaDetalleDialog
        open={detalleOpen}
        venta={selectedVenta}
        onClose={() => setDetalleOpen(false)}
      />
    </Container>
  );
}
