import { useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { AuthKitProvider, useAuth as useWorkosAuth } from '@workos-inc/authkit-react';
import { useAuthStore } from '@/stores/auth.store';
import { usuariosApi } from '@/api/usuarios';
import { setWorkosTokenGetter } from '@/api/tokenRef';
import { CircularProgress, Box } from '@mui/material';
import { WORKOS_CLIENT_ID } from './workosConfig';

function LoadingScreen() {
  return (
    <Box display="flex" justifyContent="center" alignItems="center" minHeight="100vh">
      <CircularProgress />
    </Box>
  );
}

function WorkOsAuthInitializer({ children }: { children: ReactNode }) {
  const { user, isLoading, signOut, getAccessToken } = useWorkosAuth();
  const isAuthenticated = !!user;
  const { setUser, setIdpLogout } = useAuthStore();

  // Exponer getAccessToken al interceptor de axios para manejo de token expirado
  useEffect(() => {
    setWorkosTokenGetter(getAccessToken);
  }, [getAccessToken]);
  const backendFetchedRef = useRef(false);
  // Timeout fallback: if WorkOS SDK doesn't initialize within 10s, unblock rendering.
  // IMPORTANT: depend on isLoading so the timer cancels when SDK initializes normally.
  const [initTimeout, setInitTimeout] = useState(false);
  useEffect(() => {
    if (!isLoading) return; // SDK already done — no timer needed
    const t = setTimeout(() => {
      console.error('[WorkOS] SDK no inicializó en 10s. Verifica VITE_WORKOS_CLIENT_ID y la red.');
      setInitTimeout(true);
    }, 10000);
    return () => clearTimeout(t);
  }, [isLoading]);

  // Registrar función de logout del IdP en el store.
  // Construimos la URL de logout de WorkOS usando el 'sid' del JWT almacenado,
  // porque el SDK tiene su propio almacenamiento en memoria que puede estar vacío
  // cuando el intercambio de código lo hizo nuestro backend (y no el SDK directamente).
  useEffect(() => {
    setIdpLogout(() => {
      const returnTo = `${window.location.origin}/login`;
      const token = localStorage.getItem('access_token');
      if (token) {
        try {
          const payload = JSON.parse(
            atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/'))
          );
          if (payload.sid) {
            const url = new URL('https://api.workos.com/user_management/sessions/logout');
            url.searchParams.set('session_id', payload.sid);
            url.searchParams.set('return_to', returnTo);
            window.location.assign(url.toString());
            return;
          }
        } catch { /* token no es un JWT estándar */ }
      }
      // Fallback: el SDK manejó el callback y tiene el token en memoria
      try {
        signOut();
      } catch {
        // SDK sin sesión — navegar directo al login
        window.location.assign(returnTo);
      }
    });
  }, [signOut, setIdpLogout]);

  // Mantener access_token en localStorage para el interceptor de axios
  useEffect(() => {
    if (!isAuthenticated || !user) return;
    getAccessToken().then((token) => {
      if (token) localStorage.setItem('access_token', token);
    });
  }, [isAuthenticated, user?.id, getAccessToken]);

  // Refresh periódico del token cada 4 minutos
  useEffect(() => {
    if (!isAuthenticated) return;
    const interval = setInterval(() => {
      getAccessToken().then((token) => {
        if (token) localStorage.setItem('access_token', token);
      });
    }, 4 * 60 * 1000);
    return () => clearInterval(interval);
  }, [isAuthenticated, getAccessToken]);

  // Cargar perfil de usuario desde el backend
  useEffect(() => {
    const initAuth = async () => {
      if (isLoading) return;

      if (isAuthenticated && user) {
        if (backendFetchedRef.current) return;
        backendFetchedRef.current = true;

        // Asegurarse de que el token esté disponible antes de llamar al backend
        const token = await getAccessToken();
        if (token) localStorage.setItem('access_token', token);

        try {
          const userInfo = await usuariosApi.me();
          setUser(userInfo);
        } catch (error) {
          console.error('[Auth] Backend /me falló, usando datos del token:', error);
          backendFetchedRef.current = false;
          const currentUser = useAuthStore.getState().user;
          if (!currentUser) {
            setUser({
              id: user.id,
              username: user.email,
              email: user.email,
              nombre: [user.firstName, user.lastName].filter(Boolean).join(' ') || user.email,
              roles: [],
              sucursalId: undefined,
              sucursalNombre: undefined,
              sucursalesDisponibles: [],
            });
          }
        }
      } else if (!isLoading) {
        backendFetchedRef.current = false;
        // Only clear if no manually-set token (from our /api/v1/auth/callback flow)
        const hasToken = !!localStorage.getItem('access_token');
        if (!hasToken) {
          setUser(null);
        } else if (!useAuthStore.getState().user) {
          // Token present but no user — validate by calling /me (e.g. after page refresh)
          try {
            const userInfo = await usuariosApi.me();
            setUser(userInfo);
          } catch {
            localStorage.removeItem('access_token');
            setUser(null);
          }
        }
      }
    };

    initAuth();
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAuthenticated, isLoading, user?.id]);

  if (isLoading && !initTimeout) return <LoadingScreen />;

  return <>{children}</>;
}

export function AuthProvider({ children }: { children: ReactNode }) {
  if (!WORKOS_CLIENT_ID) {
    console.error('[WorkOS] VITE_WORKOS_CLIENT_ID no está definido. El login no funcionará.');
  }

  // Redirect URI explícito con /callback para coincidir con lo registrado en WorkOS dashboard.
  // En desarrollo: http://localhost:5173/callback
  // En producción: https://zealous-hill-0a185e00f.1.azurestaticapps.net/callback
  const redirectUri = `${window.location.origin}/callback`;

  return (
    <AuthKitProvider clientId={WORKOS_CLIENT_ID} redirectUri={redirectUri}>
      <WorkOsAuthInitializer>{children}</WorkOsAuthInitializer>
    </AuthKitProvider>
  );
}
