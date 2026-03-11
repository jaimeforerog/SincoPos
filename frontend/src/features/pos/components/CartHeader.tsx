import { useQuery } from '@tanstack/react-query';
import {
  Box,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Autocomplete,
  TextField,
  Alert,
  CircularProgress,
} from '@mui/material';
import { useCajasAbiertas } from '../hooks/useCajasAbiertas';
import { tercerosApi } from '@/api/terceros';

interface CartHeaderProps {
  selectedCajaId: number | null;
  selectedClienteId: number | null;
  onCajaChange: (cajaId: number | null) => void;
  onClienteChange: (clienteId: number | null) => void;
}

export function CartHeader({
  selectedCajaId,
  selectedClienteId,
  onCajaChange,
  onClienteChange,
}: CartHeaderProps) {
  const { data: cajas = [], isLoading: loadingCajas } = useCajasAbiertas();

  const { data: clientesData } = useQuery({
    queryKey: ['terceros', 'clientes'],
    queryFn: () => tercerosApi.getAll({ esCliente: true, activo: true }),
  });

  const clientes = clientesData?.items || [];

  if (loadingCajas) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', p: 2 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (cajas.length === 0) {
    return (
      <Alert severity="error" sx={{ mb: 2 }}>
        No tienes cajas abiertas. Debes abrir una caja antes de realizar ventas.
      </Alert>
    );
  }

  const selectedCliente = clientes.find((c) => c.id === selectedClienteId);

  return (
    <Box sx={{ mb: 2 }}>
      <FormControl fullWidth sx={{ mb: 2 }}>
        <InputLabel>Caja</InputLabel>
        <Select
          value={selectedCajaId ?? ''}
          onChange={(e) => onCajaChange(Number(e.target.value) || null)}
          label="Caja"
        >
          {cajas.map((caja) => (
            <MenuItem key={caja.id} value={caja.id}>
              {caja.nombre} - {caja.nombreSucursal}
            </MenuItem>
          ))}
        </Select>
      </FormControl>

      <Autocomplete
        options={clientes}
        getOptionLabel={(option) => {
          if (!option) return '';
          return typeof option === 'string' ? option : option.nombre;
        }}
        value={selectedCliente || null}
        onChange={(_, newValue) => {
          onClienteChange(newValue ? newValue.id : null);
        }}
        renderInput={(params) => (
          <TextField {...params} label="Cliente (opcional)" placeholder="Buscar cliente..." />
        )}
      />
    </Box>
  );
}
