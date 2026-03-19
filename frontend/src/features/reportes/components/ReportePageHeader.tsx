import { Box, Typography, Breadcrumbs, Link, IconButton } from '@mui/material';
import { ArrowBack, Home } from '@mui/icons-material';
import { useNavigate } from 'react-router-dom';

interface BreadcrumbItem {
  label: string;
  path?: string;
}

interface ReportePageHeaderProps {
  title: string;
  subtitle?: string;
  breadcrumbs: BreadcrumbItem[];
  action?: React.ReactNode;
  backPath?: string;
  /** Color de acento del módulo. Por defecto: azul de reportes */
  color?: string;
}

const DEFAULT_COLOR = '#1565c0';

export function ReportePageHeader({
  title,
  subtitle,
  breadcrumbs,
  action,
  backPath = '/reportes',
  color = DEFAULT_COLOR,
}: ReportePageHeaderProps) {
  const navigate = useNavigate();

  return (
    <Box
      sx={{
        background: `linear-gradient(135deg, ${color} 0%, #0d47a1 50%, #01579b 100%)`,
        borderRadius: 3,
        px: { xs: 3, md: 4 },
        py: { xs: 2.5, md: 3 },
        mb: 3,
        mt: 1,
        position: 'relative',
        overflow: 'hidden',
        '&::before': {
          content: '""',
          position: 'absolute',
          top: -60,
          right: -60,
          width: 200,
          height: 200,
          borderRadius: '50%',
          background: 'rgba(255,255,255,0.05)',
        },
        '&::after': {
          content: '""',
          position: 'absolute',
          bottom: -40,
          right: 80,
          width: 120,
          height: 120,
          borderRadius: '50%',
          background: 'rgba(255,255,255,0.05)',
        },
      }}
    >
      {/* Breadcrumbs */}
      <Breadcrumbs
        sx={{
          mb: 1.5,
          '& .MuiBreadcrumbs-separator': { color: 'rgba(255,255,255,0.4)' },
        }}
      >
        <Link
          component="button"
          variant="caption"
          onClick={() => navigate('/')}
          sx={{
            display: 'flex',
            alignItems: 'center',
            gap: 0.4,
            color: 'rgba(255,255,255,0.65)',
            textDecoration: 'none',
            '&:hover': { color: '#fff' },
          }}
        >
          <Home sx={{ fontSize: 14 }} />
          Inicio
        </Link>
        {breadcrumbs.map((crumb, i) => {
          const isLast = i === breadcrumbs.length - 1;
          return isLast ? (
            <Typography key={i} variant="caption" sx={{ color: 'rgba(255,255,255,0.9)' }}>
              {crumb.label}
            </Typography>
          ) : (
            <Link
              key={i}
              component="button"
              variant="caption"
              onClick={() => crumb.path && navigate(crumb.path)}
              sx={{
                color: 'rgba(255,255,255,0.65)',
                textDecoration: 'none',
                '&:hover': { color: '#fff' },
              }}
            >
              {crumb.label}
            </Link>
          );
        })}
      </Breadcrumbs>

      {/* Fila principal: back + title + action */}
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: 2,
          position: 'relative',
          zIndex: 1,
        }}
      >
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
          <IconButton
            size="small"
            onClick={() => navigate(backPath)}
            sx={{
              color: 'rgba(255,255,255,0.8)',
              bgcolor: 'rgba(255,255,255,0.12)',
              '&:hover': { bgcolor: 'rgba(255,255,255,0.22)', color: '#fff' },
            }}
          >
            <ArrowBack fontSize="small" />
          </IconButton>
          <Box>
            <Typography variant="h5" fontWeight={700} sx={{ color: '#fff', lineHeight: 1.2 }}>
              {title}
            </Typography>
            {subtitle && (
              <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.75)', mt: 0.3 }}>
                {subtitle}
              </Typography>
            )}
          </Box>
        </Box>

        {action && (
          <Box sx={{ flexShrink: 0 }}>
            {action}
          </Box>
        )}
      </Box>
    </Box>
  );
}
