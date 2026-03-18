import { Box, Typography } from '@mui/material';
import { sincoColors } from '@/theme/tokens';

interface HeroBannerProps {
  title:     string;
  subtitle?: string;
  variant?:  'blue' | 'success' | 'warning';
  info?:     React.ReactNode;
  actions?:  React.ReactNode;
}

export function HeroBanner({
  title,
  subtitle,
  variant = 'blue',
  info,
  actions,
}: HeroBannerProps) {
  const gradient = {
    blue:    sincoColors.gradients.heroBlue,
    success: sincoColors.gradients.heroSuccess,
    warning: sincoColors.gradients.heroWarning,
  }[variant];

  return (
    <Box
      sx={{
        background: gradient,
        borderRadius: '16px',
        p: 2,
        mb: 3,
        position: 'relative',
        overflow: 'hidden',
        '&::before': {
          content: '""',
          position: 'absolute',
          top: -40, right: -40,
          width: 140, height: 140,
          borderRadius: '50%',
          background: 'rgba(255,255,255,0.06)',
        },
        '&::after': {
          content: '""',
          position: 'absolute',
          bottom: -30, left: 60,
          width: 100, height: 100,
          borderRadius: '50%',
          background: 'rgba(255,255,255,0.04)',
        },
      }}
    >
      <Box
        sx={{
          display: 'flex',
          gap: 3,
          alignItems: 'center',
          flexWrap: 'wrap',
          justifyContent: 'space-between',
          position: 'relative',
          zIndex: 1,
        }}
      >
        {/* Título */}
        <Box>
          <Typography variant="h6" fontWeight={700} sx={{ color: '#fff', lineHeight: 1.1 }}>
            {title}
          </Typography>
          {subtitle && (
            <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.7)' }}>
              {subtitle}
            </Typography>
          )}
        </Box>

        {/* Info central — sucursal, caja, cajero, etc. */}
        {info && (
          <Box sx={{ display: 'flex', gap: 3, alignItems: 'center', flexWrap: 'wrap' }}>
            {info}
          </Box>
        )}

        {/* Acciones — botones, chips */}
        {actions && (
          <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
            {actions}
          </Box>
        )}
      </Box>
    </Box>
  );
}
