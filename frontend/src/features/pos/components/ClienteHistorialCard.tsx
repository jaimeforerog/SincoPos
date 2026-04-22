import { useQuery } from '@tanstack/react-query';
import {
  Box,
  Typography,
  Chip,
  Skeleton,
  Tooltip,
} from '@mui/material';
import HistoryIcon    from '@mui/icons-material/History';
import ShoppingBagIcon from '@mui/icons-material/ShoppingBag';
import { tercerosApi } from '@/api/terceros';
import { sincoColors }  from '@/theme/tokens';

interface ClienteHistorialCardProps {
  clienteId: number;
}

const formatCurrency = (v: number) =>
  new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', minimumFractionDigits: 0 }).format(v);

/**
 * Capa 4 — Dependencias inteligentes.
 * Muestra el historial de compras acumulado del cliente seleccionado.
 * Alimentado automáticamente por ClienteHistorialProjection en cada venta.
 */
export function ClienteHistorialCard({ clienteId }: ClienteHistorialCardProps) {
  const { data: historial, isLoading } = useQuery({
    queryKey: ['cliente-historial', clienteId],
    queryFn:  () => tercerosApi.getHistorial(clienteId),
    staleTime: 60_000,
    enabled:  clienteId > 0,
  });

  if (isLoading) {
    return (
      <Box sx={{ mt: 1, px: 0.5 }}>
        <Skeleton variant="rectangular" height={40} sx={{ borderRadius: 1 }} />
      </Box>
    );
  }

  if (!historial || historial.totalCompras === 0) return null;

  return (
    <Box
      sx={{
        mt: 1,
        p: 1,
        bgcolor: sincoColors.surface.subtle,
        borderRadius: '8px',
        borderLeft: `3px solid ${sincoColors.brand[500]}`,
      }}
    >
      {/* Resumen rápido */}
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mb: 0.75 }}>
        <HistoryIcon sx={{ fontSize: 13, color: sincoColors.brand[600] }} />
        <Typography variant="caption" fontWeight={600} color={sincoColors.brand[700]}>
          Historial del cliente
        </Typography>
      </Box>

      <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
        <Chip
          label={`${historial.totalCompras} compra${historial.totalCompras !== 1 ? 's' : ''}`}
          size="small"
          sx={{ height: 18, fontSize: '0.65rem', bgcolor: sincoColors.brand[50], color: sincoColors.brand[800] }}
        />
        <Chip
          label={`Prom. ${formatCurrency(historial.gastoPromedio)}`}
          size="small"
          sx={{ height: 18, fontSize: '0.65rem', bgcolor: sincoColors.surface.subtle }}
        />
      </Box>

      {/* Top productos comprados */}
      {historial.topProductos.length > 0 && (
        <Box sx={{ mt: 0.75 }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mb: 0.5 }}>
            <ShoppingBagIcon sx={{ fontSize: 11, color: 'text.disabled' }} />
            <Typography variant="caption" color="text.disabled">
              Suele comprar:
            </Typography>
          </Box>
          <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
            {historial.topProductos.slice(0, 4).map((p) => (
              <Tooltip key={p.productoId} title={`×${p.cantidadTotal} unidades`}>
                <Chip
                  label={p.nombreProducto}
                  size="small"
                  sx={{
                    height: 18,
                    fontSize: '0.63rem',
                    maxWidth: 120,
                    bgcolor: 'background.paper',
                    border: `1px solid ${sincoColors.brand[100]}`,
                    '& .MuiChip-label': { overflow: 'hidden', textOverflow: 'ellipsis' },
                  }}
                />
              </Tooltip>
            ))}
          </Box>
        </Box>
      )}
    </Box>
  );
}
