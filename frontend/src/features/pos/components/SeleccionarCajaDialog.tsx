import { useState, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Alert,
  Box,
  Typography,
  CircularProgress,
} from '@mui/material';
import WifiOffIcon from '@mui/icons-material/WifiOff';
import { sucursalesApi } from '@/api/sucursales';
import { cajasApi } from '@/api/cajas';
import { useAuth } from '@/hooks/useAuth';
import { useOfflineStore } from '@/stores/offline.store';
import { posSessionCache } from '@/offline/posSessionCache';

interface SeleccionarCajaDialogProps {
  open: boolean;
  onSelect: (cajaId: number, sucursalId: number) => void;
}

export function SeleccionarCajaDialog({ open, onSelect }: SeleccionarCajaDialogProps) {
  const { activeSucursalId, activeEmpresaId, user } = useAuth();
  const isOnline = useOfflineStore((s) => s.isOnline);
  const [selectedSucursalId, setSelectedSucursalId] = useState<number | null>(null);
  const [selectedCajaId, setSelectedCajaId] = useState<number | null>(null);

  // Cargar sucursales (online)
  const { data: sucursalesOnline = [], isLoading: loadingSucursales } = useQuery({
    queryKey: ['sucursales'],
    queryFn: async () => {
      const data = await sucursalesApi.getAll(true);
      posSessionCache.saveSucursales(data); // cachear para offline
      return data;
    },
    enabled: open && isOnline,
  });

  // Cargar cajas de la sucursal seleccionada (online)
  const { data: cajasOnline = [], isLoading: loadingCajas } = useQuery({
    queryKey: ['cajas', selectedSucursalId],
    queryFn: () =>
      cajasApi.getAll({
        sucursalId: selectedSucursalId ?? undefined,
        estado: 'Abierta',
      }),
    enabled: open && selectedSucursalId !== null && isOnline,
  });

  // IDs de sucursales del usuario para la empresa activa
  const sucursalIdsEmpresa = new Set(
    (user?.sucursalesDisponibles ?? [])
      .filter(s => activeEmpresaId == null || s.empresaId === activeEmpresaId || s.empresaId == null)
      .map(s => s.id)
  );

  // Datos finales: online → API filtrada por empresa, offline → cache filtrada
  const sucursalesRaw = isOnline ? sucursalesOnline : posSessionCache.loadSucursales();
  const sucursales = sucursalesRaw.filter(s => sucursalIdsEmpresa.size === 0 || sucursalIdsEmpresa.has(s.id));
  const cajas = isOnline
    ? cajasOnline
    : posSessionCache
        .loadCajas()
        .filter((c) => c.sucursalId === selectedSucursalId && c.estado === 'Abierta');

  // Auto-seleccionar la sucursal activa del usuario — solo cuando las opciones ya están disponibles
  useEffect(() => {
    if (activeSucursalId && !selectedSucursalId && open && sucursales.length > 0) {
      if (sucursales.some((s) => s.id === activeSucursalId)) {
        setSelectedSucursalId(activeSucursalId);
      }
    }
  }, [activeSucursalId, selectedSucursalId, open, sucursales]);

  const handleSucursalChange = (sucursalId: number) => {
    setSelectedSucursalId(sucursalId);
    setSelectedCajaId(null); // Limpiar caja al cambiar sucursal
  };

  const handleConfirm = () => {
    if (selectedCajaId && selectedSucursalId) {
      onSelect(selectedCajaId, selectedSucursalId);
    }
  };

  const selectedSucursal = sucursales.find((s) => s.id === selectedSucursalId);
  const isLoading = isOnline && (loadingSucursales || loadingCajas);
  const noCachedData = !isOnline && sucursales.length === 0;

  return (
    <Dialog
      open={open}
      maxWidth="sm"
      fullWidth
      disableEscapeKeyDown
      onClose={(_, reason) => {
        if (reason === 'backdropClick' || reason === 'escapeKeyDown') return;
      }}
    >
      <DialogTitle>
        <Box>
          <Typography variant="h6" sx={{ fontWeight: 700 }}>
            Iniciar Punto de Venta
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
            Selecciona la sucursal y caja para comenzar a vender
          </Typography>
        </Box>
      </DialogTitle>

      <DialogContent>
        <Box sx={{ mt: 2 }}>
          {/* Banner offline */}
          {!isOnline && (
            <Alert
              severity="warning"
              icon={<WifiOffIcon />}
              sx={{ mb: 2 }}
            >
              Sin conexión — mostrando cajas de la última sesión conocida
            </Alert>
          )}

          {/* Sin datos en cache offline */}
          {noCachedData ? (
            <Alert severity="error">
              Sin conexión y sin datos cacheados. Conéctate a internet para iniciar el POS por primera vez.
            </Alert>
          ) : isLoading ? (
            <Box sx={{ display: 'flex', justifyContent: 'center', p: 3 }}>
              <CircularProgress />
            </Box>
          ) : sucursales.length === 0 ? (
            <Alert severity="error" sx={{ mb: 2 }}>
              No hay sucursales disponibles. Contacta al administrador.
            </Alert>
          ) : (
            <>
              {/* Selector de Sucursal */}
              <FormControl fullWidth sx={{ mb: 3 }}>
                <InputLabel>Sucursal *</InputLabel>
                <Select
                  value={selectedSucursalId || ''}
                  onChange={(e) => handleSucursalChange(e.target.value as number)}
                  label="Sucursal *"
                >
                  {sucursales.map((sucursal) => (
                    <MenuItem key={sucursal.id} value={sucursal.id}>
                      {sucursal.nombre}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>

              {/* Selector de Caja */}
              {selectedSucursalId && (
                <>
                  {cajas.length === 0 ? (
                    <Alert severity="warning">
                      No hay cajas abiertas en <strong>{selectedSucursal?.nombre}</strong>.
                      <br />
                      {isOnline
                        ? 'Debes abrir una caja primero desde el módulo de Gestión de Cajas.'
                        : 'No hay cajas cacheadas para esta sucursal.'}
                    </Alert>
                  ) : (
                    <>
                      <FormControl fullWidth>
                        <InputLabel>Caja *</InputLabel>
                        <Select
                          value={selectedCajaId || ''}
                          onChange={(e) => setSelectedCajaId(e.target.value as number)}
                          label="Caja *"
                        >
                          {cajas.map((caja) => (
                            <MenuItem key={caja.id} value={caja.id}>
                              {caja.nombre}
                            </MenuItem>
                          ))}
                        </Select>
                      </FormControl>

                      {selectedCajaId && (
                        <Alert severity="success" sx={{ mt: 2 }}>
                          ✓ Listo para iniciar ventas
                          {!isOnline && ' (modo offline)'}
                        </Alert>
                      )}
                    </>
                  )}
                </>
              )}
            </>
          )}
        </Box>
      </DialogContent>

      <DialogActions sx={{ px: 3, pb: 3 }}>
        <Button
          variant="contained"
          onClick={handleConfirm}
          disabled={!selectedCajaId || noCachedData}
          size="large"
          color="success"
          fullWidth
        >
          Iniciar Ventas {!isOnline && '(Offline)'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
