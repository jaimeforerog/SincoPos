import { useQuery } from '@tanstack/react-query';
import {
  Box,
  Autocomplete,
  TextField,
  Chip,
} from '@mui/material';
import { tercerosApi } from '@/api/terceros';
import { useTurnContextStore } from '@/stores/turnContext.store';
import { useAuth } from '@/hooks/useAuth';
import type { TerceroDTO } from '@/types/api';

interface CartHeaderProps {
  selectedClienteId: number | null;
  onClienteChange: (clienteId: number | null) => void;
}

export function CartHeader({
  selectedClienteId,
  onClienteChange,
}: CartHeaderProps) {
  const clientesRecientes = useTurnContextStore((s) => s.clientesRecientes);
  const { activeEmpresaId } = useAuth();

  const { data: clientesData } = useQuery({
    queryKey: ['terceros', 'clientes', activeEmpresaId],
    queryFn: () => tercerosApi.getAll({ esCliente: true, activo: true }),
  });

  const clientes = clientesData?.items ?? [];

  // IDs de clientes recientes para ordenar primero
  const recentIds = new Set(clientesRecientes.map((c) => c.id));

  // Recientes al principio, resto en orden alfabético
  const clientesOrdenados: TerceroDTO[] = [
    ...clientes.filter((c) => recentIds.has(c.id)),
    ...clientes.filter((c) => !recentIds.has(c.id)),
  ];

  const selectedCliente = clientes.find((c) => c.id === selectedClienteId);

  return (
    <Box sx={{ mb: 1.5 }}>
      <Autocomplete
        options={clientesOrdenados}
        size="small"
        fullWidth
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
