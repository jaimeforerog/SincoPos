import { Button, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle } from '@mui/material';

interface ConfirmDialogProps {
  open: boolean;
  mensaje: string;
  titulo?: string;
  onAceptar: () => void;
  onCancelar: () => void;
}

export function ConfirmDialog({ open, mensaje, titulo = 'Confirmar', onAceptar, onCancelar }: ConfirmDialogProps) {
  return (
    <Dialog open={open} onClose={onCancelar} maxWidth="xs" fullWidth>
      <DialogTitle>{titulo}</DialogTitle>
      <DialogContent>
        <DialogContentText>{mensaje}</DialogContentText>
      </DialogContent>
      <DialogActions>
        <Button onClick={onCancelar}>Cancelar</Button>
        <Button onClick={onAceptar} variant="contained" color="error" autoFocus>
          Aceptar
        </Button>
      </DialogActions>
    </Dialog>
  );
}
