import { useState, useMemo, type HTMLAttributes } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Box,
  CircularProgress,
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
  Tooltip,
  Autocomplete,
  Pagination,
  Skeleton,
  alpha,
  type ChipProps,
} from '@mui/material';
import VisibilityIcon from '@mui/icons-material/Visibility';
import SearchIcon from '@mui/icons-material/Search';
import ReceiptIcon from '@mui/icons-material/Receipt';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import AttachMoneyIcon from '@mui/icons-material/AttachMoney';
import UndoIcon from '@mui/icons-material/Undo';
import FilterListIcon from '@mui/icons-material/FilterList';
import { useAuth } from '@/hooks/useAuth';
import { TableSkeleton } from '@/components/common/TableSkeleton';
import { ventasApi } from '@/api/ventas';
import { sucursalesApi } from '@/api/sucursales';
import { VentaDetalleDialog } from '../components/VentaDetalleDialog';
import type { VentaDTO } from '@/types/api';

const HERO_COLOR = '#1565c0';

const formatDateForInput = (date: Date): string => {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
};

const getDaysAgo = (days: number): string => {
  const date = new Date();
  date.setDate(date.getDate() - days);
  return formatDateForInput(date);
};

const formatCurrency = (value: number) =>
  new Intl.NumberFormat('es-CO', {
    style: 'currency', currency: 'COP',
    minimumFractionDigits: 0, maximumFractionDigits: 0,
  }).format(value);

const formatDate = (dateString: string) =>
  new Date(dateString).toLocaleString('es-CO', {
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit',
  });

const ESTADO_META: Record<string, { color: ChipProps['color']; label: string }> = {
  Completada:      { color: 'success', label: 'Completada' },
  Cancelada:       { color: 'error',   label: 'Cancelada' },
  Anulada:         { color: 'error',   label: 'Anulada' },
  DevueltaParcial: { color: 'warning', label: 'Dev. Parcial' },
  DevueltaTotal:   { color: 'error',   label: 'Dev. Total' },
};

const ESTADOS_FILTRO = [
  { value: '', label: 'Todos los estados' },
  { value: 'Completada',      label: 'Completada' },
  { value: 'Cancelada',       label: 'Cancelada' },
  { value: 'Anulada',         label: 'Anulada' },
];

interface HeroStatProps {
  icon: React.ReactElement;
  label: string;
  value: string | number;
  loading: boolean;
}

function HeroStat({ icon, label, value, loading }: HeroStatProps) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
      <Box sx={{ color: 'rgba(255,255,255,0.8)', display: 'flex' }}>{icon}</Box>
      <Box>
        <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.7)', display: 'block', lineHeight: 1 }}>
          {label}
        </Typography>
        {loading ? (
          <Skeleton variant="text" width={70} sx={{ bgcolor: 'rgba(255,255,255,0.2)' }} />
        ) : (
          <Typography variant="subtitle1" fontWeight={700} sx={{ color: '#fff', lineHeight: 1.2 }}>
            {value}
          </Typography>
        )}
      </Box>
    </Box>
  );
}

