import { Box, Typography, Chip, IconButton } from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import { formatDateOnly } from '@/utils/format';
import type { OrdenCompraDTO } from '@/types/api';

const HERO_COLOR = '#1565c0';

const ESTADO_META: Record<string, { color: 'warning' | 'info' | 'primary' | 'success' | 'error' | 'default'; label: string }> = {
  Pendiente:        { color: 'warning', label: 'Pendiente' },
  Aprobada:         { color: 'info',    label: 'Aprobada' },
  RecibidaParcial:  { color: 'primary', label: 'Rec. Parcial' },
  RecibidaCompleta: { color: 'success', label: 'Recibida Completa' },
  Rechazada:        { color: 'error',   label: 'Rechazada' },
  Cancelada:        { color: 'default', label: 'Cancelada' },
};

interface OrdenCompraDetalleHeroProps {
  orden: OrdenCompraDTO;
  onBack: () => void;
}

export function OrdenCompraDetalleHero({ orden, onBack }: OrdenCompraDetalleHeroProps) {
  const estadoMeta = ESTADO_META[orden.estado] ?? { color: 'default' as const, label: orden.estado };

  return (
    <Box
      sx={{
        background: `linear-gradient(135deg, ${HERO_COLOR} 0%, #0d47a1 50%, #01579b 100%)`,
        px: { xs: 3, md: 4 },
        py: { xs: 1.5, md: 2 },
        mb: 3,
        position: 'relative',
        overflow: 'hidden',
        '&::before': {
          content: '""', position: 'absolute', top: -60, right: -60,
          width: 200, height: 200, borderRadius: '50%', background: 'rgba(255,255,255,0.05)',
        },
        '&::after': {
          content: '""', position: 'absolute', bottom: -40, right: 80,
          width: 120, height: 120, borderRadius: '50%', background: 'rgba(255,255,255,0.05)',
        },
      }}
    >
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          position: 'relative',
          zIndex: 1,
          flexWrap: 'wrap',
          gap: 1,
        }}
      >
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
          <IconButton
            aria-label="regresar"
            onClick={onBack}
            sx={{
              color: '#fff',
              bgcolor: 'rgba(255,255,255,0.12)',
              '&:hover': { bgcolor: 'rgba(255,255,255,0.22)' },
            }}
          >
            <ArrowBackIcon />
          </IconButton>
          <Typography
            variant="h5"
            fontWeight={700}
            sx={{ color: '#fff', fontFamily: 'monospace', letterSpacing: '0.05em' }}
          >
            {orden.numeroOrden}
          </Typography>
          <Chip
            label={estadoMeta.label}
            color={estadoMeta.color}
            size="small"
            sx={{ fontWeight: 700, bgcolor: 'rgba(255,255,255,0.18)', color: '#fff', border: '1px solid rgba(255,255,255,0.3)' }}
          />
        </Box>
        <Box sx={{ textAlign: { xs: 'left', md: 'right' } }}>
          <Typography variant="body1" fontWeight={600} sx={{ color: '#fff' }}>
            {orden.nombreProveedor}
          </Typography>
          <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.75)' }}>
            {formatDateOnly(orden.fechaOrden)}
          </Typography>
        </Box>
      </Box>
    </Box>
  );
}
