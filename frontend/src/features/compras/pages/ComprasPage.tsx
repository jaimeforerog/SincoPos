import { useState, useMemo } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Box,
  Button,
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
  Tooltip,
  TextField,
  MenuItem,
  Alert,
  Container,
  alpha,
  Skeleton,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import VisibilityIcon from '@mui/icons-material/Visibility';
import CheckIcon from '@mui/icons-material/Check';
import CloseIcon from '@mui/icons-material/Close';
import LocalShippingIcon from '@mui/icons-material/LocalShipping';
import CancelIcon from '@mui/icons-material/Cancel';
import SyncIcon from '@mui/icons-material/Sync';
import SyncProblemIcon from '@mui/icons-material/SyncProblem';
import CloudDoneIcon from '@mui/icons-material/CloudDone';
import ReplayIcon from '@mui/icons-material/Replay';
import ShoppingCartIcon from '@mui/icons-material/ShoppingCart';
import PendingActionsIcon from '@mui/icons-material/PendingActions';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import AttachMoneyIcon from '@mui/icons-material/AttachMoney';
import FilterListIcon from '@mui/icons-material/FilterList';
import { comprasApi } from '@/api/compras';
import { TableSkeleton } from '@/components/common/TableSkeleton';
import type { OrdenCompraDTO } from '@/types/api';
import { OrdenCompraFormDialog } from '../components/OrdenCompraFormDialog';
import { OrdenCompraDetalleDialog } from '../components/OrdenCompraDetalleDialog';
import { AprobarOrdenDialog } from '../components/AprobarOrdenDialog';
import { RechazarOrdenDialog } from '../components/RechazarOrdenDialog';
import { RecibirOrdenDialog } from '../components/RecibirOrdenDialog';
import { CancelarOrdenDialog } from '../components/CancelarOrdenDialog';

const ESTADOS_ORDEN = [
  { value: '', label: 'Todos los estados' },
  { value: 'Pendiente', label: 'Pendiente' },
  { value: 'Aprobada', label: 'Aprobada' },
  { value: 'RecibidaParcial', label: 'Recibida Parcial' },
  { value: 'RecibidaCompleta', label: 'Recibida Completa' },
  { value: 'Rechazada', label: 'Rechazada' },
  { value: 'Cancelada', label: 'Cancelada' },
];

const ESTADO_META: Record<string, { color: 'warning' | 'info' | 'primary' | 'success' | 'error' | 'default'; label: string }> = {
  Pendiente:         { color: 'warning',  label: 'Pendiente' },
  Aprobada:          { color: 'info',     label: 'Aprobada' },
  RecibidaParcial:   { color: 'primary',  label: 'Rec. Parcial' },
  RecibidaCompleta:  { color: 'success',  label: 'Completada' },
  Rechazada:         { color: 'error',    label: 'Rechazada' },
  Cancelada:         { color: 'default',  label: 'Cancelada' },
};

const HERO_COLOR = '#1565c0';

const formatCurrency = (v: number) =>
  new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', minimumFractionDigits: 0 }).format(v);

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
          <Skeleton variant="text" width={60} sx={{ bgcolor: 'rgba(255,255,255,0.2)' }} />
        ) : (
          <Typography variant="subtitle1" fontWeight={700} sx={{ color: '#fff', lineHeight: 1.2 }}>
            {value}
          </Typography>
        )}
      </Box>
    </Box>
  );
}

