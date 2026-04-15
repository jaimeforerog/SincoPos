import { useState, useEffect, useMemo } from 'react';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Typography,
  Box,
  Avatar,
  Button,
  Autocomplete,
  TextField,
  CircularProgress,
} from '@mui/material';
import BusinessIcon from '@mui/icons-material/Business';
import StoreIcon from '@mui/icons-material/Store';
import { useQuery } from '@tanstack/react-query';
import { useAuthStore } from '@/stores/auth.store';
import { sucursalesApi } from '@/api/sucursales';

export function SeleccionarEmpresaDialog() {
  const { isAuthenticated, isLoading, activeEmpresaId, setActiveEmpresa, setActiveSucursal, user, empresasDisponibles } = useAuthStore();

  const [selectedEmpresa, setSelectedEmpresa] = useState<{ id: number; nombre: string } | null>(null);
  const [selectedSucursal, setSelectedSucursal] = useState<{ id: number; nombre: string } | null>(null);

  // Empresas disponibles desde el store (ya vienen del /me endpoint, sin restricción de rol)
  const empresas = empresasDisponibles;
  const loadingEmpresas = isLoading;

  // Sucursales desde la API con el X-Empresa-Id explícito
  const { data: sucursalesApi_data = [], isLoading: loadingSucursales } = useQuery({
    queryKey: ['sucursales-empresa', selectedEmpresa?.id],
    queryFn: () =>
      sucursalesApi
        .getByEmpresa(selectedEmpresa!.id)
        .then(list => list.map(s => ({ id: s.id, nombre: s.nombre }))),
    enabled: !!selectedEmpresa,
    staleTime: 60_000,
  });

  // Si la API devuelve vacío (sucursales con empresaId=null no migradas),
  // caemos en las sucursales asignadas al usuario en el store
  const sucursalesDeEmpresa = useMemo(() => {
    if (!selectedEmpresa) return [];
    if (sucursalesApi_data.length > 0) return sucursalesApi_data;
    // Fallback: sucursales del store que coincidan con la empresa o no tengan empresa asignada
    return (user?.sucursalesDisponibles ?? [])
      .filter(s => s.empresaId === selectedEmpresa.id)
      .map(s => ({ id: s.id, nombre: s.nombre }));
  }, [sucursalesApi_data, selectedEmpresa, user?.sucursalesDisponibles]);

  // Auto-seleccionar si hay exactamente 1 sucursal
  useEffect(() => {
    if (!loadingSucursales) {
      if (sucursalesDeEmpresa.length === 1) {
        setSelectedSucursal(sucursalesDeEmpresa[0]);
      } else if (sucursalesDeEmpresa.length > 1) {
        setSelectedSucursal(null);
      }
    }
  }, [sucursalesDeEmpresa, loadingSucursales]);

  // Auto-seleccionar empresa si solo hay una disponible
  useEffect(() => {
    if (empresas.length === 1 && !selectedEmpresa) {
      setSelectedEmpresa(empresas[0]);
    }
  }, [empresas]); // eslint-disable-line react-hooks/exhaustive-deps

  const open = isAuthenticated && !isLoading && activeEmpresaId === undefined;
  if (!open) return null;

  const canConfirm =
    selectedEmpresa !== null &&
    !loadingSucursales &&
    (sucursalesDeEmpresa.length === 0 || selectedSucursal !== null);

  const handleEmpresaChange = (val: { id: number; nombre: string } | null) => {
    setSelectedEmpresa(val);
    setSelectedSucursal(null);
  };

  const handleConfirm = () => {
    if (!selectedEmpresa) return;
    setActiveEmpresa(selectedEmpresa.id, selectedEmpresa.nombre);
    if (selectedSucursal) {
      setActiveSucursal(selectedSucursal.id);
    }
  };

  return (
    <Dialog
      open
      maxWidth="xs"
      fullWidth
      disableEscapeKeyDown
      slotProps={{ backdrop: { sx: { bgcolor: 'rgba(255,255,255,0.97)' } } }}
    >
      <DialogTitle sx={{ textAlign: 'center', pt: 4 }}>
        <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 1.5 }}>
          <Avatar sx={{ bgcolor: 'primary.main', width: 56, height: 56 }}>
            <BusinessIcon sx={{ fontSize: 32 }} />
          </Avatar>
          <Typography variant="h6" fontWeight={700}>
            Seleccionar Empresa y Sucursal
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Selecciona con qué empresa y sucursal deseas trabajar
          </Typography>
        </Box>
      </DialogTitle>

      <DialogContent sx={{ pb: 1 }}>
        {loadingEmpresas ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 3 }}>
            <CircularProgress />
          </Box>
        ) : (
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5, mt: 1 }}>
            {/* Selector de empresa */}
            <Autocomplete
              options={empresas}
              getOptionLabel={(o) => o.nombre}
              isOptionEqualToValue={(o, v) => o.id === v.id}
              value={selectedEmpresa}
              onChange={(_, val) => handleEmpresaChange(val)}
              renderInput={(params) => (
                <TextField
                  {...params}
                  label="Empresa"
                  autoFocus
                  slotProps={{
                    input: {
                      ...params.InputProps,
                      startAdornment: (
                        <>
                          <BusinessIcon sx={{ color: 'text.secondary', mr: 0.5, fontSize: 20 }} />
                          {params.InputProps.startAdornment}
                        </>
                      ),
                    },
                  }}
                />
              )}
              noOptionsText="Sin resultados"
            />

            {/* Selector de sucursal en cascada */}
            {selectedEmpresa && (
              loadingSucursales ? (
                <Box sx={{ display: 'flex', justifyContent: 'center', py: 1 }}>
                  <CircularProgress size={24} />
                </Box>
              ) : sucursalesDeEmpresa.length > 0 ? (
                <Autocomplete
                  options={sucursalesDeEmpresa}
                  getOptionLabel={(o) => o.nombre}
                  isOptionEqualToValue={(o, v) => o.id === v.id}
                  value={selectedSucursal}
                  onChange={(_, val) => setSelectedSucursal(val)}
                  renderInput={(params) => (
                    <TextField
                      {...params}
                      label="Sucursal"
                      autoFocus={sucursalesDeEmpresa.length > 1}
                      slotProps={{
                        input: {
                          ...params.InputProps,
                          startAdornment: (
                            <>
                              <StoreIcon sx={{ color: 'text.secondary', mr: 0.5, fontSize: 20 }} />
                              {params.InputProps.startAdornment}
                            </>
                          ),
                        },
                      }}
                    />
                  )}
                  noOptionsText="Sin resultados"
                />
              ) : null
            )}
          </Box>
        )}
      </DialogContent>

      <DialogActions sx={{ px: 3, pb: 3 }}>
        <Button
          variant="contained"
          fullWidth
          size="large"
          disabled={!canConfirm || loadingEmpresas}
          onClick={handleConfirm}
        >
          Ingresar
        </Button>
      </DialogActions>
    </Dialog>
  );
}
