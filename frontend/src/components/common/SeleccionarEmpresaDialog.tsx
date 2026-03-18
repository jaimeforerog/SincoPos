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
} from '@mui/material';
import BusinessIcon from '@mui/icons-material/Business';
import { useAuthStore } from '@/stores/auth.store';

export function SeleccionarEmpresaDialog() {
  const { isAuthenticated, isLoading, activeEmpresaId, empresasDisponibles, setActiveEmpresa } = useAuthStore();

  const [selected, setSelected] = useState<{ id: number; nombre: string } | null>(null);

  // Mostrar solo si: autenticado, cargado, sin empresa activa, y hay 2+ empresas
  const open =
    isAuthenticated &&
    !isLoading &&
    activeEmpresaId === undefined &&
    empresasDisponibles.length > 1;

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
            Tu cuenta tiene acceso a múltiples empresas
          </Typography>
        </Box>
      </DialogTitle>

      <DialogContent sx={{ pb: 1 }}>
        <Autocomplete
          options={empresasDisponibles}
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
      </DialogContent>

      <DialogActions sx={{ px: 3, pb: 3 }}>
        <Button
          variant="contained"
          fullWidth
          size="large"
          disabled={!selected}
          onClick={() => { if (selected) setActiveEmpresa(selected.id); }}
        >
          Ingresar
        </Button>
      </DialogActions>
    </Dialog>
  );
}