export function VentasPage() {
  const { activeSucursalId } = useAuth();
  const [selectedVenta, setSelectedVenta] = useState<VentaDTO | null>(null);
  const [detalleOpen, setDetalleOpen] = useState(false);
  const [filtroSucursal, setFiltroSucursal] = useState<number | ''>(activeSucursalId || '');
  const [filtroEstado, setFiltroEstado] = useState<string>('');
  const [fechaDesde, setFechaDesde] = useState<string>(getDaysAgo(5));
  const [fechaHasta, setFechaHasta] = useState<string>(formatDateForInput(new Date()));
  const [busquedaVenta, setBusquedaVenta] = useState<VentaDTO | null>(null);
  const [page, setPage] = useState(1);
  const pageSize = 50;

  const { data: sucursales = [] } = useQuery({
    queryKey: ['sucursales'],
    queryFn: () => sucursalesApi.getAll(true),
  });

  const { data: ventasPage, isLoading } = useQuery({
    queryKey: ['ventas', filtroSucursal, filtroEstado, fechaDesde, fechaHasta, page],
    queryFn: () =>
      ventasApi.getAll({
        sucursalId: filtroSucursal || undefined,
        estado: filtroEstado || undefined,
        desde: fechaDesde ? `${fechaDesde}T00:00:00Z` : undefined,
        hasta: fechaHasta ? `${fechaHasta}T23:59:59Z` : undefined,
        page,
        pageSize,
      }),
    refetchInterval: 30000,
  });

  const ventas = ventasPage?.items ?? [];
  const totalCount = ventasPage?.totalCount ?? 0;
  const totalPages = ventasPage?.totalPages ?? 1;

  const stats = useMemo(() => ({
    total:       totalCount,
    completadas: ventas.filter((v) => v.estado === 'Completada').length,
    devueltas:   ventas.filter((v) => v.estado === 'DevueltaParcial' || v.estado === 'DevueltaTotal').length,
    totalCOP:    ventas.reduce((sum, v) => sum + v.total, 0),
  }), [ventas, totalCount]);

  const ventasFiltradas = useMemo(() => {
    if (busquedaVenta) return ventas.filter((v) => v.id === busquedaVenta.id);
    return ventas;
  }, [ventas, busquedaVenta]);

  const handleVerDetalle = (venta: VentaDTO) => {
    setSelectedVenta(venta);
    setDetalleOpen(true);
  };

  return (
    <Container maxWidth="xl">
      {/* Hero */}
      <Box
        sx={{
          background: `linear-gradient(135deg, ${HERO_COLOR} 0%, #0d47a1 50%, #01579b 100%)`,
          borderRadius: 3,
          px: { xs: 3, md: 4 },
          py: { xs: 2.5, md: 3 },
          mb: 3,
          mt: 1,
          position: 'relative',
          overflow: 'hidden',
          '&::before': {
            content: '""', position: 'absolute', top: -60, right: -60,
            width: 200, height: 200, borderRadius: '50%', background: 'rgba(255,255,255,0.05)',
          },
          '&::after': {
            content: '""', position: 'absolute', bottom: -40, right: 80,
            width: 120, height: 120, borderRadius: '50%', background: 'rgba(255,255,255,0.05)',
          },
        }}
      >
        <Box
          sx={{
            display: 'flex',
            flexDirection: { xs: 'column', md: 'row' },
            alignItems: { xs: 'flex-start', md: 'center' },
            justifyContent: 'space-between',
            gap: { xs: 2.5, md: 0 },
            position: 'relative',
            zIndex: 1,
          }}
        >
          <Box>
            <Typography variant="h5" fontWeight={700} sx={{ color: '#fff', lineHeight: 1.2 }}>
              Historial de Ventas
            </Typography>
            <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.75)', mt: 0.5 }}>
              Consulta y gestión de transacciones de venta
            </Typography>
          </Box>

          <Box
            sx={{
              display: 'flex', flexWrap: 'wrap',
              gap: { xs: 2.5, md: 4 }, alignItems: 'center',
              '& > *:not(:last-child)': {
                position: 'relative',
                '&::after': {
                  content: '""', position: 'absolute',
                  right: { xs: 'unset', md: -16 }, top: '10%',
                  height: '80%', width: '1px',
                  bgcolor: 'rgba(255,255,255,0.2)',
                  display: { xs: 'none', md: 'block' },
                },
              },
            }}
          >
            <HeroStat icon={<ReceiptIcon />}      label="Total ventas"  value={stats.total}                    loading={isLoading} />
            <HeroStat icon={<CheckCircleIcon />}  label="Completadas"   value={stats.completadas}              loading={isLoading} />
            <HeroStat icon={<UndoIcon />}         label="Devueltas"     value={stats.devueltas}                loading={isLoading} />
            <HeroStat icon={<AttachMoneyIcon />}  label="Monto página"  value={formatCurrency(stats.totalCOP)} loading={isLoading} />
          </Box>
        </Box>
      </Box>

      {/* Filtros */}
      <Box
        sx={{
          bgcolor: 'background.paper', borderRadius: 2,
          border: '1px solid', borderColor: 'divider',
          p: 2, mb: 2.5,
        }}
      >
        <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'center', mb: 2 }}>
          <FilterListIcon sx={{ color: 'text.secondary' }} fontSize="small" />
          <TextField
            label="Desde"
            type="date"
            value={fechaDesde}
            onChange={(e) => { setFechaDesde(e.target.value); setBusquedaVenta(null); setPage(1); }}
            size="small"
            sx={{ minWidth: 155 }}
            InputLabelProps={{ shrink: true }}
          />
          <TextField
            label="Hasta"
            type="date"
            value={fechaHasta}
            onChange={(e) => { setFechaHasta(e.target.value); setBusquedaVenta(null); setPage(1); }}
            size="small"
            sx={{ minWidth: 155 }}
            InputLabelProps={{ shrink: true }}
          />
          <TextField
            select label="Sucursal" value={filtroSucursal}
            onChange={(e) => { setFiltroSucursal(e.target.value as number | ''); setPage(1); }}
            sx={{ minWidth: 180 }} size="small"
          >
            <MenuItem value="">Todas</MenuItem>
            {sucursales.map((s) => (
              <MenuItem key={s.id} value={s.id}>{s.nombre}</MenuItem>
            ))}
          </TextField>
          <TextField
            select label="Estado" value={filtroEstado}
            onChange={(e) => { setFiltroEstado(e.target.value); setPage(1); }}
            sx={{ minWidth: 160 }} size="small"
          >
            {ESTADOS_FILTRO.map((e) => (
              <MenuItem key={e.value} value={e.value}>{e.label}</MenuItem>
            ))}
          </TextField>
          <Box sx={{ ml: 'auto' }}>
            <Typography variant="caption" color="text.secondary">
              {isLoading ? 'Cargando...' : `${totalCount} venta(s) encontradas`}
            </Typography>
          </Box>
        </Box>

        <Box sx={{ display: 'flex', gap: 2, alignItems: 'center' }}>
          <SearchIcon sx={{ color: 'text.secondary' }} fontSize="small" />
          <Autocomplete
            fullWidth
            options={ventas}
            getOptionLabel={(o) => o.numeroVenta}
            value={busquedaVenta}
            onChange={(_, v) => setBusquedaVenta(v)}
            loading={isLoading}
            size="small"
            renderInput={(params) => (
              <TextField
                {...params}
                label="Buscar venta por número"
                placeholder="V-000001"
                InputProps={{
                  ...params.InputProps,
                  endAdornment: (
                    <>
                      {isLoading ? <CircularProgress color="inherit" size={16} /> : null}
                      {params.InputProps.endAdornment}
                    </>
                  ),
                }}
              />
            )}
            renderOption={(props, option) => {
              const { key, ...rest } = props as HTMLAttributes<HTMLLIElement> & { key: string };
              return (
                <Box component="li" key={key} {...rest}>
                  <Box sx={{ flexGrow: 1 }}>
                    <Typography variant="body2" sx={{ fontWeight: 600, fontFamily: 'monospace' }}>
                      {option.numeroVenta}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {formatDate(option.fechaVenta)} — {formatCurrency(option.total)}
                    </Typography>
                  </Box>
                  <Chip
                    label={ESTADO_META[option.estado]?.label ?? option.estado}
                    color={ESTADO_META[option.estado]?.color ?? 'default'}
                    size="small"
                  />
                </Box>
              );
            }}
            noOptionsText={isLoading ? 'Cargando...' : 'No se encontraron ventas'}
          />
        </Box>
      </Box>

      {/* Tabla */}
      {isLoading ? (
        <TableSkeleton cols={11} />
      ) : ventasFiltradas.length === 0 ? (
        <Box sx={{ textAlign: 'center', py: 8 }}>
          <ReceiptIcon sx={{ fontSize: 48, color: 'text.disabled', mb: 1 }} />
          <Typography color="text.secondary">
            No se encontraron ventas con los filtros seleccionados.
          </Typography>
        </Box>
      ) : (
        <TableContainer
          component={Paper}
          sx={{ borderRadius: 2, border: '1px solid', borderColor: 'divider', overflow: 'hidden' }}
        >
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
                <TableCell>Número</TableCell>
                <TableCell>Fecha</TableCell>
                <TableCell>Sucursal</TableCell>
                <TableCell>Caja</TableCell>
                <TableCell>Cliente</TableCell>
                <TableCell align="right">Total</TableCell>
                <TableCell>Método Pago</TableCell>
                <TableCell>Estado</TableCell>
                <TableCell>DIAN</TableCell>
                <TableCell align="center">ERP</TableCell>
                <TableCell align="center">Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {ventasFiltradas.map((venta) => {
                const estadoMeta = ESTADO_META[venta.estado] ?? { color: 'default' as const, label: venta.estado };
                return (
                  <TableRow
                    key={venta.id}
                    hover
                    sx={{
                      '&:hover': { bgcolor: alpha(HERO_COLOR, 0.03) },
                      '&:last-child td': { borderBottom: 0 },
                    }}
                  >
                    <TableCell>
                      <Typography variant="body2" fontWeight={700} color="primary.main" sx={{ fontFamily: 'monospace' }}>
                        {venta.numeroVenta}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2">{formatDate(venta.fechaVenta)}</Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" fontWeight={500}>{venta.nombreSucursal}</Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" color="text.secondary">{venta.nombreCaja}</Typography>
                    </TableCell>
                    <TableCell>
                      {venta.nombreCliente || (
                        <Typography variant="body2" color="text.disabled">Sin cliente</Typography>
                      )}
                    </TableCell>
                    <TableCell align="right">
                      <Typography variant="body2" fontWeight={600}>
                        {formatCurrency(venta.total)}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2">{venta.metodoPago}</Typography>
                    </TableCell>
                    <TableCell>
                      <Chip label={estadoMeta.label} color={estadoMeta.color} size="small" sx={{ fontWeight: 600 }} />
                    </TableCell>
                    <TableCell>
                      {venta.requiereFacturaElectronica ? (
                        <Chip label="FE" size="small" color="info" variant="outlined" />
                      ) : (
                        <Typography variant="caption" color="text.disabled">—</Typography>
                      )}
                    </TableCell>
                    <TableCell align="center">
                      <Tooltip
                        title={
                          venta.sincronizadoErp
                            ? `Ref: ${venta.erpReferencia ?? ''}`
                            : venta.errorSincronizacion ?? 'Pendiente de sincronización'
                        }
                      >
                        <Chip
                          label={venta.sincronizadoErp ? 'Sync' : venta.errorSincronizacion ? 'Error' : 'Pend.'}
                          size="small"
                          color={venta.sincronizadoErp ? 'success' : venta.errorSincronizacion ? 'error' : 'warning'}
                          variant={venta.sincronizadoErp ? 'filled' : 'outlined'}
                          sx={{ fontWeight: 600 }}
                        />
                      </Tooltip>
                    </TableCell>
                    <TableCell align="center">
                      <Tooltip title="Ver detalle">
                        <IconButton size="small" color="primary" onClick={() => handleVerDetalle(venta)}>
                          <VisibilityIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Paginación */}
      {totalPages > 1 && (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 2.5 }}>
          <Pagination
            count={totalPages}
            page={page}
            onChange={(_, p) => setPage(p)}
            color="primary"
            showFirstButton
            showLastButton
          />
        </Box>
      )}

      <VentaDetalleDialog
        open={detalleOpen}
        venta={selectedVenta}
        onClose={() => setDetalleOpen(false)}
      />
    </Container>
  );
}
