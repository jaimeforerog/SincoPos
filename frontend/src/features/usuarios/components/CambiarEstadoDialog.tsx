import { useState } from 'react';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Alert,
  TextField,
  Button,
  CircularProgress,
} from '@mui/material';
import type { UsuarioDto } from '@/api/usuarios';

interface CambiarEstadoDialogProps {
  usuario: UsuarioDto | null;
  onClose: () => void;
  onConfirm: (motivo: string) => void;
  loading: boolean;
}

export function CambiarEstadoDialog({ usuario, onClose, onConfirm, loading }: CambiarEstadoDialogProps) {
  const [motivo, setMotivo] = useState('');

  if (!usuario) return null;
  const accion = usuario.activo ? 'desactivar' : 'activar';

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ textTransform: 'capitalize' }}>{accion} usuario</DialogTitle>
      <DialogContent>
        <Alert severity={usuario.activo ? 'warning' : 'info'} sx={{ mb: 2 }}>
          ¿Seguro que deseas {accion} a <strong>{usuario.nombreCompleto}</strong>?
        </Alert>
        <TextField
          fullWidth
          label="Motivo (opcional)"
          value={motivo}
          onChange={(e) => setMotivo(e.target.value)}
          multiline
          rows={2}
        />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={loading}>Cancelar</Button>
        <Button
          variant="contained"
          color={usuario.activo ? 'error' : 'success'}
          onClick={() => onConfirm(motivo)}
          disabled={loading}
        >
          {loading ? <CircularProgress size={20} /> : 'Confirmar'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