export function ComprasPage() {
  const [estadoFiltro, setEstadoFiltro] = useState('');
  const [showFormDialog, setShowFormDialog] = useState(false);
  const [showDetalleDialog, setShowDetalleDialog] = useState(false);
  const [showAprobarDialog, setShowAprobarDialog] = useState(false);
  const [showRechazarDialog, setShowRechazarDialog] = useState(false);
  const [showRecibirDialog, setShowRecibirDialog] = useState(false);
  const [showCancelarDialog, setShowCancelarDialog] = useState(false);
  const [selectedOrden, setSelectedOrden] = useState<OrdenCompraDTO | null>(null);

  const queryClient = useQueryClient();

  const { data: ordenesPage, isLoading, error, refetch } = useQuery({
    queryKey: ['compras', { estado: estadoFiltro }],
    queryFn: () => comprasApi.getAll({ estado: estadoFiltro || undefined, pageSize: 100 }),
  });
  const ordenes = ordenesPage?.items ?? [];

  const { data: erroresErp = [] } = useQuery({
    queryKey: ['erp-outbox-errores'],
    queryFn: () => comprasApi.getErroresErp(),
  });

  const reintentarErpMutation = useMutation({
    mutationFn: (outboxId: number) => comprasApi.reintentarErp(outboxId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['compras'] });
      queryClient.invalidateQueries({ queryKey: ['erp-outbox-errores'] });
    },
  });

  // Stats para el hero (calculadas sobre TODOS los datos sin filtro de estado)
  const { data: todasOrdenes } = useQuery({
    queryKey: ['compras', { estado: '' }],
    queryFn: () => comprasApi.getAll({ pageSize: 1000 }),
    staleTime: 60000,
  });
  const allOrdenes = todasOrdenes?.items ?? [];

  const stats = useMemo(() => ({
    total: allOrdenes.length,
    pendientes: allOrdenes.filter((o) => o.estado === 'Pendiente').length,
    aprobadas: allOrdenes.filter((o) => o.estado === 'Aprobada' || o.estado === 'RecibidaParcial').length,
    valorTotal: allOrdenes.reduce((sum, o) => sum + o.total, 0),
  }), [allOrdenes]);

  const handleVerDetalle = (orden: OrdenCompraDTO) => { setSelectedOrden(orden); setShowDetalleDialog(true); };
  const handleAprobar    = (orden: OrdenCompraDTO) => { setSelectedOrden(orden); setShowAprobarDialog(true); };
  const handleRechazar   = (orden: OrdenCompraDTO) => { setSelectedOrden(orden); setShowRechazarDialog(true); };
  const handleRecibir    = (orden: OrdenCompraDTO) => { setSelectedOrden(orden); setShowRecibirDialog(true); };
  const handleCancelar   = (orden: OrdenCompraDTO) => { setSelectedOrden(orden); setShowCancelarDialog(true); };
  const handleSuccess    = () => refetch();

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
          {/* Título + botón */}
          <Box>
            <Typography variant="h5" fontWeight={700} sx={{ color: '#fff', lineHeight: 1.2 }}>
              Órdenes de Compra
            </Typography>
            <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.75)', mt: 0.5 }}>
              Gestión de compras y recepción de mercancía
            </Typography>
          </Box>

          {/* Stats */}
          <Box
            sx={{
              display: 'flex',
              flexWrap: 'wrap',
              gap: { xs: 2.5, md: 4 },
              alignItems: 'center',
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
            <HeroStat icon={<ShoppingCartIcon />}    label="Total órdenes"  value={stats.total}                    loading={isLoading} />
            <HeroStat icon={<PendingActionsIcon />}  label="Pendientes"     value={stats.pendientes}               loading={isLoading} />
            <HeroStat icon={<CheckCircleIcon />}     label="En proceso"     value={stats.aprobadas}                loading={isLoading} />
            <HeroStat icon={<AttachMoneyIcon />}     label="Valor total"    value={formatCurrency(stats.valorTotal)} loading={isLoading} />

            <Button
              variant="contained"
              startIcon={<AddIcon />}
              onClick={() => setShowFormDialog(true)}
              sx={{
                bgcolor: 'rgba(255,255,255,0.15)',
                color: '#fff',
                border: '1px solid rgba(255,255,255,0.35)',
                fontWeight: 700,
                backdropFilter: 'blur(4px)',
                '&:hover': { bgcolor: 'rgba(255,255,255,0.25)', borderColor: '#fff' },
              }}
            >
              Nueva Orden
            </Button>
          </Box>
        </Box>
      </Box>

      {/* Barra de filtros */}
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          gap: 2,
          mb: 2.5,
          p: 1.5,
          bgcolor: 'background.paper',
          borderRadius: 2,
          border: '1px solid',
          borderColor: 'divider',
        }}
      >
        <FilterListIcon sx={{ color: 'text.secondary', ml: 0.5 }} fontSize="small" />
        <Typography variant="body2" color="text.secondary" fontWeight={500}>
          Filtrar por estado:
        </Typography>
        <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
          {ESTADOS_ORDEN.map((e) => (
            <Chip
              key={e.value}
              label={e.label}
              size="small"
              clickable
              variant={estadoFiltro === e.value ? 'filled' : 'outlined'}
              color={estadoFiltro === e.value ? 'primary' : 'default'}
              onClick={() => setEstadoFiltro(e.value)}
              sx={{ fontWeight: estadoFiltro === e.value ? 700 : 400 }}
            />
          ))}
        </Box>
        <Box sx={{ ml: 'auto' }}>
          <Typography variant="caption" color="text.secondary">
            {ordenes.length} resultado{ordenes.length !== 1 ? 's' : ''}
          </Typography>
        </Box>
      </Box>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          Error al cargar las órdenes: {error instanceof Error ? error.message : 'Error desconocido'}
        </Alert>
      )}

      {/* Tabla */}
      {isLoading ? (
        <TableSkeleton cols={9} />
      ) : !error && (
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
                    color: HERO_COLOR,
                    fontWeight: 700,
                    fontSize: '0.75rem',
                    textTransform: 'uppercase',
                    letterSpacing: '0.04em',
                    borderBottom: `2px solid ${alpha(HERO_COLOR, 0.2)}`,
                  },
                }}
              >
                <TableCell>Número</TableCell>
                <TableCell>Fecha</TableCell>
                <TableCell>Proveedor</TableCell>
                <TableCell>Sucursal</TableCell>
                <TableCell>Pago</TableCell>
                <TableCell align="right">Total</TableCell>
                <TableCell>Estado</TableCell>
                <TableCell align="center">ERP</TableCell>
                <TableCell align="right">Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {ordenes.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={9} align="center" sx={{ py: 6 }}>
                    <ShoppingCartIcon sx={{ fontSize: 40, color: 'text.disabled', mb: 1 }} />
                    <Typography variant="body2" color="text.secondary">
                      No hay órdenes de compra
                      {estadoFiltro ? ` con estado "${estadoFiltro}"` : ' registradas'}
                    </Typography>
                  </TableCell>
                </TableRow>
              ) : (
                ordenes.map((orden) => {
                  const estadoMeta = ESTADO_META[orden.estado] ?? { color: 'default' as const, label: orden.estado };
                  return (
                    <TableRow
                      key={orden.id}
                      hover
                      sx={{
                        '&:hover': { bgcolor: alpha(HERO_COLOR, 0.03) },
                        '&:last-child td': { borderBottom: 0 },
                      }}
                    >
                      <TableCell>
                        <Typography variant="body2" fontWeight={700} color="primary.main">
                          {orden.numeroOrden}
                        </Typography>
                      </TableCell>
                      <TableCell>
                        <Typography variant="body2">
                          {new Date(orden.fechaOrden).toLocaleDateString('es-CO')}
                        </Typography>
                      </TableCell>
                      <TableCell>
                        <Typography variant="body2" fontWeight={500}>{orden.nombreProveedor}</Typography>
                      </TableCell>
                      <TableCell>
                        <Typography variant="body2" color="text.secondary">{orden.nombreSucursal}</Typography>
                      </TableCell>
                      <TableCell>
                        <Typography variant="body2">{orden.formaPago}</Typography>
                        {orden.formaPago === 'Credito' && (
                          <Typography variant="caption" color="text.secondary">
                            {orden.diasPlazo} días
                          </Typography>
                        )}
                      </TableCell>
                      <TableCell align="right">
                        <Typography variant="body2" fontWeight={600}>
                          {formatCurrency(orden.total)}
                        </Typography>
                      </TableCell>
                      <TableCell>
                        <Chip
                          label={estadoMeta.label}
                          color={estadoMeta.color}
                          size="small"
                          sx={{ fontWeight: 600 }}
                        />
                      </TableCell>
                      <TableCell align="center">
                        {(orden.estado === 'RecibidaParcial' || orden.estado === 'RecibidaCompleta') ? (
                          orden.sincronizadoErp ? (
                            <Tooltip title={`Sincronizado — Ref: ${orden.erpReferencia}`}>
                              <Chip size="small" icon={<CloudDoneIcon />} label="OK" color="success" variant="outlined" />
                            </Tooltip>
                          ) : orden.errorSincronizacion ? (
                            <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 0.5 }}>
                              <Tooltip title={`Error ERP: ${orden.errorSincronizacion}`}>
                                <Chip size="small" icon={<SyncProblemIcon />} label="Error" color="error" variant="outlined" />
                              </Tooltip>
                              {(() => {
                                const outboxError = erroresErp.find(e => e.entidadId === orden.id && e.tipoDocumento === 'CompraRecibida');
                                if (!outboxError) return null;
                                return (
                                  <Tooltip title="Reintentar sincronización ERP">
                                    <IconButton
                                      size="small"
                                      color="warning"
                                      disabled={reintentarErpMutation.isPending}
                                      onClick={() => reintentarErpMutation.mutate(outboxError.id)}
                                    >
                                      <ReplayIcon fontSize="small" />
                                    </IconButton>
                                  </Tooltip>
                                );
                              })()}
                            </Box>
                          ) : (
                            <Tooltip title="Pendiente de sincronización">
                              <Chip size="small" icon={<SyncIcon />} label="Pendiente" color="default" variant="outlined" />
                            </Tooltip>
                          )
                        ) : (
                          <Typography variant="caption" color="text.disabled">—</Typography>
                        )}
                      </TableCell>
                      <TableCell align="right">
                        <Tooltip title="Ver detalle">
                          <IconButton size="small" onClick={() => handleVerDetalle(orden)}>
                            <VisibilityIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>

                        {orden.estado === 'Pendiente' && (
                          <>
                            <Tooltip title="Aprobar">
                              <IconButton size="small" color="success" onClick={() => handleAprobar(orden)}>
                                <CheckIcon fontSize="small" />
                              </IconButton>
                            </Tooltip>
                            <Tooltip title="Rechazar">
                              <IconButton size="small" color="error" onClick={() => handleRechazar(orden)}>
                                <CloseIcon fontSize="small" />
                              </IconButton>
                            </Tooltip>
                          </>
                        )}

                        {(orden.estado === 'Aprobada' || orden.estado === 'RecibidaParcial') && (
                          <Tooltip title="Recibir mercancía">
                            <IconButton size="small" color="primary" onClick={() => handleRecibir(orden)}>
                              <LocalShippingIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        )}

                        {(orden.estado === 'Pendiente' || orden.estado === 'Aprobada') && (
                          <Tooltip title="Cancelar">
                            <IconButton size="small" onClick={() => handleCancelar(orden)}>
                              <CancelIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        )}
                      </TableCell>
                    </TableRow>
                  );
                })
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Diálogos */}
      <OrdenCompraFormDialog
        open={showFormDialog}
        onClose={() => setShowFormDialog(false)}
        onSuccess={handleSuccess}
      />

      {selectedOrden && (
        <>
          <OrdenCompraDetalleDialog open={showDetalleDialog} orden={selectedOrden} onClose={() => setShowDetalleDialog(false)} />
          <AprobarOrdenDialog open={showAprobarDialog} orden={selectedOrden} onClose={() => setShowAprobarDialog(false)} onSuccess={handleSuccess} />
          <RechazarOrdenDialog open={showRechazarDialog} orden={selectedOrden} onClose={() => setShowRechazarDialog(false)} onSuccess={handleSuccess} />
          <RecibirOrdenDialog open={showRecibirDialog} orden={selectedOrden} onClose={() => setShowRecibirDialog(false)} onSuccess={handleSuccess} />
          <CancelarOrdenDialog open={showCancelarDialog} orden={selectedOrden} onClose={() => setShowCancelarDialog(false)} onSuccess={handleSuccess} />
        </>
      )}
    </Container>
  );
}
