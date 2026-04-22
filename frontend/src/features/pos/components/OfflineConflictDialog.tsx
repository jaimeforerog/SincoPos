import { useState, useEffect, useCallback } from 'react';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Box,
  Typography,
  Button,
  IconButton,
  Chip,
  Divider,
  Tooltip,
  CircularProgress,
} from '@mui/material';
import CloseIcon          from '@mui/icons-material/Close';
import ErrorOutlineIcon   from '@mui/icons-material/ErrorOutline';
import ReplayIcon         from '@mui/icons-material/Replay';
import DeleteOutlineIcon  from '@mui/icons-material/DeleteOutline';
import {
  getFailedVentasForDisplay,
  retryVenta,
  deleteFailedVenta,
  discardFailedVentas,
  syncPending,
} from '@/offline/offlineQueue.service';
import type { OfflineVenta } from '@/offline/posIdb';
import { sincoColors } from '@/theme/tokens';

// ── Helpers ────────────────────────────────────────────────────────────────

const METODO_PAGO_LABELS: Record<number, string> = {
  0: 'Efectivo',
  1: 'Tarjeta',
  2: 'Transferencia',
  3: 'Mixto',
};

function calcTotal(venta: OfflineVenta): number {
  return venta.payload.lineas.reduce(
    (sum, l) => sum + (l.precioUnitario ?? 0) * l.cantidad - (l.descuento ?? 0),
    0,
  );
}

