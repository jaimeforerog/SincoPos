import { useCallback } from 'react';
import { isEntraId, loginRequest } from '../msalConfig';
import { msalInstance } from '../AuthProvider';

/**
 * Provides login/logout/acquireToken functions.
 * Uses MSAL (Entra ID) or falls back to OIDC (Keycloak) based on env config.
 * Does NOT call hooks — safe to call anywhere.
 */
export function useMsalLogin() {
  const login = useCallback(() => {
    if (isEntraId) {
      msalInstance.loginRedirect(loginRequest);
    } else {
      // Keycloak login is handled by react-oidc-context in the component tree
      // This function is only called from LoginPage which has access to oidcAuth
      console.warn('useMsalLogin.login() called in Keycloak mode — use oidcAuth.signinRedirect() instead');
    }
  }, []);

  const logout = useCallback(() => {
    sessionStorage.removeItem('access_token');
    if (isEntraId) {
      msalInstance.logoutRedirect({
        postLogoutRedirectUri: window.location.origin,
      });
    }
  }, []);

  const acquireToken = useCallback(async (): Promise<string | null> => {
    if (!isEntraId) {
      return sessionStorage.getItem('access_token');
    }
    const account = msalInstance.getActiveAccount();
    if (!account) return null;
    try {
      const response = await msalInstance.acquireTokenSilent({
        ...loginRequest,
        account,
      });
      return response.accessToken;
    } catch {
      return null;
    }
  }, []);

  return { login, logout, acquireToken };
}
