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
  onClose?: () => void;
}

export function SeleccionarCajaDialog({ open, onSelect, onClose }: SeleccionarCajaDialogProps) {
  const { activeSucursalId, activeEmpresaId, user } = useAuth();
  const isOnline = useOfflineStore((s) => s.isOnline);
  const [selectedSucursalId, setSelectedSucursalId] = useState<number | null>(null);
  const [selectedCajaId, setSelectedCajaId] = useState<number | null>(null);

  // Cargar sucursales desde la API (filtradas por empresa activa vía middleware)
  const { data: sucursalesOnline = [] } = useQuery({
    queryKey: ['sucursales', activeEmpresaId],
    queryFn: async () => {
      const data = await sucursalesApi.getAll();
      posSessionCache.saveSucursales(data);
      return data;
    },
    enabled: open && isOnline,
    staleTime: 0,
  });

  // Resetear selecciones cada vez que el diálogo se abre
  useEffect(() => {
    if (open) {
      setSelectedSucursalId(null);
      setSelectedCajaId(null);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  // Cargar cajas de la sucursal seleccionada
  const { data: cajasOnline = [], isLoading: loadingCajas } = useQuery({
    queryKey: ['cajas', selectedSucursalId],
    queryFn: () =>
      cajasApi.getAll({
        sucursalId: selectedSucursalId ?? undefined,
        estado: 'Abierta',
      }),
    enabled: open && selectedSucursalId !== null && isOnline,
  });

  // Lista de sucursales: si el usuario tiene sucursales asignadas, intersectar;
  // si no tiene (admin sin asignación explícita), mostrar todas las de la empresa (ya filtradas por backend)
  const filtrarPorAsignadas = (lista: typeof sucursalesOnline) =>
    user?.sucursalesDisponibles?.length
      ? lista.filter((s) => user.sucursalesDisponibles.some((sd) => sd.id === s.id))
      : lista;

  const sucursales = isOnline
    ? filtrarPorAsignadas(sucursalesOnline)
    : filtrarPorAsignadas(posSessionCache.loadSucursales());

  const cajas = isOnline
    ? cajasOnline
    : posSessionCache
        .loadCajas()
        .filter((c) => c.sucursalId === selectedSucursalId && c.estado === 'Abierta');

  // Auto-seleccionar la sucursal activa cuando las opciones están disponibles
  useEffect(() => {
    if (activeSucursalId && !selectedSucursalId && open && sucursales.length > 0) {
      if (sucursales.some((s) => s.id === activeSucursalId)) {
        setSelectedSucursalId(activeSucursalId);
      }
    }
  }, [activeSucursalId, selectedSucursalId, open, sucursales]);

  const handleSucursalChange = (sucursalId: number) => {
    setSelectedSucursalId(sucursalId);
    setSelectedCajaId(null);
  };

  const handleConfirm = () => {
    if (selectedCajaId && selectedSucursalId) {
      onSelect(selectedCajaId, selectedSucursalId);
    }
  };

  const selectedSucursal = sucursales.find((s) => s.id === selectedSucursalId);
  const noCachedData = !isOnline && sucursales.length === 0;

  return (
    <Dialog
      open={open}
      maxWidth="sm"
      fullWidth
      disableEscapeKeyDown={!onClose}
      onClose={(_, reason) => {
        if (reason === 'backdropClick') return;
        onClose?.();
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
            <Alert severity="warning" icon={<WifiOffIcon />} sx={{ mb: 2 }}>
              Sin conexión — mostrando cajas de la última sesión conocida
            </Alert>
          )}

          {noCachedData ? (
            <Alert severity="error">
              Sin conexión y sin datos cacheados. Conéctate a internet para iniciar el POS por primera vez.
            </Alert>
          ) : (
            <>
              {/* Selector de Sucursal */}
              {sucursales.length === 0 ? (
                <Alert severity="error" sx={{ mb: 2 }}>
                  No hay sucursales disponibles. Contacta al administrador.
                </Alert>
              ) : (
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
              )}

              {/* Selector de Caja */}
              {selectedSucursalId && (
                <>
                  {loadingCajas ? (
                    <Box sx={{ display: 'flex', justifyContent: 'center', p: 2 }}>
                      <CircularProgress size={28} />
                    </Box>
                  ) : cajas.length === 0 ? (
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

      <DialogActions sx={{ px: 3, pb: 3, gap: 1 }}>
        {onClose && (
          <Button variant="outlined" onClick={onClose} size="large">
            Cancelar
          </Button>
        )}
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
