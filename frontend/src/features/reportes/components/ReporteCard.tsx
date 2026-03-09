import { useNavigate } from 'react-router-dom';
import {
  Card,
  CardContent,
  CardActions,
  Button,
  Typography,
  Box,
  Avatar,
} from '@mui/material';
import { ChevronRight } from '@mui/icons-material';

export interface ReporteModule {
  id: string;
  title: string;
  description: string;
  icon: React.ReactElement;
  path: string;
  roles?: string[];
}

interface ReporteCardProps {
  module: ReporteModule;
}

export function ReporteCard({ module }: ReporteCardProps) {
  const navigate = useNavigate();

  return (
    <Card
      sx={{
        height: '100%',
        cursor: 'pointer',
        transition: 'all 0.3s',
        '&:hover': {
          transform: 'translateY(-4px)',
          boxShadow: 4,
        },
      }}
      onClick={() => navigate(module.path)}
    >
      <CardContent>
        <Box sx={{ display: 'flex', alignItems: 'flex-start', mb: 2 }}>
          <Avatar
            sx={{
              bgcolor: 'primary.main',
              width: 56,
              height: 56,
              mr: 2,
            }}
          >
            {module.icon}
          </Avatar>
          <Box sx={{ flexGrow: 1 }}>
            <Typography variant="h6" fontWeight={600} gutterBottom>
              {module.title}
            </Typography>
          </Box>
        </Box>
        <Typography variant="body2" color="text.secondary">
          {module.description}
        </Typography>
      </CardContent>
      <CardActions sx={{ justifyContent: 'flex-end' }}>
        <Button
          size="small"
          endIcon={<ChevronRight />}
        >
          Ver Reporte
        </Button>
      </CardActions>
    </Card>
  );
}
