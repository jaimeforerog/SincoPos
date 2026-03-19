import { useNavigate } from 'react-router-dom';
import { Card, CardContent, Typography, Box, alpha } from '@mui/material';
import { ChevronRight } from '@mui/icons-material';

export interface ConfigModule {
  id: string;
  title: string;
  description: string;
  icon: React.ReactElement;
  path: string;
  roles?: string[];
  stat?: string;
  category: 'negocio' | 'catalogo' | 'sistema';
  color?: string;
}

interface ConfigCardProps {
  module: ConfigModule;
  index?: number;
}

export function ConfigCard({ module, index = 0 }: ConfigCardProps) {
  const navigate = useNavigate();
  const color = module.color ?? '#1976d2';

  return (
    <Card
      sx={{
        height: '100%',
        cursor: 'pointer',
        border: '1px solid',
        borderColor: 'divider',
        transition: 'all 0.25s ease',
        animation: 'fadeSlideIn 0.4s ease both',
        animationDelay: `${index * 60}ms`,
        '@keyframes fadeSlideIn': {
          from: { opacity: 0, transform: 'translateY(12px)' },
          to: { opacity: 1, transform: 'translateY(0)' },
        },
        '&:hover': {
          transform: 'translateY(-3px)',
          boxShadow: `0 8px 24px ${alpha(color, 0.2)}`,
          borderColor: alpha(color, 0.4),
        },
      }}
      onClick={() => navigate(module.path)}
    >
      <CardContent sx={{ p: 1.5, '&:last-child': { pb: 1.5 } }}>
        {/* Icono + título + flecha en una fila */}
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
          <Box
            sx={{
              width: 36,
              height: 36,
              borderRadius: 1.5,
              flexShrink: 0,
              bgcolor: alpha(color, 0.1),
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: color,
              transition: 'background-color 0.25s',
              '.MuiCard-root:hover &': { bgcolor: alpha(color, 0.18) },
            }}
          >
            {module.icon}
          </Box>
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <Typography variant="body2" fontWeight={700} sx={{ lineHeight: 1.3 }}>
              {module.title}
            </Typography>
            <Typography variant="caption" color="text.secondary" sx={{ lineHeight: 1.4, display: 'block' }}>
              {module.description}
            </Typography>
          </Box>
          <ChevronRight
            fontSize="small"
            sx={{
              flexShrink: 0,
              color: 'text.disabled',
              transition: 'transform 0.25s, color 0.25s',
              '.MuiCard-root:hover &': { transform: 'translateX(3px)', color: color },
            }}
          />
        </Box>
      </CardContent>
    </Card>
  );
}
