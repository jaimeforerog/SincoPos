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
  Chip,
} from '@mui/material';
import { useCajasAbiertas } from '../hooks/useCajasAbiertas';
import { tercerosApi } from '@/api/terceros';
import { useTurnContextStore } from '@/stores/turnContext.store';
import { useAuth } from '@/hooks/useAuth';
import type { TerceroDTO } from '@/types/api';

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
  const clientesRecientes = useTurnContextStore((s) => s.clientesRecientes);
  const { activeEmpresaId } = useAuth();

  const { data: clientesData } = useQuery({
    queryKey: ['terceros', 'clientes', activeEmpresaId],
    queryFn: () => tercerosApi.getAll({ esCliente: true, activo: true }),
  });

  const clientes = clientesData?.items ?? [];

  // Capa 3: IDs de clientes recientes para ordenar primero
  const recentIds = new Set(clientesRecientes.map((c) => c.id));

  // Recientes al principio, resto en orden alfabético
  const clientesOrdenados: TerceroDTO[] = [
    ...clientes.filter((c) => recentIds.has(c.id)),
    ...clientes.filter((c) => !recentIds.has(c.id)),
  ];

  if (loadingCajas) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', p: 2 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (cajas.length === 0 && !selectedCajaId) {
    return (
      <Alert severity="error" sx={{ mb: 2 }}>
        No tienes cajas abiertas. Debes abrir una caja antes de realizar ventas.
      </Alert>
    );
  }

  const selectedCliente = clientes.find((c) => c.id === selectedClienteId);

  return (
    <Box sx={{ display: 'flex', gap: 1, mb: 1.5 }}>
      <FormControl size="small" sx={{ flex: '0 0 140px' }}>
        <InputLabel>Caja</InputLabel>
        <Select
          value={selectedCajaId ?? ''}
          onChange={(e) => onCajaChange(Number(e.target.value) || null)}
          label="Caja"
        >
          {cajas.map((caja) => (
            <MenuItem key={caja.id} value={caja.id}>
              {caja.nombre}
            </MenuItem>
          ))}
        </Select>
      </FormControl>

      <Autocomplete
        options={clientesOrdenados}
        size="small"
        sx={{ flex: 1, minWidth: 0 }}
        getOptionLabel={(option) =>
          typeof option === 'string' ? option : option.nombre
        }
        groupBy={(option) =>
          recentIds.has(option.id) ? 'Recientes' : 'Todos los clientes'
        }
        value={selectedCliente ?? null}
        onChange={(_, newValue) => onClienteChange(newValue ? newValue.id : null)}
        renderOption={(props, option) => (
          <li {...props} key={option.id}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, width: '100%' }}>
              <span>{option.nombre}</span>
              {recentIds.has(option.id) && (
                <Chip label="reciente" size="small" sx={{ height: 18, fontSize: '0.65rem' }} />
              )}
            </Box>
          </li>
        )}
        renderInput={(params) => (
          <TextField {...params} label="Cliente" placeholder="Buscar..." />
        )}
      />
    </Box>
  );
}
