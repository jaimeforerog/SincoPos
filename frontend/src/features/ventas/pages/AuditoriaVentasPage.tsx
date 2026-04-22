import { useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  Collapse,
  Dialog,
  DialogContent,
  DialogTitle,
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
  Close as CloseIcon,
  Error as ErrorIcon,
  FilterList as FilterListIcon,
  History as HistoryIcon,
  KeyboardArrowDown as ExpandIcon,
  KeyboardArrowUp as CollapseIcon,
  Refresh as RefreshIcon,
} from '@mui/icons-material';
import { useQuery } from '@tanstack/react-query';
import { reportesApi } from '@/api/reportes';
import { ReportePageHeader } from '@/features/reportes/components/ReportePageHeader';
import { AuditTimeline } from '@/features/auditoria/components/AuditTimeline';
import type { ActivityLogFullDTO, CambioEntidadDTO } from '@/types/api';
import { formatDateTime, formatCurrency } from '@/utils/format';

const ACCIONES_VENTA = [
  'CrearVenta',
  'AnularVenta',
  'DevolucionParcial',
];

const today = () => new Date().toISOString().split('T')[0];

function tryPrettyJson(raw: string): string {
  try { return JSON.stringify(JSON.parse(raw), null, 2); }
  catch { return raw; }
}

// ── Fila expandible ────────────────────────────────────────────────────────

