import { useState } from 'react';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Typography,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Button,
  CircularProgress,
} from '@mui/material';
import type { UsuarioDto } from '@/api/usuarios';
import type { SucursalDTO } from '@/types/api';

interface AsignarSucursalDialogProps {
  usuario: UsuarioDto | null;
  sucursales: SucursalDTO[];
  onClose: () => void;
  onConfirm: (sucursalId: number) => void;
  loading: boolean;
}

export function AsignarSucursalDialog({ usuario, sucursales, onClose, onConfirm, loading }: AsignarSucursalDialogProps) {
  const [sucursalId, setSucursalId] = useState<number | ''>(usuario?.sucursalDefaultId ?? '');

  if (!usuario) return null;

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Asignar Sucursal Default</DialogTitle>
      <DialogContent>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Usuario: <strong>{usuario.nombreCompleto}</strong> ({usuario.email})
        </Typography>
        <FormControl fullWidth>
          <InputLabel id="sucursal-label">Sucursal</InputLabel>
          <Select
            labelId="sucursal-label"
            label="Sucursal"
            value={sucursalId}
            onChange={(e) => setSucursalId(e.target.value as number)}
          >
            {sucursales.map((s) => (
              <MenuItem key={s.id} value={s.id}>
                {s.nombre}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={loading}>Cancelar</Button>
        <Button
          variant="contained"
          onClick={() => sucursalId !== '' && onConfirm(sucursalId as number)}
          disabled={sucursalId === '' || loading}
        >
          {loading ? <CircularProgress size={20} /> : 'Guardar'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
