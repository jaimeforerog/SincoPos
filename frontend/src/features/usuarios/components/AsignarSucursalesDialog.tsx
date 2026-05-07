import { useState } from 'react';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Typography,
  Button,
  CircularProgress,
  Checkbox,
  FormControlLabel,
  FormGroup,
  Box,
  Chip,
} from '@mui/material';
import type { UsuarioDto } from '@/api/usuarios';
import type { SucursalDTO } from '@/types/api';

interface AsignarSucursalesDialogProps {
  usuario: UsuarioDto | null;
  sucursales: SucursalDTO[];
  onClose: () => void;
  onConfirm: (sucursalIds: number[]) => void;
  loading: boolean;
}

export function AsignarSucursalesDialog({ usuario, sucursales, onClose, onConfirm, loading }: AsignarSucursalesDialogProps) {
  const [selected, setSelected] = useState<Set<number>>(
    () => new Set(usuario?.sucursalesAsignadas?.map(s => s.id) ?? [])
  );

  if (!usuario) return null;

  const toggle = (id: number) => {
    setSelected(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Sucursales asignadas</DialogTitle>
      <DialogContent>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Usuario: <strong>{usuario.nombreCompleto}</strong> ({usuario.email})
        </Typography>
        <FormGroup>
          {sucursales.map((s) => (
            <FormControlLabel
              key={s.id}
              control={
                <Checkbox
                  checked={selected.has(s.id)}
                  onChange={() => toggle(s.id)}
                />
              }
              label={
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                  {s.nombre}
                  {s.id === usuario.sucursalDefaultId && (
                    <Chip label="default" size="small" variant="outlined" />
                  )}
                </Box>
              }
            />
          ))}
        </FormGroup>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={loading}>Cancelar</Button>
        <Button
          variant="contained"
          onClick={() => onConfirm(Array.from(selected))}
          disabled={loading}
        >
          {loading ? <CircularProgress size={20} /> : 'Guardar'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
