import { useCallback } from 'react';
import { Box, Typography, CircularProgress, Chip, Button } from '@mui/material';
import WifiOffIcon from '@mui/icons-material/WifiOff';
import WifiIcon from '@mui/icons-material/Wifi';
import SyncIcon from '@mui/icons-material/Sync';
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline';
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline';
import { useOfflineStore } from '@/stores/offline.store';
import { syncPending, discardFailedVentas } from '@/offline/offlineQueue.service';

interface OfflineStatusBannerProps {
  onViewFailed?: () => void;
}

export function OfflineStatusBanner({ onViewFailed }: OfflineStatusBannerProps) {
  const { isOnline, pendingCount, failedCount, isSyncing, syncStatus, lastSyncError } = useOfflineStore();
  const syncNow = useCallback(() => {
    if (!isSyncing) syncPending();
  }, [isSyncing]);

  const discard = useCallback(() => discardFailedVentas(), []);

  // No mostrar el banner si todo está bien (online, sin pendientes, sin fallidas)
  if (isOnline && pendingCount === 0 && failedCount === 0 && syncStatus === 'idle') {
    return null;
  }

  // Offline activo
  if (!isOnline) {
    return (
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          gap: 1.5,
          px: 2,
          py: 1,
          mb: 1.5,
          borderRadius: 2,
          bgcolor: 'warning.light',
          border: '1px solid',
          borderColor: 'warning.main',
        }}
      >
        <WifiOffIcon sx={{ color: 'warning.dark', fontSize: 20 }} />
        <Typography variant="body2" fontWeight={600} sx={{ color: 'warning.dark', flexGrow: 1 }}>
          Modo offline — Las ventas se guardarán localmente
          {pendingCount > 0 && ` (${pendingCount} pendiente${pendingCount !== 1 ? 's' : ''})`}
        </Typography>
      </Box>
    );
  }

  // Sincronizando
  if (isSyncing) {
    return (
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          gap: 1.5,
          px: 2,
          py: 1,
          mb: 1.5,
          borderRadius: 2,
          bgcolor: 'info.light',
          border: '1px solid',
          borderColor: 'info.main',
        }}
      >
        <CircularProgress size={18} sx={{ color: 'info.dark' }} />
        <Typography variant="body2" fontWeight={600} sx={{ color: 'info.dark' }}>
          Sincronizando {pendingCount} venta{pendingCount !== 1 ? 's' : ''}...
        </Typography>
      </Box>
    );
  }

  // Ventas fallidas pendientes de resolución
  if (failedCount > 0 || syncStatus === 'error') {
    return (
      <Box
        sx={{
          display: 'flex',
          flexDirection: 'column',
          gap: 0.5,
          px: 2,
          py: 1,
          mb: 1.5,
          borderRadius: 2,
          bgcolor: 'error.light',
          border: '1px solid',
          borderColor: 'error.main',
        }}
      >
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
          <ErrorOutlineIcon sx={{ color: 'error.dark', fontSize: 20 }} />
          <Typography variant="body2" fontWeight={600} sx={{ color: 'error.dark', flexGrow: 1 }}>
            {failedCount} venta{failedCount !== 1 ? 's' : ''} no se pudo{failedCount !== 1 ? 'eron' : ''} sincronizar
          </Typography>
          {onViewFailed && (
            <Button
              size="small"
              variant="outlined"
              color="error"
              onClick={onViewFailed}
              sx={{ fontSize: '0.75rem', py: 0.25 }}
            >
              Ver detalles
            </Button>
          )}
          <Button
            size="small"
            variant="contained"
            onClick={discard}
            sx={{ fontSize: '0.75rem', py: 0.25, bgcolor: 'error.dark', color: '#fff', '&:hover': { bgcolor: 'error.main' } }}
          >
            Descartar
          </Button>
        </Box>
        {lastSyncError && (
          <Typography variant="caption" sx={{ color: 'error.dark', pl: 3.5 }}>
            {lastSyncError}
          </Typography>
        )}
      </Box>
    );
  }

  // Pendientes online (reconectó, aún no sincronizó)
  if (pendingCount > 0 && isOnline) {
    return (
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          gap: 1.5,
          px: 2,
          py: 1,
          mb: 1.5,
          borderRadius: 2,
          bgcolor: 'info.light',
          border: '1px solid',
          borderColor: 'info.main',
        }}
      >
        <WifiIcon sx={{ color: 'info.dark', fontSize: 20 }} />
        <Typography variant="body2" fontWeight={600} sx={{ color: 'info.dark', flexGrow: 1 }}>
          {pendingCount} venta{pendingCount !== 1 ? 's' : ''} pendiente{pendingCount !== 1 ? 's' : ''} de enviar
        </Typography>
        <Button
          size="small"
          variant="outlined"
          color="info"
          startIcon={<SyncIcon />}
          onClick={syncNow}
          sx={{ fontSize: '0.75rem', py: 0.25 }}
        >
          Sincronizar
        </Button>
      </Box>
    );
  }

  // Sync exitosa (mostrar brevemente)
  if (syncStatus === 'success') {
    return (
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          gap: 1.5,
          px: 2,
          py: 1,
          mb: 1.5,
          borderRadius: 2,
          bgcolor: 'success.light',
          border: '1px solid',
          borderColor: 'success.main',
        }}
      >
        <CheckCircleOutlineIcon sx={{ color: 'success.dark', fontSize: 20 }} />
        <Typography variant="body2" fontWeight={600} sx={{ color: 'success.dark' }}>
          Ventas sincronizadas correctamente
        </Typography>
        <Chip label="✓" size="small" color="success" sx={{ ml: 'auto' }} />
      </Box>
    );
  }

  return null;
}
