import { useEffect, useCallback } from 'react';
import type { ReactNode } from 'react';
import { InteractionStatus } from '@azure/msal-browser';
import type { AccountInfo } from '@azure/msal-browser';
import { MsalProvider, useMsal, useIsAuthenticated } from '@azure/msal-react';
import { AuthProvider as OidcAuthProvider, useAuth as useOidcAuth } from 'react-oidc-context';
import { useAuthStore } from '@/stores/auth.store';
import { usuariosApi } from '@/api/usuarios';
import { CircularProgress, Box } from '@mui/material';
import { loginRequest, keycloakConfig, isEntraId, msalInstance, msalInitPromise } from './msalConfig';

// ── Entra ID Auth Initializer ──────────────────────────────────────────────
function EntraAuthInitializer({ children }: { children: ReactNode }) {
  const { instance, inProgress } = useMsal();
  const isAuthenticated = useIsAuthenticated();
  const { setUser, setIdpLogout } = useAuthStore();

  // Register IdP logout function
  useEffect(() => {
    setIdpLogout(() => {
      instance.logoutRedirect({ postLogoutRedirectUri: window.location.origin });
    });
  }, [instance, setIdpLogout]);

  const acquireToken = useCallback(async (): Promise<string | null> => {
    const account = instance.getActiveAccount();
    if (!account) return null;
    try {
      const response = await instance.acquireTokenSilent({
        ...loginRequest,
        account,
      });
      return response.accessToken;
    } catch {
      // Silent token acquisition failed — interaction required
      return null;
    }
  }, [instance]);

  useEffect(() => {
    const initAuth = async () => {
      if (inProgress !== InteractionStatus.None) return;

      // Ensure MSAL is fully initialized before acquiring tokens
      await msalInitPromise;

      if (isAuthenticated) {
        const token = await acquireToken();
        if (token) {
          sessionStorage.setItem('access_token', token);

          try {
            const userInfo = await usuariosApi.me();
            setUser(userInfo);
          } catch (error) {
            console.error('Failed to fetch user info from backend:', error);
            // Backend unavailable or user not provisioned yet — use token claims as fallback
            const account = instance.getActiveAccount() as AccountInfo;
            const idTokenClaims = account.idTokenClaims as Record<string, unknown> | undefined;
            const roles = (idTokenClaims?.['roles'] as string[] | undefined)
              ?.filter(r => ['admin', 'supervisor', 'cajero', 'vendedor'].includes(r)) ?? [];

            setUser({
              id: account.localAccountId,
              username: account.username,
              email: account.username,
              nombre: account.name ?? account.username,
              roles,
              sucursalId: undefined,
              sucursalNombre: undefined,
              sucursalesDisponibles: [],
            });
          }
        } else {
          setUser(null);
        }
      } else {
        setUser(null);
      }
    };

    initAuth();
  }, [isAuthenticated, inProgress, acquireToken, setUser, instance]);

  // Set up token refresh — keep sessionStorage in sync
  useEffect(() => {
    if (!isAuthenticated) return;

    const interval = setInterval(async () => {
      const token = await acquireToken();
      if (token) {
        sessionStorage.setItem('access_token', token);
      }
    }, 4 * 60 * 1000); // Refresh every 4 minutes

    return () => clearInterval(interval);
  }, [isAuthenticated, acquireToken]);

  if (inProgress !== InteractionStatus.None) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight="100vh">
        <CircularProgress />
      </Box>
    );
  }

  return <>{children}</>;
}

// ── Keycloak Auth Initializer (desarrollo local) ───────────────────────────
function KeycloakAuthInitializer({ children }: { children: ReactNode }) {
  const oidcAuth = useOidcAuth();
  const { setUser, setIdpLogout } = useAuthStore();

  // Register IdP logout function
  useEffect(() => {
    setIdpLogout(() => {
      oidcAuth.signoutRedirect({ post_logout_redirect_uri: window.location.origin });
    });
  }, [oidcAuth, setIdpLogout]);

  useEffect(() => {
    const initAuth = async () => {
      if (oidcAuth.isLoading) return;

      if (oidcAuth.isAuthenticated && oidcAuth.user) {
        sessionStorage.setItem('access_token', oidcAuth.user.access_token);

        try {
          const userInfo = await usuariosApi.me();
          setUser(userInfo);
        } catch (error) {
          console.error('Failed to fetch user info from backend:', error);
          const profile = oidcAuth.user.profile;
          let roles: string[] = [];
          try {
            const b64 = oidcAuth.user.access_token.split('.')[1]
              .replace(/-/g, '+').replace(/_/g, '/');
            const payload = JSON.parse(atob(b64)) as Record<string, unknown>;
            const realmAccess = payload['realm_access'] as { roles?: string[] } | undefined;
            roles = realmAccess?.roles?.filter(r =>
              ['admin', 'supervisor', 'cajero', 'vendedor'].includes(r)) ?? [];
          } catch {
            const profileRealmAccess = (profile as Record<string, unknown>)['realm_access'] as { roles?: string[] } | undefined;
            roles = profileRealmAccess?.roles?.filter(r =>
              ['admin', 'supervisor', 'cajero', 'vendedor'].includes(r)) ?? [];
          }
          setUser({
            id: profile.sub ?? 'unknown',
            username: profile.preferred_username ?? profile.email ?? 'unknown',
            email: profile.email ?? '',
            nombre: profile.name ?? profile.preferred_username ?? '',
            roles,
            sucursalId: undefined,
            sucursalNombre: undefined,
            sucursalesDisponibles: [],
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
      <Box display="flex" justifyContent="center" alignItems="center" minHeight="100vh">
        <CircularProgress />
      </Box>
    );
  }

  if (oidcAuth.error) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight="100vh" flexDirection="column">
        <h1>Error de autenticaci&oacute;n</h1>
        <p>{oidcAuth.error.message}</p>
      </Box>
    );
  }

  return <>{children}</>;
}

// ── Auth Provider (selecciona Entra ID o Keycloak) ─────────────────────────
export function AuthProvider({ children }: { children: ReactNode }) {
  if (isEntraId) {
    return (
      <MsalProvider instance={msalInstance}>
        <EntraAuthInitializer>{children}</EntraAuthInitializer>
      </MsalProvider>
    );
  }

  // Desarrollo local con Keycloak
  const oidcConfig = {
    ...keycloakConfig,
    onSigninCallback: (): void => {
      window.history.replaceState({}, document.title, window.location.pathname);
    },
  };

  return (
    <OidcAuthProvider {...oidcConfig}>
      <KeycloakAuthInitializer>{children}</KeycloakAuthInitializer>
    </OidcAuthProvider>
  );
}