function fmtDate(iso: string): string {
  return new Intl.DateTimeFormat('es-CO', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(new Date(iso));
}

function fmtCurrency(n: number): string {
  return new Intl.NumberFormat('es-CO', {
    style: 'currency',
    currency: 'COP',
    minimumFractionDigits: 0,
  }).format(n);
}

// ── Props ──────────────────────────────────────────────────────────────────

interface OfflineConflictDialogProps {
  open: boolean;
  onClose: () => void;
}

// ── Componente ─────────────────────────────────────────────────────────────

/**
 * Muestra las ventas offline que fallaron durante la sincronización.
 * Permite ver el detalle del error, reintentar o descartar cada venta.
 */
export function OfflineConflictDialog({ open, onClose }: OfflineConflictDialogProps) {
  const [ventas, setVentas]       = useState<OfflineVenta[]>([]);
  const [loading, setLoading]     = useState(false);
  const [retrying, setRetrying]   = useState<string | null>(null);
  const [deleting, setDeleting]   = useState<string | null>(null);

  const reload = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getFailedVentasForDisplay();
      setVentas(data);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (open) reload();
  }, [open, reload]);

  const handleRetry = async (localId: string) => {
    setRetrying(localId);
    try {
      await retryVenta(localId);
      await syncPending();
      await reload();
    } finally {
      setRetrying(null);
    }
  };

  const handleDelete = async (localId: string) => {
    setDeleting(localId);
    try {
      await deleteFailedVenta(localId);
      await reload();
    } finally {
      setDeleting(null);
    }
  };

  const handleDiscardAll = async () => {
    setLoading(true);
    try {
      await discardFailedVentas();
      setVentas([]);
      onClose();
    } finally {
      setLoading(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ display: 'flex', alignItems: 'center', gap: 1, pb: 1 }}>
        <ErrorOutlineIcon sx={{ color: 'error.main' }} />
        <Typography variant="h6" component="span" sx={{ flexGrow: 1 }}>
          Ventas con error de sincronización
        </Typography>
        <IconButton size="small" onClick={onClose} aria-label="Cerrar diálogo">
          <CloseIcon fontSize="small" />
        </IconButton>
      </DialogTitle>

      <Divider />

      <DialogContent sx={{ p: 0 }}>
        {loading ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
            <CircularProgress size={32} />
          </Box>
        ) : ventas.length === 0 ? (
          <Box sx={{ py: 4, textAlign: 'center' }}>
            <Typography variant="body2" color="text.secondary">
              No hay ventas con errores pendientes.
            </Typography>
          </Box>
        ) : (
          ventas.map((v, i) => {
            const total   = calcTotal(v);
            const metodo  = METODO_PAGO_LABELS[v.payload.metodoPago] ?? 'Desconocido';
            const isBusy  = retrying === v.localId || deleting === v.localId;

            return (
              <Box
                key={v.localId}
                sx={{
                  px: 2.5,
                  py: 2,
                  borderBottom: i < ventas.length - 1 ? '1px solid' : 'none',
                  borderColor: 'divider',
                  bgcolor: sincoColors.error.bg,
                }}
              >
                {/* Encabezado fila */}
                <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 1, mb: 0.75 }}>
                  <Box sx={{ flexGrow: 1 }}>
                    <Typography variant="body2" fontWeight={600}>
                      {fmtDate(v.creadoEn)}
                    </Typography>
                    <Box sx={{ display: 'flex', gap: 0.75, mt: 0.5, flexWrap: 'wrap' }}>
                      <Chip
                        label={`${v.payload.lineas.length} ítem${v.payload.lineas.length !== 1 ? 's' : ''}`}
                        size="small"
                        sx={{ height: 20, fontSize: '0.68rem' }}
                      />
                      <Chip
                        label={fmtCurrency(total)}
                        size="small"
                        sx={{ height: 20, fontSize: '0.68rem' }}
                      />
                      <Chip
                        label={metodo}
                        size="small"
                        sx={{ height: 20, fontSize: '0.68rem' }}
                      />
                      <Chip
                        label={`${v.intentos} intento${v.intentos !== 1 ? 's' : ''}`}
                        size="small"
                        color="warning"
                        sx={{ height: 20, fontSize: '0.68rem' }}
                      />
                    </Box>
                  </Box>

                  {/* Acciones */}
                  <Box sx={{ display: 'flex', gap: 0.5, flexShrink: 0 }}>
                    <Tooltip title="Reintentar envío">
                      <span>
                        <IconButton
                          size="small"
                          color="primary"
                          disabled={isBusy}
                          onClick={() => handleRetry(v.localId)}
                          aria-label="Reintentar venta"
                        >
                          {retrying === v.localId
                            ? <CircularProgress size={16} />
                            : <ReplayIcon fontSize="small" />
                          }
                        </IconButton>
                      </span>
                    </Tooltip>
                    <Tooltip title="Eliminar esta venta">
                      <span>
                        <IconButton
                          size="small"
                          color="error"
                          disabled={isBusy}
                          onClick={() => handleDelete(v.localId)}
                          aria-label="Eliminar"
                        >
                          {deleting === v.localId
                            ? <CircularProgress size={16} />
                            : <DeleteOutlineIcon fontSize="small" />
                          }
                        </IconButton>
                      </span>
                    </Tooltip>
                  </Box>
                </Box>

                {/* Error */}
                {v.errorMensaje && (
                  <Box
                    sx={{
                      mt: 0.75,
                      p: 0.75,
                      bgcolor: 'background.paper',
                      borderRadius: 1,
                      borderLeft: `3px solid ${sincoColors.error.main}`,
                    }}
                  >
                    <Typography variant="caption" color="error.dark">
                      {v.errorMensaje}
                    </Typography>
                  </Box>
                )}
              </Box>
            );
          })
        )}
      </DialogContent>

      <Divider />

      <DialogActions sx={{ px: 2.5, py: 1.5, gap: 1 }}>
        <Button onClick={onClose} size="small">
          Cerrar
        </Button>
        <Box sx={{ flexGrow: 1 }} />
        {ventas.length > 0 && (
          <>
            <Button
              size="small"
              variant="outlined"
              color="primary"
              startIcon={<ReplayIcon />}
              disabled={loading}
              onClick={async () => {
                setLoading(true);
                try {
                  for (const v of ventas) await retryVenta(v.localId);
                  await syncPending();
                  await reload();
                } finally {
                  setLoading(false);
                }
              }}
            >
              Reintentar todas
            </Button>
            <Button
              size="small"
              variant="contained"
              color="error"
              startIcon={<DeleteOutlineIcon />}
              disabled={loading}
              onClick={handleDiscardAll}
            >
              Descartar todas
            </Button>
          </>
        )}
      </DialogActions>
    </Dialog>
  );
}
