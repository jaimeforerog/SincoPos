import { useEffect } from 'react';
import type { ReactNode } from 'react';
import { AuthProvider as OidcAuthProvider, useAuth as useOidcAuth } from 'react-oidc-context';
import type { User } from 'oidc-client-ts';
import { useAuthStore } from '@/stores/auth.store';
import { usuariosApi } from '@/api/usuarios';
import { CircularProgress, Box } from '@mui/material';

const KEYCLOAK_URL = import.meta.env.VITE_KEYCLOAK_URL || 'http://localhost:8080';
const KEYCLOAK_REALM = import.meta.env.VITE_KEYCLOAK_REALM || 'sincopos';
const CLIENT_ID = import.meta.env.VITE_KEYCLOAK_CLIENT_ID || 'sincopos-frontend';

const oidcConfig = {
  authority: `${KEYCLOAK_URL}/realms/${KEYCLOAK_REALM}`,
  client_id: CLIENT_ID,
  redirect_uri: window.location.origin + '/callback',
  response_type: 'code',
  scope: 'openid profile email',
  onSigninCallback: (_user: User | void): void => {
    window.history.replaceState({}, document.title, window.location.pathname);
  },
  automaticSilentRenew: true,
  loadUserInfo: true,
};

function AuthInitializer({ children }: { children: ReactNode }) {
  const oidcAuth = useOidcAuth();
  const { setUser } = useAuthStore();

  useEffect(() => {
    const initAuth = async () => {
      if (oidcAuth.isLoading) {
        return;
      }

      if (oidcAuth.isAuthenticated && oidcAuth.user) {
        // Store token in sessionStorage for axios interceptor
        sessionStorage.setItem('access_token', oidcAuth.user.access_token);

        try {
          // Fetch user info from backend
          const userInfo = await usuariosApi.me();
          setUser(userInfo);
        } catch (error) {
          console.error('Failed to fetch user info from backend:', error);
          // Fallback: decodificar el access token JWT para obtener roles
          // (realm_access está en el access token, no en el ID token/profile)
          const profile = oidcAuth.user.profile;
          let roles: string[] = [];
          try {
            const b64 = oidcAuth.user.access_token.split('.')[1]
              .replace(/-/g, '+').replace(/_/g, '/');
            const payload = JSON.parse(atob(b64)) as Record<string, unknown>;
            const realmAccess = payload['realm_access'] as { roles?: string[] } | undefined;
            roles = realmAccess?.roles?.filter(r =>
              ['admin','supervisor','cajero','vendedor'].includes(r)) ?? [];
          } catch {
            // Si falla el decode, intentar desde el profile (ID token)
            const profileRealmAccess = (profile as Record<string, unknown>)['realm_access'] as { roles?: string[] } | undefined;
            roles = profileRealmAccess?.roles?.filter(r =>
              ['admin','supervisor','cajero','vendedor'].includes(r)) ?? [];
          }
          setUser({
            id: profile.sub ?? 'unknown',
            username: profile.preferred_username ?? profile.email ?? 'unknown',
            email: profile.email ?? '',
            nombre: profile.name ?? profile.preferred_username ?? '',
            roles,
            sucursalId: undefined,
            sucursalNombre: undefined,
          });
        }
      } else {
        setUser(null);
      }
    };

    initAuth();
  }, [oidcAuth.isAuthenticated, oidcAuth.isLoading, oidcAuth.user, setUser]);

  if (oidcAuth.isLoading) {
    return (
      <Box
        display="flex"
        justifyContent="center"
        alignItems="center"
        minHeight="100vh"
      >
        <CircularProgress />
      </Box>
    );
  }

  if (oidcAuth.error) {
    return (
      <Box
        display="flex"
        justifyContent="center"
        alignItems="center"
        minHeight="100vh"
        flexDirection="column"
      >
        <h1>Error de autenticación</h1>
        <p>{oidcAuth.error.message}</p>
      </Box>
    );
  }

  return <>{children}</>;
}

export function AuthProvider({ children }: { children: ReactNode }) {
  return (
    <OidcAuthProvider {...oidcConfig}>
      <AuthInitializer>{children}</AuthInitializer>
    </OidcAuthProvider>
  );
}
