import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth as useWorkosAuth } from '@workos-inc/authkit-react';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Container,
  Typography,
} from '@mui/material';
import { LoginOutlined } from '@mui/icons-material';
import { APP_NAME } from '@/utils/constants';
import { useAuth } from '@/hooks/useAuth';

export function LoginPage() {
  const navigate = useNavigate();
  const { isAuthenticated } = useAuth();
  const { getSignInUrl, isLoading: sdkLoading } = useWorkosAuth();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (isAuthenticated) {
      navigate('/');
    }
  }, [isAuthenticated, navigate]);

  const handleSignIn = async () => {
    setLoading(true);
    setError(null);
    try {
      // Use getSignInUrl instead of signIn() so we can back up the code_verifier
      // before navigating. Some browsers (Edge Strict, Chrome with enhanced privacy)
      // clear sessionStorage when returning from a cross-site redirect.
      const explicitRedirectUri = `${window.location.origin}/callback`;
      void explicitRedirectUri; // configured at AuthKitProvider level
      const url = await getSignInUrl({});
      // The SDK already stored code_verifier in sessionStorage['workos:code-verifier'].
      // Back it up to localStorage so CallbackPage can recover it if sessionStorage is cleared.
      const cv = sessionStorage.getItem('workos:code-verifier');
      if (cv) localStorage.setItem('workos:pkce-cv-backup', cv);
      window.location.assign(url);
    } catch (err: unknown) {
      console.error('[WorkOS] Error al iniciar sesión:', err);
      setError(err instanceof Error ? err.message : 'Error inesperado al iniciar sesión');
      setLoading(false);
    }
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

            {error && (
              <Alert severity="error" sx={{ mb: 2, textAlign: 'left' }}>
                {error}
              </Alert>
            )}

            <Button
              variant="contained"
              size="large"
              fullWidth
              startIcon={
                loading || sdkLoading
                  ? <CircularProgress size={20} color="inherit" />
                  : <LoginOutlined />
              }
              onClick={handleSignIn}
              disabled={loading || sdkLoading}
              sx={{ py: 1.5, fontSize: '1.1rem' }}
            >
              {sdkLoading ? 'Cargando...' : loading ? 'Redirigiendo...' : 'Iniciar Sesión'}
            </Button>

            <Typography variant="body2" color="text.secondary" sx={{ mt: 3 }}>
              Al continuar, serás redirigido para autenticarte de forma segura
            </Typography>
          </CardContent>
        </Card>
      </Container>
    </Box>
  );
}
