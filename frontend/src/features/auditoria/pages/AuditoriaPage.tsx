import { useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  Collapse,
  FormControl,
  IconButton,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import {
  CheckCircle as CheckCircleIcon,
  Error as ErrorIcon,
  FilterList as FilterListIcon,
  KeyboardArrowDown as ExpandIcon,
  KeyboardArrowUp as CollapseIcon,
  Refresh as RefreshIcon,
} from '@mui/icons-material';
import { useQuery } from '@tanstack/react-query';
import { activityLogsApi } from '@/api/activityLogs';
import { ReportePageHeader } from '@/features/reportes/components/ReportePageHeader';
import type { ActivityLogFullDTO } from '@/types/api';
import { formatDateTime } from '@/utils/format';

// ── Constantes ─────────────────────────────────────────────────────────────

const TIPOS_ACTIVIDAD: Record<number, string> = {
  1:  'Caja',
  2:  'Venta',
  3:  'Inventario',
  4:  'Usuario',
  10: 'Precio',
  11: 'Producto',
  12: 'Costeo',
  20: 'Configuración',
  99: 'Sistema',
};

const chipColor = (tipo: number): 'error' | 'warning' | 'success' | 'info' | 'default' => {
  const map: Record<number, 'error' | 'warning' | 'success' | 'info'> = {
    1: 'error', 2: 'success', 3: 'info', 4: 'warning',
    10: 'warning', 11: 'info', 12: 'default' as never, 20: 'default' as never, 99: 'default' as never,
  };
  return (map[tipo] as 'error' | 'warning' | 'success' | 'info') ?? 'default';
};

const today = () => new Date().toISOString().split('T')[0];

// ── Fila expandible ────────────────────────────────────────────────────────

function LogRow({ log }: { log: ActivityLogFullDTO }) {
  const [open, setOpen] = useState(false);
  const hasDetail = !!(log.descripcion || log.datosAnteriores || log.datosNuevos || log.mensajeError);

  return (
    <>
      <TableRow hover sx={{ '& > *': { borderBottom: 'unset' } }}>
        <TableCell padding="checkbox">
          {hasDetail && (
            <IconButton size="small" onClick={() => setOpen(!open)}>
              {open ? <CollapseIcon /> : <ExpandIcon />}
            </IconButton>
          )}
        </TableCell>
        <TableCell sx={{ whiteSpace: 'nowrap', fontSize: '0.8rem' }}>
          {formatDateTime(log.fechaHora)}
        </TableCell>
        <TableCell sx={{ fontSize: '0.8rem' }}>{log.usuarioEmail}</TableCell>
        <TableCell sx={{ fontSize: '0.8rem', fontWeight: 500 }}>{log.accion}</TableCell>
        <TableCell>
          <Chip
            label={TIPOS_ACTIVIDAD[log.tipo] ?? log.tipoNombre}
            size="small"
            color={chipColor(log.tipo)}
            variant="outlined"
          />
        </TableCell>
        <TableCell sx={{ fontSize: '0.8rem', color: 'text.secondary' }}>
          {log.tipoEntidad && log.entidadNombre
            ? `${log.tipoEntidad}: ${log.entidadNombre}`
            : log.tipoEntidad ?? '—'}
        </TableCell>
        <TableCell sx={{ fontSize: '0.8rem', color: 'text.secondary' }}>
          {log.nombreSucursal ?? '—'}
        </TableCell>
        <TableCell align="center">
          {log.exitosa
            ? <Tooltip title="Exitosa"><CheckCircleIcon color="success" fontSize="small" /></Tooltip>
            : <Tooltip title={log.mensajeError ?? 'Fallida'}><ErrorIcon color="error" fontSize="small" /></Tooltip>
          }
        </TableCell>
      </TableRow>

      {hasDetail && (
        <TableRow>
          <TableCell colSpan={8} sx={{ py: 0, bgcolor: 'grey.50' }}>
            <Collapse in={open} timeout="auto" unmountOnExit>
              <Box sx={{ py: 2, px: 3, display: 'flex', flexDirection: 'column', gap: 1 }}>
                {log.descripcion && (
                  <Typography variant="body2">
                    <strong>Descripción:</strong> {log.descripcion}
                  </Typography>
                )}
                {log.mensajeError && (
                  <Alert severity="error" sx={{ py: 0 }}>{log.mensajeError}</Alert>
                )}
                {log.ipAddress && (
                  <Typography variant="caption" color="text.secondary">
                    IP: {log.ipAddress}
                  </Typography>
                )}
                <Box sx={{ display: 'flex', gap: 3, flexWrap: 'wrap' }}>
                  {log.datosAnteriores && (
                    <Box sx={{ flex: 1, minWidth: 280 }}>
                      <Typography variant="caption" fontWeight={700} color="error.main">
                        Datos anteriores
                      </Typography>
                      <Box
                        component="pre"
                        sx={{
                          fontSize: '0.72rem', bgcolor: 'background.paper',
                          border: '1px solid', borderColor: 'divider',
                          borderRadius: 1, p: 1, overflow: 'auto', maxHeight: 150,
                          whiteSpace: 'pre-wrap', wordBreak: 'break-all',
                        }}
                      >
                        {tryPrettyJson(log.datosAnteriores)}
                      </Box>
                    </Box>
                  )}
                  {log.datosNuevos && (
                    <Box sx={{ flex: 1, minWidth: 280 }}>
                      <Typography variant="caption" fontWeight={700} color="success.main">
                        Datos nuevos
                      </Typography>
                      <Box
                        component="pre"
                        sx={{
                          fontSize: '0.72rem', bgcolor: 'background.paper',
                          border: '1px solid', borderColor: 'divider',
                          borderRadius: 1, p: 1, overflow: 'auto', maxHeight: 150,
                          whiteSpace: 'pre-wrap', wordBreak: 'break-all',
                        }}
                      >
                        {tryPrettyJson(log.datosNuevos)}
                      </Box>
                    </Box>
                  )}
                </Box>
              </Box>
            </Collapse>
          </TableCell>
        </TableRow>
      )}
    </>
  );
}

function tryPrettyJson(raw: string): string {
  try { return JSON.stringify(JSON.parse(raw), null, 2); }
  catch { return raw; }
}

// ── Componente principal ───────────────────────────────────────────────────

export default function AuditoriaPage() {
  const [fechaDesde, setFechaDesde] = useState(today());
  const [fechaHasta, setFechaHasta] = useState(today());
  const [tipo, setTipo]             = useState<number | ''>('');
  const [usuario, setUsuario]       = useState('');
  const [exitosa, setExitosa]       = useState<'' | 'true' | 'false'>('');
  const [page, setPage]             = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(50);

  // Filtros aplicados (solo al hacer click en "Buscar")
  const [applied, setApplied] = useState({
    fechaDesde: today(),
    fechaHasta: today(),
    tipo: '' as number | '',
    usuario: '',
    exitosa: '' as '' | 'true' | 'false',
    page: 0,
    rowsPerPage: 50,
  });

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['activity-logs', applied],
    queryFn: () => activityLogsApi.getLogs({
      fechaDesde:   applied.fechaDesde ? `${applied.fechaDesde}T00:00:00` : undefined,
      fechaHasta:   applied.fechaHasta ? `${applied.fechaHasta}T23:59:59` : undefined,
      tipo:         applied.tipo !== '' ? applied.tipo : undefined,
      usuarioEmail: applied.usuario || undefined,
      exitosa:      applied.exitosa !== '' ? applied.exitosa === 'true' : undefined,
      pageNumber:   applied.page + 1,
      pageSize:     applied.rowsPerPage,
    }),
  });

  const { data: dashboard } = useQuery({
    queryKey: ['activity-dashboard', applied.fechaDesde, applied.fechaHasta],
    queryFn: () => activityLogsApi.getDashboard(
      applied.fechaDesde ? `${applied.fechaDesde}T00:00:00` : undefined,
      applied.fechaHasta ? `${applied.fechaHasta}T23:59:59` : undefined,
    ),
  });

  const handleBuscar = () => {
    setPage(0);
    setApplied({ fechaDesde, fechaHasta, tipo, usuario, exitosa, page: 0, rowsPerPage });
  };

  const handleChangePage = (_: unknown, newPage: number) => {
    setPage(newPage);
    setApplied(prev => ({ ...prev, page: newPage }));
  };

  const handleChangeRowsPerPage = (e: React.ChangeEvent<HTMLInputElement>) => {
    const rpp = parseInt(e.target.value, 10);
    setRowsPerPage(rpp);
    setPage(0);
    setApplied(prev => ({ ...prev, rowsPerPage: rpp, page: 0 }));
  };

  return (
    <Box sx={{ p: 3, maxWidth: 1400, mx: 'auto' }}>
      <ReportePageHeader
        title="Auditoría de Actividad"
        subtitle="Registro de la trazabilidad y eventos del sistema"
        breadcrumbs={[
          { label: 'Reportes', path: '/reportes' },
          { label: 'Auditoría' },
        ]}
        color="#1976d2"
        action={
          <Button
            variant="outlined"
            startIcon={<RefreshIcon />}
            onClick={() => refetch()}
            sx={{ color: '#fff', borderColor: 'rgba(255,255,255,0.5)', '&:hover': { borderColor: '#fff', bgcolor: 'rgba(255,255,255,0.1)' } }}
          >
            Actualizar
          </Button>
        }
      />

      {/* Métricas del día */}
      {dashboard && (
        <Box sx={{ display: 'flex', gap: 2, mb: 3, flexWrap: 'wrap' }}>
          <Paper sx={{ p: 2, flex: 1, minWidth: 140, textAlign: 'center' }}>
            <Typography variant="h4" fontWeight={700}>{dashboard.totalAcciones}</Typography>
            <Typography variant="body2" color="text.secondary">Total acciones</Typography>
          </Paper>
          <Paper sx={{ p: 2, flex: 1, minWidth: 140, textAlign: 'center' }}>
            <Typography variant="h4" fontWeight={700} color="success.main">{dashboard.accionesExitosas}</Typography>
            <Typography variant="body2" color="text.secondary">Exitosas</Typography>
          </Paper>
          <Paper sx={{ p: 2, flex: 1, minWidth: 140, textAlign: 'center' }}>
            <Typography variant="h4" fontWeight={700} color="error.main">{dashboard.accionesFallidas}</Typography>
            <Typography variant="body2" color="text.secondary">Fallidas</Typography>
          </Paper>
          {Object.entries(dashboard.accionesPorTipo).map(([tipo, count]) => (
            <Paper key={tipo} sx={{ p: 2, flex: 1, minWidth: 120, textAlign: 'center' }}>
              <Typography variant="h5" fontWeight={700}>{count}</Typography>
              <Typography variant="body2" color="text.secondary">{tipo}</Typography>
            </Paper>
          ))}
        </Box>
      )}

      {/* Filtros */}
      <Paper sx={{ p: 2, mb: 3 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 2 }}>
          <FilterListIcon fontSize="small" color="action" />
          <Typography variant="subtitle2" fontWeight={600}>Filtros</Typography>
        </Box>
        <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'flex-end' }}>
          <TextField
            label="Desde"
            type="date"
            size="small"
            value={fechaDesde}
            onChange={e => setFechaDesde(e.target.value)}
            InputLabelProps={{ shrink: true }}
            sx={{ minWidth: 150 }}
          />
          <TextField
            label="Hasta"
            type="date"
            size="small"
            value={fechaHasta}
            onChange={e => setFechaHasta(e.target.value)}
            InputLabelProps={{ shrink: true }}
            sx={{ minWidth: 150 }}
          />
          <FormControl size="small" sx={{ minWidth: 160 }}>
            <InputLabel>Tipo de actividad</InputLabel>
            <Select
              value={tipo}
              label="Tipo de actividad"
              onChange={e => setTipo(e.target.value as number | '')}
            >
              <MenuItem value="">Todos</MenuItem>
              {Object.entries(TIPOS_ACTIVIDAD).map(([id, nombre]) => (
                <MenuItem key={id} value={Number(id)}>{nombre}</MenuItem>
              ))}
            </Select>
          </FormControl>
          <TextField
            label="Usuario (email)"
            size="small"
            value={usuario}
            onChange={e => setUsuario(e.target.value)}
            sx={{ minWidth: 220 }}
          />
          <FormControl size="small" sx={{ minWidth: 130 }}>
            <InputLabel>Resultado</InputLabel>
            <Select
              value={exitosa}
              label="Resultado"
              onChange={e => setExitosa(e.target.value as '' | 'true' | 'false')}
            >
              <MenuItem value="">Todos</MenuItem>
              <MenuItem value="true">Exitosas</MenuItem>
              <MenuItem value="false">Fallidas</MenuItem>
            </Select>
          </FormControl>
          <Button variant="contained" onClick={handleBuscar}>
            Buscar
          </Button>
        </Box>
      </Paper>

      {/* Tabla */}
      <Paper>
        {isError && (
          <Alert severity="error" sx={{ m: 2 }}>Error al cargar los logs de auditoría.</Alert>
        )}
        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell padding="checkbox" />
                <TableCell>Fecha/Hora</TableCell>
                <TableCell>Usuario</TableCell>
                <TableCell>Acción</TableCell>
                <TableCell>Tipo</TableCell>
                <TableCell>Entidad</TableCell>
                <TableCell>Sucursal</TableCell>
                <TableCell align="center">Resultado</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {isLoading ? (
                <TableRow>
                  <TableCell colSpan={8} align="center" sx={{ py: 4 }}>
                    Cargando...
                  </TableCell>
                </TableRow>
              ) : data?.items.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={8} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                    No se encontraron registros con los filtros aplicados.
                  </TableCell>
                </TableRow>
              ) : (
                data?.items.map(log => <LogRow key={log.id} log={log} />)
              )}
            </TableBody>
          </Table>
        </TableContainer>
        <TablePagination
          component="div"
          count={data?.totalCount ?? 0}
          page={page}
          onPageChange={handleChangePage}
          rowsPerPage={rowsPerPage}
          onRowsPerPageChange={handleChangeRowsPerPage}
          rowsPerPageOptions={[25, 50, 100]}
          labelRowsPerPage="Filas:"
          labelDisplayedRows={({ from, to, count }) => `${from}-${to} de ${count}`}
        />
      </Paper>
    </Box>
  );
}
