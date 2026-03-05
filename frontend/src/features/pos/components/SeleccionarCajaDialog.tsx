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
import { sucursalesApi } from '@/api/sucursales';
import { cajasApi } from '@/api/cajas';
import { useAuth } from '@/hooks/useAuth';

interface SeleccionarCajaDialogProps {
  open: boolean;
  onSelect: (cajaId: number) => void;
}

export function SeleccionarCajaDialog({ open, onSelect }: SeleccionarCajaDialogProps) {
  const { user } = useAuth();
  const [selectedSucursalId, setSelectedSucursalId] = useState<number | null>(null);
  const [selectedCajaId, setSelectedCajaId] = useState<number | null>(null);

  // Auto-seleccionar sucursal del usuario si tiene una asignada
  useEffect(() => {
    if (user?.sucursalId && !selectedSucursalId && open) {
      setSelectedSucursalId(user.sucursalId);
    }
  }, [user, selectedSucursalId, open]);

  // Cargar sucursales
  const { data: sucursales = [], isLoading: loadingSucursales } = useQuery({
    queryKey: ['sucursales'],
    queryFn: () => sucursalesApi.getAll(true),
    enabled: open,
  });

  // Cargar cajas de la sucursal seleccionada
  const { data: cajas = [], isLoading: loadingCajas } = useQuery({
    queryKey: ['cajas', selectedSucursalId],
    queryFn: () =>
      cajasApi.getAll({
        sucursalId: selectedSucursalId ?? undefined,
        estado: 'Abierta',
      }),
    enabled: open && selectedSucursalId !== null,
  });

  const handleSucursalChange = (sucursalId: number) => {
    setSelectedSucursalId(sucursalId);
    setSelectedCajaId(null); // Limpiar caja al cambiar sucursal
  };

  const handleConfirm = () => {
    if (selectedCajaId) {
      onSelect(selectedCajaId);
    }
  };

  const selectedSucursal = sucursales.find((s) => s.id === selectedSucursalId);

  return (
    <Dialog
      open={open}
      maxWidth="sm"
      fullWidth
      disableEscapeKeyDown
      onClose={(_, reason) => {
        // Prevenir cierre con click fuera o ESC
        if (reason === 'backdropClick' || reason === 'escapeKeyDown') {
          return;
        }
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
          {/* Selector de Sucursal */}
          {loadingSucursales ? (
            <Box sx={{ display: 'flex', justifyContent: 'center', p: 3 }}>
              <CircularProgress />
            </Box>
          ) : sucursales.length === 0 ? (
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
                <Box sx={{ display: 'flex', justifyContent: 'center', p: 3 }}>
                  <CircularProgress />
                </Box>
              ) : cajas.length === 0 ? (
                <Alert severity="warning">
                  No hay cajas abiertas en <strong>{selectedSucursal?.nombre}</strong>.
                  <br />
                  Debes abrir una caja primero desde el módulo de Gestión de Cajas.
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
                    </Alert>
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
          disabled={!selectedCajaId}
          size="large"
          color="success"
          fullWidth
        >
          Iniciar Ventas
        </Button>
      </DialogActions>
    </Dialog>
  );
}
