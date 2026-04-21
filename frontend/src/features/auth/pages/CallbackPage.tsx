import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Box, CircularProgress, Typography, Button, Alert } from '@mui/material';
import axios from 'axios';
import { useAuthStore } from '@/stores/auth.store';
import { usuariosApi } from '@/api/usuarios';
import { setRefreshToken } from '@/api/tokenRef';

// Captured at MODULE LOAD TIME — before React renders, before AuthKitProvider's
// useEffect runs, before the SDK cleans the URL and sessionStorage.
// CallbackPage is NOT lazy-loaded so this runs immediately when App.tsx imports it.
const _searchParams = new URLSearchParams(window.location.search);
const _capturedCode = _searchParams.get('code');
const _capturedError = _searchParams.get('error');
const _capturedErrorDesc = _searchParams.get('error_description');
// Try sessionStorage first; fall back to localStorage backup (for browsers that
// clear sessionStorage on cross-site navigation, e.g. Edge Strict tracking prevention).
const _capturedVerifier =
  sessionStorage.getItem('workos:code-verifier') ??
  localStorage.getItem('workos:pkce-cv-backup');
// Module-level flag — survives React StrictMode unmount+remount cycles.
let _exchanged = false;


export function CallbackPage() {
  const navigate = useNavigate();
  const { setUser } = useAuthStore();
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (_exchanged) return;

    // WorkOS returned an error (e.g. user denied, invalid redirect_uri, etc.)
    if (_capturedError) {
      _exchanged = true;
      const msg = _capturedErrorDesc
        ? `WorkOS error: ${_capturedError} — ${_capturedErrorDesc}`
        : `WorkOS error: ${_capturedError}`;
      console.error('[Callback]', msg);
      setError(msg);
      return;
    }

    const code = _capturedCode;
    const codeVerifier = _capturedVerifier;

    if (!code || !codeVerifier) {
      console.error('[Callback] Faltan parámetros:', { code: !!code, codeVerifier: !!codeVerifier });
      if (!codeVerifier) {
        setError('El code_verifier no se encontró en sessionStorage. Intenta de nuevo desde el botón de login.');
        return;
      }
      navigate('/login', { replace: true });
      return;
    }

    _exchanged = true;

    // Exchange code via backend (server-to-server, no CORS issues)
    const doExchange = async () => {
      try {
        const API_URL = import.meta.env.VITE_API_URL ?? '';
        const { data } = await axios.post<{ accessToken: string; refreshToken?: string }>(`${API_URL}/api/v1/auth/callback`, {
          code,
          codeVerifier,
        });
        // Store token for axios interceptor
        localStorage.setItem('access_token', data.accessToken);
        if (data.refreshToken) {
          setRefreshToken(data.refreshToken);
        }
        // Clean up PKCE backup now that exchange succeeded
        localStorage.removeItem('workos:pkce-cv-backup');

        // Load user profile from backend
        try {
          const userInfo = await usuariosApi.me();
          setUser(userInfo);
        } catch {
          // Decode minimal info from JWT as fallback
          const payload = JSON.parse(
            atob(data.accessToken.split('.')[1].replace(/-/g, '+').replace(/_/g, '/'))
          );
          setUser({
            id: payload.sub ?? '',
            username: payload.email ?? '',
            email: payload.email ?? '',
            nombre:
              [payload.given_name, payload.family_name].filter(Boolean).join(' ') ||
              (payload.email ?? ''),
            roles: [],
            sucursalId: undefined,
            sucursalNombre: undefined,
            sucursalesDisponibles: [],
          });
        }

        navigate('/', { replace: true });
      } catch (err: unknown) {
        console.error('[Callback] Error al intercambiar código:', err);
        if (axios.isAxiosError(err)) {
          const status = err.response?.status;
          const detail = err.response?.data?.message ?? err.response?.data ?? err.message;
          setError(`Error ${status ?? 'de red'}: ${JSON.stringify(detail)}`);
        } else {
          setError(`Error inesperado: ${String(err)}`);
        }
      }
    };

    void doExchange();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (error) {
    return (
      <Box
        display="flex"
        flexDirection="column"
        justifyContent="center"
        alignItems="center"
        minHeight="100vh"
        gap={3}
        p={4}
      >
        <Alert severity="error" sx={{ maxWidth: 500 }}>
          {error}
        </Alert>
        <Button variant="contained" onClick={() => navigate('/login', { replace: true })}>
          Volver al login
        </Button>
      </Box>
    );
  }

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
