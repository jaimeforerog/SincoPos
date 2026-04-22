import { useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  FormControl,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import {
  Download as DownloadIcon,
  Refresh as RefreshIcon,
  WarningAmber as WarningIcon,
  CheckCircle as CheckCircleIcon,
  Error as ErrorIcon,
  Schedule as ScheduleIcon,
  HelpOutline as SinFechaIcon,
  Timeline as TimelineIcon,
} from '@mui/icons-material';
import { IconButton } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { lotesApi } from '@/api/lotes';
import { sucursalesApi } from '@/api/sucursales';
import { ReportePageHeader } from '../components/ReportePageHeader';
import { useAuth } from '@/hooks/useAuth';
import { exportarReporteLotes } from '@/utils/exportReportes';
import type { LoteReporteItemDTO } from '@/types/api';
import { TrazabilidadLoteModal } from '@/features/inventario/components/TrazabilidadLoteModal';

// ── helpers ──────────────────────────────────────────────────────────────────

const COP = (v: number) =>
  new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(v);

const NUM = (v: number) =>
  new Intl.NumberFormat('es-CO', { maximumFractionDigits: 2 }).format(v);

type Estado = 'Vencido' | 'Critico' | 'Proximo' | 'Vigente' | 'SinFecha';

const ESTADO_CONFIG: Record<Estado, { label: string; color: 'error' | 'warning' | 'success' | 'default'; icon: React.ReactNode }> = {
  Vencido:  { label: 'Vencido',        color: 'error',   icon: <ErrorIcon fontSize="small" /> },
  Critico:  { label: '≤ 7 días',       color: 'error',   icon: <WarningIcon fontSize="small" /> },
  Proximo:  { label: '≤ 30 días',      color: 'warning', icon: <ScheduleIcon fontSize="small" /> },
  Vigente:  { label: 'Vigente',        color: 'success', icon: <CheckCircleIcon fontSize="small" /> },
  SinFecha: { label: 'Sin vencimiento', color: 'default', icon: <SinFechaIcon fontSize="small" /> },
};

function ChipEstado({ estado, dias }: { estado: Estado; dias?: number }) {
  const cfg = ESTADO_CONFIG[estado];
  const label = estado === 'Vencido'
    ? `Vencido hace ${Math.abs(dias ?? 0)}d`
    : estado === 'SinFecha'
      ? 'Sin fecha'
      : dias != null
        ? `${dias}d`
        : cfg.label;
  return (
    <Chip
      label={label}
      color={cfg.color}
      size="small"
      icon={cfg.icon as React.ReactElement}
      variant={estado === 'Vigente' ? 'outlined' : 'filled'}
    />
  );
}

// ── KPI card ─────────────────────────────────────────────────────────────────

function KpiCard({ label, value, color }: { label: string; value: string | number; color?: string }) {
  return (
    <Paper sx={{ p: 2, flex: 1, minWidth: 120, textAlign: 'center' }}>
      <Typography variant="h4" fontWeight={700} color={color ?? 'text.primary'}>{value}</Typography>
      <Typography variant="body2" color="text.secondary">{label}</Typography>
    </Paper>
  );
}

// ── Fila de tabla ─────────────────────────────────────────────────────────────

function LoteRow({ item, onKardex }: { item: LoteReporteItemDTO; onKardex: (id: number) => void }) {
  return (
    <TableRow hover>
      <TableCell sx={{ fontSize: '0.8rem', maxWidth: 200 }}>
        <Typography variant="body2" fontWeight={500} noWrap>{item.nombreProducto}</Typography>
        {item.codigoBarras && (
          <Typography variant="caption" color="text.secondary">{item.codigoBarras}</Typography>
        )}
      </TableCell>
      <TableCell sx={{ fontSize: '0.8rem' }}>{item.nombreSucursal}</TableCell>
      <TableCell sx={{ fontSize: '0.8rem' }}>{item.numeroLote ?? '—'}</TableCell>
      <TableCell sx={{ fontSize: '0.8rem' }}>
        {item.fechaVencimiento ?? '—'}
      </TableCell>
      <TableCell align="center">
        <ChipEstado estado={item.estadoVencimiento as Estado} dias={item.diasParaVencer} />
      </TableCell>
      <TableCell align="right" sx={{ fontSize: '0.8rem' }}>
        {NUM(item.cantidadDisponible)}
      </TableCell>
      <TableCell align="right" sx={{ fontSize: '0.8rem' }}>
        {COP(item.costoUnitario)}
      </TableCell>
      <TableCell align="right" sx={{ fontSize: '0.8rem', fontWeight: 500 }}>
        {COP(item.valorTotal)}
      </TableCell>
      <TableCell sx={{ fontSize: '0.75rem', color: 'text.secondary' }}>
        {item.referencia ?? '—'}
      </TableCell>
      <TableCell align="center" sx={{ px: 0.5 }}>
        <Tooltip title="Ver kardex">
          <IconButton size="small" onClick={() => onKardex(item.id)}>
            <TimelineIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </TableCell>
    </TableRow>
  );
}

// ── Página principal ──────────────────────────────────────────────────────────

export function ReporteLotesVencimientoPage() {
  const { activeSucursalId } = useAuth();

  const [sucursalId, setSucursalId] = useState<number | ''>(activeSucursalId ?? '');
  const [estadoVencimiento, setEstadoVencimiento] = useState('');
  const [soloConStock, setSoloConStock] = useState(true);
  const [kardexLoteId, setKardexLoteId] = useState<number | null>(null);

  const [applied, setApplied] = useState({
    sucursalId: activeSucursalId ?? ('' as number | ''),
    estadoVencimiento: '',
    soloConStock: true,
  });

  const { data: sucursales = [] } = useQuery({
    queryKey: ['sucursales'],
    queryFn: () => sucursalesApi.getAll(),
  });

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['reporte-lotes', applied],
    queryFn: () =>
      lotesApi.reporte({
        sucursalId: applied.sucursalId !== '' ? (applied.sucursalId as number) : undefined,
        estadoVencimiento: applied.estadoVencimiento || undefined,
        soloConStock: applied.soloConStock,
      }),
  });

  const handleBuscar = () =>
    setApplied({ sucursalId, estadoVencimiento, soloConStock });

  return (
    <Box sx={{ p: 3, maxWidth: 1400, mx: 'auto' }}>
      <ReportePageHeader
        title="Inventario por Lote y Vencimiento"
        subtitle="Stock disponible por lote, fecha de vencimiento y estado de vigencia"
        breadcrumbs={[
          { label: 'Reportes', path: '/reportes' },
          { label: 'Lotes y Vencimientos' },
        ]}
        color="#00695c"
        action={
          <Box sx={{ display: 'flex', gap: 1 }}>
            <Tooltip title="Actualizar datos">
              <Button
                variant="outlined"
                startIcon={<RefreshIcon />}
                onClick={() => refetch()}
                sx={{ color: '#fff', borderColor: 'rgba(255,255,255,0.5)', '&:hover': { borderColor: '#fff', bgcolor: 'rgba(255,255,255,0.1)' } }}
              >
                Actualizar
              </Button>
            </Tooltip>
            {data && (
              <Button
                variant="outlined"
                startIcon={<DownloadIcon />}
                onClick={() => exportarReporteLotes(data)}
                sx={{ color: '#fff', borderColor: 'rgba(255,255,255,0.5)', '&:hover': { borderColor: '#fff', bgcolor: 'rgba(255,255,255,0.1)' } }}
              >
                Excel
              </Button>
            )}
          </Box>
        }
      />

      {/* KPIs */}
      {data && (
        <Box sx={{ display: 'flex', gap: 2, mb: 3, flexWrap: 'wrap' }}>
          <KpiCard label="Total lotes" value={data.totalLotes} />
          <KpiCard label="Total unidades" value={NUM(data.totalUnidades)} />
          <KpiCard label="Valor en inventario" value={COP(data.valorTotalInventario)} />
          <KpiCard label="Vencidos" value={data.lotesVencidos} color={data.lotesVencidos > 0 ? 'error.main' : undefined} />
          <KpiCard label="Críticos (≤7d)" value={data.lotesCriticos} color={data.lotesCriticos > 0 ? 'error.main' : undefined} />
          <KpiCard label="Próximos (≤30d)" value={data.lotesProximos} color={data.lotesProximos > 0 ? 'warning.main' : undefined} />
          <KpiCard label="Vigentes" value={data.lotesVigentes} color="success.main" />
          <KpiCard label="Sin fecha" value={data.lotesSinFecha} />
        </Box>
      )}

      {/* Filtros */}
      <Paper sx={{ p: 2, mb: 3 }}>
        <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'center' }}>
          <FormControl size="small" sx={{ minWidth: 200 }}>
            <InputLabel>Sucursal</InputLabel>
            <Select
              value={sucursalId}
              label="Sucursal"
              onChange={e => setSucursalId(e.target.value as number | '')}
            >
              <MenuItem value="">Todas</MenuItem>
              {sucursales.map((s: { id: number; nombre: string }) => (
                <MenuItem key={s.id} value={s.id}>{s.nombre}</MenuItem>
              ))}
            </Select>
          </FormControl>

          <FormControl size="small" sx={{ minWidth: 180 }}>
            <InputLabel>Estado vencimiento</InputLabel>
            <Select
              value={estadoVencimiento}
              label="Estado vencimiento"
              onChange={e => setEstadoVencimiento(e.target.value)}
            >
              <MenuItem value="">Todos</MenuItem>
              <MenuItem value="Vencido">Vencidos</MenuItem>
              <MenuItem value="Critico">Críticos (≤ 7 días)</MenuItem>
              <MenuItem value="Proximo">Próximos (≤ 30 días)</MenuItem>
              <MenuItem value="Vigente">Vigentes</MenuItem>
              <MenuItem value="SinFecha">Sin fecha de vencimiento</MenuItem>
            </Select>
          </FormControl>

          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
            <Switch
              checked={soloConStock}
              onChange={e => setSoloConStock(e.target.checked)}
              size="small"
            />
            <Typography variant="body2">Solo con stock</Typography>
          </Box>

          <Button variant="contained" onClick={handleBuscar} sx={{ bgcolor: '#00695c', '&:hover': { bgcolor: '#004d40' } }}>
            Buscar
          </Button>
        </Box>
      </Paper>

      {/* Tabla */}
      <Paper>
        {isError && (
          <Alert severity="error" sx={{ m: 2 }}>Error al cargar el informe de lotes.</Alert>
        )}
        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow sx={{ bgcolor: 'grey.50' }}>
                <TableCell>Producto</TableCell>
                <TableCell>Sucursal</TableCell>
                <TableCell>Nº Lote</TableCell>
                <TableCell>Fecha vence</TableCell>
                <TableCell align="center">Estado</TableCell>
                <TableCell align="right">Disponible</TableCell>
                <TableCell align="right">Costo unit.</TableCell>
                <TableCell align="right">Valor total</TableCell>
                <TableCell>Referencia</TableCell>
                <TableCell align="center"></TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {isLoading ? (
                <TableRow>
                  <TableCell colSpan={9} align="center" sx={{ py: 4 }}>Cargando...</TableCell>
                </TableRow>
              ) : !data || data.items.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={9} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                    No se encontraron lotes con los filtros aplicados.
                  </TableCell>
                </TableRow>
              ) : (
                data.items.map(item => <LoteRow key={item.id} item={item} onKardex={setKardexLoteId} />)
              )}
            </TableBody>
          </Table>
        </TableContainer>
        {data && data.items.length > 0 && (
          <Box sx={{ p: 1.5, borderTop: '1px solid', borderColor: 'divider', display: 'flex', justifyContent: 'flex-end' }}>
            <Typography variant="caption" color="text.secondary">
              {data.items.length} lote{data.items.length !== 1 ? 's' : ''} — Valor total: <strong>{COP(data.valorTotalInventario)}</strong>
            </Typography>
          </Box>
        )}
      </Paper>

      <TrazabilidadLoteModal
        loteId={kardexLoteId}
        onClose={() => setKardexLoteId(null)}
      />
    </Box>
  );
}
