import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth as useOidcAuth } from 'react-oidc-context';
import { Box, CircularProgress, Typography } from '@mui/material';

export function CallbackPage() {
  const oidcAuth = useOidcAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (oidcAuth.isAuthenticated) {
      navigate('/');
    }
  }, [oidcAuth.isAuthenticated, navigate]);

  return (
    <Box
      display="flex"
      flexDirection="column"
      justifyContent="center"
      alignItems="center"
      minHeight="100vh"
      gap={2}
    >
      <CircularProgress size={60} />
      <Typography variant="h6">Iniciando sesión...</Typography>
    </Box>
  );
}
