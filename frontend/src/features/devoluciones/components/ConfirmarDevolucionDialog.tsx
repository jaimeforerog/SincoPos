import {
  Box,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Button,
  Typography,
  Alert,
  CircularProgress,
} from '@mui/material';
import { formatCurrency } from '@/utils/format';

interface ConfirmarDevolucionDialogProps {
  open: boolean;
  onClose: () => void;
  motivo: string;
  onMotivoChange: (value: string) => void;
  totalDevolucion: number;
  loading: boolean;
  onConfirm: () => void;
}

export function ConfirmarDevolucionDialog({
  open,
  onClose,
  motivo,
  onMotivoChange,
  totalDevolucion,
  loading,
  onConfirm,
}: ConfirmarDevolucionDialogProps) {
  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Confirmar Devolución</DialogTitle>
      <DialogContent>
        <Alert severity="warning" sx={{ mb: 2 }}>
          Esta acción devolverá los productos al inventario y ajustará el monto de caja.
        </Alert>
        <TextField
          label="Motivo de la devolución"
          multiline
          rows={3}
          value={motivo}
          onChange={(e) => onMotivoChange(e.target.value)}
          fullWidth
          required
          placeholder="Ej: Producto defectuoso, Error en la venta, etc."
        />
        <Box sx={{ mt: 2 }}>
          <Typography variant="h6">
            Total a devolver: {formatCurrency(totalDevolucion)}
          </Typography>
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={loading}>
          Cancelar
        </Button>
        <Button
          onClick={onConfirm}
          variant="contained"
          color="error"
          disabled={loading || !motivo.trim()}
        >
          {loading ? <CircularProgress size={24} /> : 'Confirmar Devolución'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
