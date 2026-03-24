import { useState } from 'react';
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
import { useQuery } from '@tanstack/react-query';
import { useAuthStore } from '@/stores/auth.store';
import { empresasApi } from '@/api/empresas';

export function SeleccionarEmpresaDialog() {
  const { isAuthenticated, isLoading, activeEmpresaId, setActiveEmpresa } = useAuthStore();

  const [selected, setSelected] = useState<{ id: number; nombre: string } | null>(null);

  // Consultar empresas directamente al backend cuando el usuario está autenticado.
  // Esto es más robusto que depender del store, que puede quedar vacío si /me falla.
  const { data: empresas = [], isLoading: loadingEmpresas } = useQuery({
    queryKey: ['empresas-selector'],
    queryFn: () => empresasApi.getAll().then(list => list.map(e => ({ id: e.id, nombre: e.nombre }))),
    enabled: isAuthenticated && !isLoading && activeEmpresaId === undefined,
    staleTime: 60_000,
  });

  // Mostrar si: autenticado, cargado, sin empresa activa, y hay al menos 1 empresa
  const open =
    isAuthenticated &&
    !isLoading &&
    activeEmpresaId === undefined;

  if (!open) return null;

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
            Seleccionar Empresa
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Selecciona la empresa con la que deseas trabajar
          </Typography>
        </Box>
      </DialogTitle>

      <DialogContent sx={{ pb: 1 }}>
        {loadingEmpresas ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 3 }}>
            <CircularProgress />
          </Box>
        ) : (
          <Autocomplete
            options={empresas}
            getOptionLabel={(o) => o.nombre}
            isOptionEqualToValue={(o, v) => o.id === v.id}
            value={selected}
            onChange={(_, val) => setSelected(val)}
            renderInput={(params) => (
              <TextField {...params} label="Empresa" autoFocus />
            )}
            noOptionsText="Sin resultados"
            sx={{ mt: 1 }}
          />
        )}
      </DialogContent>

      <DialogActions sx={{ px: 3, pb: 3 }}>
        <Button
          variant="contained"
          fullWidth
          size="large"
          disabled={!selected || loadingEmpresas}
          onClick={() => { if (selected) setActiveEmpresa(selected.id, selected.nombre); }}
        >
          Ingresar
        </Button>
      </DialogActions>
    </Dialog>
  );
}