function LogRow({
  log,
  onVerHistorial,
}: {
  log: ActivityLogFullDTO;
  onVerHistorial: (ventaId: string, nombre: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const hasDetail = !!(log.descripcion || log.datosAnteriores || log.datosNuevos || log.mensajeError);

  const accionColor = (accion: string) => {
    if (accion === 'AnularVenta') return 'error';
    if (accion === 'DevolucionParcial') return 'warning';
    return 'primary';
  };

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
        <TableCell sx={{ fontSize: '0.8rem' }}>
          <Tooltip title={log.usuarioEmail}>
            <span>{log.usuarioNombre ?? log.usuarioEmail}</span>
          </Tooltip>
        </TableCell>
        <TableCell sx={{ fontSize: '0.8rem', fontWeight: 500 }}>
          <Chip
            label={log.accion}
            size="small"
            variant="outlined"
            color={accionColor(log.accion)}
          />
        </TableCell>
        <TableCell>
          {log.entidadId && log.tipoEntidad === 'Venta' ? (
            <Tooltip title="Ver historial de la venta">
              <Chip
                label={log.entidadNombre ?? log.entidadId}
                size="small"
                icon={<HistoryIcon />}
                onClick={() => onVerHistorial(log.entidadId!, log.entidadNombre ?? log.entidadId!)}
                sx={{ cursor: 'pointer' }}
              />
            </Tooltip>
          ) : (
            <Typography variant="caption" color="text.secondary">
              {log.entidadNombre ?? log.tipoEntidad ?? '—'}
            </Typography>
          )}
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
          <TableCell colSpan={7} sx={{ py: 0, bgcolor: 'grey.50' }}>
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
                <Box sx={{ display: 'flex', gap: 3, flexWrap: 'wrap' }}>
                  {log.datosAnteriores && (
                    <Box sx={{ flex: 1, minWidth: 280 }}>
                      <Typography variant="caption" fontWeight={700} color="error.main">Datos anteriores</Typography>
                      <Box component="pre" sx={{ fontSize: '0.72rem', bgcolor: 'background.paper', border: '1px solid', borderColor: 'divider', borderRadius: 1, p: 1, overflow: 'auto', maxHeight: 150, whiteSpace: 'pre-wrap', wordBreak: 'break-all' }}>
                        {tryPrettyJson(log.datosAnteriores)}
                      </Box>
                    </Box>
                  )}
                  {log.datosNuevos && (
                    <Box sx={{ flex: 1, minWidth: 280 }}>
                      <Typography variant="caption" fontWeight={700} color="success.main">Datos nuevos</Typography>
                      <Box component="pre" sx={{ fontSize: '0.72rem', bgcolor: 'background.paper', border: '1px solid', borderColor: 'divider', borderRadius: 1, p: 1, overflow: 'auto', maxHeight: 150, whiteSpace: 'pre-wrap', wordBreak: 'break-all' }}>
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

// ── Componente principal ───────────────────────────────────────────────────

export function AuditoriaVentasPage() {
  const [fechaDesde, setFechaDesde] = useState(today());
  const [fechaHasta, setFechaHasta] = useState(today());
  const [accion, setAccion]         = useState('');
  const [usuario, setUsuario]       = useState('');
  const [soloErrores, setSoloErrores] = useState<'' | 'true' | 'false'>('');
  const [page, setPage]             = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(50);

  const [applied, setApplied] = useState({
    fechaDesde: today(),
    fechaHasta: today(),
    accion: '',
    usuario: '',
    soloErrores: '' as '' | 'true' | 'false',
    page: 0,
    rowsPerPage: 50,
  });

  const [historialVentaId, setHistorialVentaId] = useState<string | null>(null);
  const [historialVentaNombre, setHistorialVentaNombre] = useState('');

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['auditoria-ventas', applied],
    queryFn: () => reportesApi.auditoriaVentas({
      fechaDesde: `${applied.fechaDesde}T00:00:00`,
      fechaHasta: `${applied.fechaHasta}T23:59:59`,
      accion: applied.accion || undefined,
      usuarioEmail: applied.usuario || undefined,
      soloErrores: applied.soloErrores !== '' ? applied.soloErrores === 'true' : undefined,
      pageNumber: applied.page + 1,
      pageSize: applied.rowsPerPage,
    }),
  });

  const { data: historial, isLoading: historialLoading } = useQuery({
    queryKey: ['historial-venta', historialVentaId],
    queryFn: () => reportesApi.historialVenta(Number(historialVentaId)),
    enabled: historialVentaId !== null,
  });

  const kpis = data?.kpis;

  const handleBuscar = () => {
    setPage(0);
    setApplied({ fechaDesde, fechaHasta, accion, usuario, soloErrores, page: 0, rowsPerPage });
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
        title="Auditoría de Ventas"
        subtitle="Trazabilidad de ventas, anulaciones y devoluciones"
        breadcrumbs={[
          { label: 'Reportes', path: '/reportes' },
          { label: 'Auditoría de Ventas' },
        ]}
        color="#1b5e20"
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

      {/* KPIs */}
      {kpis && (
        <Box sx={{ display: 'flex', gap: 2, mb: 3, flexWrap: 'wrap' }}>
          <Paper sx={{ p: 2, flex: 1, minWidth: 130, textAlign: 'center' }}>
            <Typography variant="h4" fontWeight={700}>{kpis.totalEventos}</Typography>
            <Typography variant="body2" color="text.secondary">Total eventos</Typography>
          </Paper>
          <Paper sx={{ p: 2, flex: 1, minWidth: 130, textAlign: 'center' }}>
            <Typography variant="h4" fontWeight={700} color="success.main">{kpis.totalVentas}</Typography>
            <Typography variant="body2" color="text.secondary">Ventas</Typography>
          </Paper>
          <Paper sx={{ p: 2, flex: 1, minWidth: 130, textAlign: 'center' }}>
            <Typography variant="h4" fontWeight={700} color="error.main">{kpis.totalAnulaciones}</Typography>
            <Typography variant="body2" color="text.secondary">Anulaciones</Typography>
          </Paper>
          <Paper sx={{ p: 2, flex: 1, minWidth: 130, textAlign: 'center' }}>
            <Typography variant="h4" fontWeight={700} color="warning.main">{kpis.totalDevoluciones}</Typography>
            <Typography variant="body2" color="text.secondary">Devoluciones</Typography>
          </Paper>
          <Paper sx={{ p: 2, flex: 1, minWidth: 160, textAlign: 'center' }}>
            <Typography variant="h5" fontWeight={700} color="success.dark">
              {formatCurrency(kpis.valorTotalVendido)}
            </Typography>
            <Typography variant="body2" color="text.secondary">Total vendido</Typography>
          </Paper>
          <Paper sx={{ p: 2, flex: 1, minWidth: 160, textAlign: 'center' }}>
            <Typography variant="h5" fontWeight={700} color={kpis.valorTotalAnulado > 0 ? 'error.main' : 'text.primary'}>
              {formatCurrency(kpis.valorTotalAnulado)}
            </Typography>
            <Typography variant="body2" color="text.secondary">Total anulado</Typography>
          </Paper>
          <Paper sx={{ p: 2, flex: 1, minWidth: 160, textAlign: 'center' }}>
            <Typography variant="h5" fontWeight={700} color={kpis.valorTotalDevuelto > 0 ? 'warning.main' : 'text.primary'}>
              {formatCurrency(kpis.valorTotalDevuelto)}
            </Typography>
            <Typography variant="body2" color="text.secondary">Total devuelto</Typography>
          </Paper>
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
          <FormControl size="small" sx={{ minWidth: 200 }}>
            <InputLabel>Acción</InputLabel>
            <Select value={accion} label="Acción" onChange={e => setAccion(e.target.value)}>
              <MenuItem value="">Todas</MenuItem>
              {ACCIONES_VENTA.map(a => (
                <MenuItem key={a} value={a}>{a}</MenuItem>
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
              value={soloErrores}
              label="Resultado"
              onChange={e => setSoloErrores(e.target.value as '' | 'true' | 'false')}
            >
              <MenuItem value="">Todos</MenuItem>
              <MenuItem value="false">Exitosos</MenuItem>
              <MenuItem value="true">Solo errores</MenuItem>
            </Select>
          </FormControl>
          <Button variant="contained" color="success" onClick={handleBuscar}>
            Buscar
          </Button>
        </Box>
      </Paper>

      {/* Tabla */}
      <Paper>
        {isError && (
          <Alert severity="error" sx={{ m: 2 }}>Error al cargar la auditoría de ventas.</Alert>
        )}
        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell padding="checkbox" />
                <TableCell>Fecha/Hora</TableCell>
                <TableCell>Usuario</TableCell>
                <TableCell>Acción</TableCell>
                <TableCell>Venta</TableCell>
                <TableCell>Sucursal</TableCell>
                <TableCell align="center">Resultado</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {isLoading ? (
                <TableRow>
                  <TableCell colSpan={7} align="center" sx={{ py: 4 }}>Cargando...</TableCell>
                </TableRow>
              ) : data?.logs.items.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={7} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                    No se encontraron eventos con los filtros aplicados.
                  </TableCell>
                </TableRow>
              ) : (
                data?.logs.items.map(log => (
                  <LogRow
                    key={log.id}
                    log={log}
                    onVerHistorial={(id, nombre) => {
                      setHistorialVentaId(id);
                      setHistorialVentaNombre(nombre);
                    }}
                  />
                ))
              )}
            </TableBody>
          </Table>
        </TableContainer>
        <TablePagination
          component="div"
          count={data?.logs.totalCount ?? 0}
          page={page}
          onPageChange={handleChangePage}
          rowsPerPage={rowsPerPage}
          onRowsPerPageChange={handleChangeRowsPerPage}
          rowsPerPageOptions={[25, 50, 100]}
          labelRowsPerPage="Filas:"
          labelDisplayedRows={({ from, to, count }) => `${from}-${to} de ${count}`}
        />
      </Paper>

      {/* Dialog historial de una venta */}
      <Dialog
        open={historialVentaId !== null}
        onClose={() => setHistorialVentaId(null)}
        maxWidth="md"
        fullWidth
      >
        <DialogTitle sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <Typography fontWeight={600}>Historial: {historialVentaNombre}</Typography>
          <IconButton size="small" onClick={() => setHistorialVentaId(null)}>
            <CloseIcon />
          </IconButton>
        </DialogTitle>
        <DialogContent dividers>
          {historialLoading && (
            <Typography color="text.secondary" sx={{ py: 2, textAlign: 'center' }}>Cargando historial...</Typography>
          )}
          {historial && historial.cambios.length === 0 && (
            <Typography color="text.secondary" sx={{ py: 2, textAlign: 'center' }}>
              No hay eventos registrados para esta venta.
            </Typography>
          )}
          {historial && historial.cambios.length > 0 && (
            <AuditTimeline
              entries={historial.cambios.map((c: CambioEntidadDTO) => ({
                id: String(c.id),
                timestamp: c.fechaHora,
                actor: c.usuarioNombre ?? c.usuarioEmail,
                actorType: 'human' as const,
                action: c.accion,
                details: c.descripcion,
                isAutomated: false,
                isError: !c.exitosa,
              }))}
            />
          )}
        </DialogContent>
      </Dialog>
    </Box>
  );
}
