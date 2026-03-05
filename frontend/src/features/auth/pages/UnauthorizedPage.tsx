import { useNavigate } from 'react-router-dom';
import { Box, Button, Container, Typography } from '@mui/material';
import { LockOutlined, HomeOutlined, ArrowBack } from '@mui/icons-material';
import { useAuth } from '@/hooks/useAuth';

export function UnauthorizedPage() {
  const navigate = useNavigate();
  const { user } = useAuth();

  return (
    <Box
      sx={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
      }}
    >
      <Container maxWidth="sm">
        <Box
          sx={{
            bgcolor: 'background.paper',
            borderRadius: 3,
            p: 6,
            textAlign: 'center',
            boxShadow: 8,
          }}
        >
          <LockOutlined sx={{ fontSize: 72, color: 'warning.main', mb: 2 }} />

          <Typography variant="h4" fontWeight={700} gutterBottom>
            Acceso restringido
          </Typography>

          <Typography variant="body1" color="text.secondary" sx={{ mb: 1 }}>
            No tienes permisos para ver esta página.
          </Typography>

          {user && (
            <Typography variant="body2" color="text.secondary" sx={{ mb: 4 }}>
              Tu rol actual es{' '}
              <strong>{user.roles?.[0] ?? 'sin rol asignado'}</strong>.
              Contacta al administrador si necesitas acceso.
            </Typography>
          )}

          <Box sx={{ display: 'flex', gap: 2, justifyContent: 'center', mt: 4 }}>
            <Button
              variant="outlined"
              startIcon={<ArrowBack />}
              onClick={() => navigate(-1)}
            >
              Volver
            </Button>
            <Button
              variant="contained"
              startIcon={<HomeOutlined />}
              onClick={() => navigate('/dashboard')}
            >
              Ir al Dashboard
            </Button>
          </Box>
        </Box>
      </Container>
    </Box>
  );
}
