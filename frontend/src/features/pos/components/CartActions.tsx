import { Box, Button, CircularProgress } from '@mui/material';
import ShoppingCartIcon from '@mui/icons-material/ShoppingCart';
import DeleteSweepIcon from '@mui/icons-material/DeleteSweep';

interface CartActionsProps {
  onClear: () => void;
  onCobrar: () => void;
  canCobrar: boolean;
  isLoading: boolean;
  isOffline?: boolean;
}

export function CartActions({
  onClear,
  onCobrar,
  canCobrar,
  isLoading,
  isOffline = false,
}: CartActionsProps) {
  return (
    <Box sx={{ display: 'flex', gap: 2 }}>
      <Button
        variant="outlined"
        color="error"
        startIcon={<DeleteSweepIcon />}
        onClick={onClear}
        disabled={isLoading}
        fullWidth
      >
        Limpiar
      </Button>
      <Button
        variant="contained"
        color="primary"
        size="large"
        startIcon={
          isLoading ? <CircularProgress size={20} color="inherit" /> : <ShoppingCartIcon />
        }
        onClick={onCobrar}
        disabled={!canCobrar || isLoading}
        fullWidth
        sx={{
          py: 1.5,
          fontSize: '1.1rem',
          fontWeight: 700,
        }}
      >
        {isLoading ? 'Procesando...' : isOffline ? 'GUARDAR OFFLINE' : 'COBRAR'}
      </Button>
    </Box>
  );
}
