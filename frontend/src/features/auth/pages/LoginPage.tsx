import { useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth as useOidcAuth } from 'react-oidc-context';
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
import { isEntraId, loginRequest } from '../msalConfig';
import { msalInstance } from '../msalConfig';
import { useAuth } from '@/hooks/useAuth';

export function LoginPage() {
  const navigate = useNavigate();
  const { isAuthenticated } = useAuth();

  useEffect(() => {
    if (isAuthenticated) {
      navigate('/');
    }
  }, [isAuthenticated, navigate]);

  const handleLogin = useCallback(() => {
    if (isEntraId) {
      msalInstance.loginRedirect(loginRequest);
    }
  }, []);

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

            {isEntraId ? (
              <EntraLoginButton onLogin={handleLogin} />
            ) : (
              <KeycloakLoginButton />
            )}

            <Typography variant="body2" color="text.secondary" sx={{ mt: 3 }}>
              {isEntraId
                ? 'Al continuar, serás redirigido a Microsoft para autenticarte'
                : 'Al continuar, serás redirigido a Keycloak para autenticarte'}
            </Typography>
          </CardContent>
        </Card>
      </Container>
    </Box>
  );
}

function EntraLoginButton({ onLogin }: { onLogin: () => void }) {
  return (
    <Button
      variant="contained"
      size="large"
      fullWidth
      startIcon={<LoginOutlined />}
      onClick={onLogin}
      sx={{ py: 1.5, fontSize: '1.1rem' }}
    >
      Iniciar Sesión con Microsoft
    </Button>
  );
}

function KeycloakLoginButton() {
  const oidcAuth = useOidcAuth();

  return (
    <Button
      variant="contained"
      size="large"
      fullWidth
      startIcon={<LoginOutlined />}
      onClick={() => oidcAuth.signinRedirect()}
      sx={{ py: 1.5, fontSize: '1.1rem' }}
    >
      Iniciar Sesión
    </Button>
  );
}
