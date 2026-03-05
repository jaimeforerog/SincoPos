import { useEffect } from 'react';
import { useAuth as useOidcAuth } from 'react-oidc-context';
import { useNavigate } from 'react-router-dom';
import {
  Box,
  Button,
  Card,
  CardContent,
  Container,
  Typography,
} from '@mui/material';
import { LoginOutlined } from '@mui/icons-material';
import { APP_NAME } from '@/utils/constants';

export function LoginPage() {
  const oidcAuth = useOidcAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (oidcAuth.isAuthenticated) {
      navigate('/');
    }
  }, [oidcAuth.isAuthenticated, navigate]);

  const handleLogin = () => {
    oidcAuth.signinRedirect();
  };

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
        <Card elevation={8}>
          <CardContent sx={{ p: 4, textAlign: 'center' }}>
            <Typography variant="h3" component="h1" gutterBottom fontWeight={600}>
              {APP_NAME}
            </Typography>
            <Typography
              variant="h6"
              color="text.secondary"
              gutterBottom
              sx={{ mb: 4 }}
            >
              Sistema de Punto de Venta
            </Typography>

            <Button
              variant="contained"
              size="large"
              fullWidth
              startIcon={<LoginOutlined />}
              onClick={handleLogin}
              sx={{
                py: 1.5,
                fontSize: '1.1rem',
              }}
            >
              Iniciar Sesión
            </Button>

            <Typography variant="body2" color="text.secondary" sx={{ mt: 3 }}>
              Al continuar, serás redirigido a Keycloak para autenticarte
            </Typography>
          </CardContent>
        </Card>
      </Container>
    </Box>
  );
}
