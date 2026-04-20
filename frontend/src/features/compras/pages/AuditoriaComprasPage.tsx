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
import { formatDateTime } from '@/utils/format';

const ACCIONES_COMPRA = [
  'CrearOrdenCompra',
  'AprobarOrdenCompra',
  'RechazarOrdenCompra',
  'RecibirOrdenCompra',
  'CancelarOrdenCompra',
  'DevolucionCompra',
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
  onVerHistorial: (ordenId: string, nombre: string) => void;
}) {
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
        <TableCell sx={{ fontSize: '0.8rem', fontWeight: 500 }}>
          <Chip label={log.accion} size="small" variant="outlined" color="primary" />
        </TableCell>
        <TableCell>
          {log.entidadId && log.tipoEntidad === 'OrdenCompra' ? (
            <Tooltip title="Ver historial de la orden">
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

export function AuditoriaComprasPage() {
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

  // Historial de una orden específica
  const [historialOrdenId, setHistorialOrdenId] = useState<string | null>(null);
  const [historialOrdenNombre, setHistorialOrdenNombre] = useState('');

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['auditoria-compras', applied],
    queryFn: () => reportesApi.auditoriaCompras({
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
    queryKey: ['historial-orden', historialOrdenId],
    queryFn: () => reportesApi.historialOrden(Number(historialOrdenId)),
    enabled: historialOrdenId !== null,
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

  const handleVerHistorial = (ordenId: string, nombre: string) => {
    setHistorialOrdenId(ordenId);
    setHistorialOrdenNombre(nombre);
  };

  return (
    <Box sx={{ p: 3, maxWidth: 1400, mx: 'auto' }}>
      <ReportePageHeader
        title="Auditoría de Compras"
        subtitle="Trazabilidad de aprobaciones, recepciones y devoluciones"
        breadcrumbs={[
          { label: 'Reportes', path: '/reportes' },
          { label: 'Auditoría de Compras' },
        ]}
        color="#1565c0"
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
            <Typography variant="h4" fontWeight={700} color="success.main">{kpis.eventosExitosos}</Typography>
            <Typography variant="body2" color="text.secondary">Exitosos</Typography>
          </Paper>
          <Paper sx={{ p: 2, flex: 1, minWidth: 130, textAlign: 'center' }}>
            <Typography variant="h4" fontWeight={700} color="error.main">{kpis.eventosFallidos}</Typography>
            <Typography variant="body2" color="text.secondary">Fallidos</Typography>
          </Paper>
          <Paper sx={{ p: 2, flex: 1, minWidth: 160, textAlign: 'center' }}>
            <Typography variant="h5" fontWeight={700} color="primary.main">
              ${kpis.valorTotalComprado.toLocaleString('es-CO', { maximumFractionDigits: 0 })}
            </Typography>
            <Typography variant="body2" color="text.secondary">Valor comprado</Typography>
          </Paper>
          <Paper sx={{ p: 2, flex: 1, minWidth: 130, textAlign: 'center' }}>
            <Typography variant="h4" fontWeight={700} color={kpis.ordenesConErrorErp > 0 ? 'warning.main' : 'text.primary'}>
              {kpis.ordenesConErrorErp}
            </Typography>
            <Typography variant="body2" color="text.secondary">Errores ERP</Typography>
          </Paper>
          <Paper sx={{ p: 2, flex: 1, minWidth: 130, textAlign: 'center' }}>
            <Typography variant="h4" fontWeight={700}>{kpis.totalDevoluciones}</Typography>
            <Typography variant="body2" color="text.secondary">Devoluciones</Typography>
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
              {ACCIONES_COMPRA.map(a => (
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
          <Button variant="contained" onClick={handleBuscar}>
            Buscar
          </Button>
        </Box>
      </Paper>

      {/* Tabla */}
      <Paper>
        {isError && (
          <Alert severity="error" sx={{ m: 2 }}>Error al cargar la auditoría de compras.</Alert>
        )}
        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell padding="checkbox" />
                <TableCell>Fecha/Hora</TableCell>
                <TableCell>Usuario</TableCell>
                <TableCell>Acción</TableCell>
                <TableCell>Orden</TableCell>
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
                  <LogRow key={log.id} log={log} onVerHistorial={handleVerHistorial} />
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

      {/* Dialog historial de una orden */}
      <Dialog
        open={historialOrdenId !== null}
        onClose={() => setHistorialOrdenId(null)}
        maxWidth="md"
        fullWidth
      >
        <DialogTitle sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <Typography fontWeight={600}>Historial: {historialOrdenNombre}</Typography>
          <IconButton size="small" onClick={() => setHistorialOrdenId(null)}>
            <CloseIcon />
          </IconButton>
        </DialogTitle>
        <DialogContent dividers>
          {historialLoading && (
            <Typography color="text.secondary" sx={{ py: 2, textAlign: 'center' }}>Cargando historial...</Typography>
          )}
          {historial && historial.cambios.length === 0 && (
            <Typography color="text.secondary" sx={{ py: 2, textAlign: 'center' }}>
              No hay eventos registrados para esta orden.
            </Typography>
          )}
          {historial && historial.cambios.length > 0 && (
            <AuditTimeline
              entries={historial.cambios.map((c: CambioEntidadDTO) => ({
                id: String(c.id),
                timestamp: c.fechaHora,
                actor: c.usuarioEmail,
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
