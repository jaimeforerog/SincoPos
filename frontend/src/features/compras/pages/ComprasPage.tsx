import { useState } from 'react';
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
  Stack,
  Alert,
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

const getEstadoColor = (estado: string) => {
  switch (estado) {
    case 'Pendiente':
      return 'warning';
    case 'Aprobada':
      return 'info';
    case 'RecibidaParcial':
      return 'primary';
    case 'RecibidaCompleta':
      return 'success';
    case 'Rechazada':
      return 'error';
    case 'Cancelada':
      return 'default';
    default:
      return 'default';
  }
};

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
    queryFn: () => comprasApi.getAll({
      estado: estadoFiltro || undefined,
      pageSize: 100,
    }),
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

  const handleVerDetalle = (orden: OrdenCompraDTO) => {
    setSelectedOrden(orden);
    setShowDetalleDialog(true);
  };

  const handleAprobar = (orden: OrdenCompraDTO) => {
    setSelectedOrden(orden);
    setShowAprobarDialog(true);
  };

  const handleRechazar = (orden: OrdenCompraDTO) => {
    setSelectedOrden(orden);
    setShowRechazarDialog(true);
  };

  const handleRecibir = (orden: OrdenCompraDTO) => {
    setSelectedOrden(orden);
    setShowRecibirDialog(true);
  };

  const handleCancelar = (orden: OrdenCompraDTO) => {
    setSelectedOrden(orden);
    setShowCancelarDialog(true);
  };

  const handleSuccess = () => {
    refetch();
  };

  return (
    <Box sx={{ p: 3 }}>
      {/* Header */}
      <Box sx={{ mb: 3, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h4">Órdenes de Compra</Typography>
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          onClick={() => setShowFormDialog(true)}
        >
          Nueva Orden
        </Button>
      </Box>

      {/* Filtros */}
      <Paper sx={{ p: 2, mb: 3 }}>
        <Stack direction="row" spacing={2}>
          <TextField
            select
            label="Estado"
            value={estadoFiltro}
            onChange={(e) => setEstadoFiltro(e.target.value)}
            sx={{ minWidth: 200 }}
          >
            {ESTADOS_ORDEN.map((estado) => (
              <MenuItem key={estado.value} value={estado.value}>
                {estado.label}
              </MenuItem>
            ))}
          </TextField>
        </Stack>
      </Paper>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          Error al cargar las órdenes de compra: {error instanceof Error ? error.message : 'Error desconocido'}
        </Alert>
      )}

      {/* Tabla */}
      {isLoading ? (
        <TableSkeleton cols={9} />
      ) : !error && (
        <TableContainer component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
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
                  <TableCell colSpan={7} align="center">
                    <Typography variant="body2" color="text.secondary" sx={{ py: 4 }}>
                      No hay órdenes de compra registradas
                    </Typography>
                  </TableCell>
                </TableRow>
              ) : (
                ordenes.map((orden) => (
                  <TableRow key={orden.id} hover>
                    <TableCell>
                      <Typography variant="body2" fontWeight="medium">
                        {orden.numeroOrden}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      {new Date(orden.fechaOrden).toLocaleDateString('es-CO')}
                    </TableCell>
                    <TableCell>{orden.nombreProveedor}</TableCell>
                    <TableCell>{orden.nombreSucursal}</TableCell>
                    <TableCell>
                      <Typography variant="body2">
                        {orden.formaPago}
                      </Typography>
                      {orden.formaPago === 'Credito' && (
                        <Typography variant="caption" color="text.secondary">
                          ({orden.diasPlazo} días)
                        </Typography>
                      )}
                    </TableCell>
                    <TableCell align="right">
                      ${orden.total.toLocaleString('es-CO')}
                    </TableCell>
                    <TableCell>
                      <Chip
                        label={orden.estado}
                        color={getEstadoColor(orden.estado)}
                        size="small"
                      />
                    </TableCell>
                    <TableCell align="center">
                      {(orden.estado === 'RecibidaParcial' || orden.estado === 'RecibidaCompleta') ? (
                        orden.sincronizadoErp ? (
                          <Tooltip title={`Sincronizado correctamente - Ref: ${orden.erpReferencia}`}>
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
                          <Tooltip title="Sin sincronizar o Procesando...">
                            <Chip size="small" icon={<SyncIcon />} label="Pendiente" color="default" variant="outlined" />
                          </Tooltip>
                        )
                      ) : (
                        <Typography variant="caption" color="text.secondary">—</Typography>
                      )}
                    </TableCell>
                    <TableCell align="right">
                      <Tooltip title="Ver detalle">
                        <IconButton
                          size="small"
                          onClick={() => handleVerDetalle(orden)}
                        >
                          <VisibilityIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>

                      {orden.estado === 'Pendiente' && (
                        <>
                          <Tooltip title="Aprobar">
                            <IconButton
                              size="small"
                              color="success"
                              onClick={() => handleAprobar(orden)}
                            >
                              <CheckIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                          <Tooltip title="Rechazar">
                            <IconButton
                              size="small"
                              color="error"
                              onClick={() => handleRechazar(orden)}
                            >
                              <CloseIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        </>
                      )}

                      {(orden.estado === 'Aprobada' || orden.estado === 'RecibidaParcial') && (
                        <Tooltip title="Recibir mercancía">
                          <IconButton
                            size="small"
                            color="primary"
                            onClick={() => handleRecibir(orden)}
                          >
                            <LocalShippingIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      )}

                      {(orden.estado === 'Pendiente' || orden.estado === 'Aprobada') && (
                        <Tooltip title="Cancelar">
                          <IconButton
                            size="small"
                            onClick={() => handleCancelar(orden)}
                          >
                            <CancelIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      )}
                    </TableCell>
                  </TableRow>
                ))
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
          <OrdenCompraDetalleDialog
            open={showDetalleDialog}
            orden={selectedOrden}
            onClose={() => setShowDetalleDialog(false)}
          />

          <AprobarOrdenDialog
            open={showAprobarDialog}
            orden={selectedOrden}
            onClose={() => setShowAprobarDialog(false)}
            onSuccess={handleSuccess}
          />

          <RechazarOrdenDialog
            open={showRechazarDialog}
            orden={selectedOrden}
            onClose={() => setShowRechazarDialog(false)}
            onSuccess={handleSuccess}
          />

          <RecibirOrdenDialog
            open={showRecibirDialog}
            orden={selectedOrden}
            onClose={() => setShowRecibirDialog(false)}
            onSuccess={handleSuccess}
          />

          <CancelarOrdenDialog
            open={showCancelarDialog}
            orden={selectedOrden}
            onClose={() => setShowCancelarDialog(false)}
            onSuccess={handleSuccess}
          />
        </>
      )}
    </Box>
  );
}
