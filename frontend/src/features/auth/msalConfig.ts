import { LogLevel, PublicClientApplication, EventType } from '@azure/msal-browser';
import type { Configuration, AuthenticationResult } from '@azure/msal-browser';

const TENANT_ID = import.meta.env.VITE_ENTRA_TENANT_ID || 'consumers';
const CLIENT_ID = import.meta.env.VITE_ENTRA_CLIENT_ID || '';
// Si no se configura VITE_ENTRA_API_SCOPE vía env var, usar scopes OIDC estándar.
// Para evitar 401 en backend, configurar VITE_ENTRA_API_SCOPE en GitHub vars con
// el scope expuesto en el app registration de la API (api://<API_CLIENT_ID>/access_as_user).
const API_SCOPE = import.meta.env.VITE_ENTRA_API_SCOPE || '';

// Fallback a Keycloak si no hay config de Entra ID
const KEYCLOAK_URL = import.meta.env.VITE_KEYCLOAK_URL || 'http://localhost:8080';
const KEYCLOAK_REALM = import.meta.env.VITE_KEYCLOAK_REALM || 'sincopos';
const KEYCLOAK_CLIENT_ID = import.meta.env.VITE_KEYCLOAK_CLIENT_ID || 'sincopos-frontend';

export const isEntraId = !!import.meta.env.VITE_ENTRA_CLIENT_ID;

export const msalConfig: Configuration = {
  auth: {
    clientId: CLIENT_ID,
    authority: `https://login.microsoftonline.com/${TENANT_ID}`,
    redirectUri: window.location.origin + '/callback',
    postLogoutRedirectUri: window.location.origin,
  },
  cache: {
    cacheLocation: 'sessionStorage',
  },
  system: {
    loggerOptions: {
      loggerCallback: (_level, message, containsPii) => {
        if (containsPii) return;
        if (_level === LogLevel.Error) console.error(message);
      },
      logLevel: LogLevel.Error,
    },
  },
};

export const loginRequest = {
  scopes: API_SCOPE ? [API_SCOPE] : ['openid', 'profile', 'email'],
};

// ── MSAL instance (singleton) ──────────────────────────────────────────────
export const msalInstance = new PublicClientApplication(msalConfig);

export const msalInitPromise = msalInstance.initialize().then(async () => {
  // Add event callback before handling redirect so LOGIN_SUCCESS is captured
  msalInstance.addEventCallback((event) => {
    if (event.eventType === EventType.LOGIN_SUCCESS && event.payload) {
      const result = event.payload as AuthenticationResult;
      msalInstance.setActiveAccount(result.account);
    }
  });

  // Process redirect response (MSAL v5 requires explicit call)
  try {
    const result = await msalInstance.handleRedirectPromise();
    if (result?.account) {
      msalInstance.setActiveAccount(result.account);
      console.log('[MSAL] Redirect processed, account:', result.account.username);
      return;
    }
  } catch (error) {
    console.error('[MSAL] handleRedirectPromise failed:', error);
  }

  // No redirect — check cached accounts
  const accounts = msalInstance.getAllAccounts();
  if (accounts.length > 0) {
    msalInstance.setActiveAccount(accounts[0]);
    console.log('[MSAL] Cached account found:', accounts[0].username);
  } else {
    console.log('[MSAL] No accounts found');
  }
});

export const keycloakConfig = {
  authority: `${KEYCLOAK_URL}/realms/${KEYCLOAK_REALM}`,
  client_id: KEYCLOAK_CLIENT_ID,
  redirect_uri: window.location.origin + '/callback',
  response_type: 'code' as const,
  scope: 'openid profile email',
  automaticSilentRenew: true,
  loadUserInfo: true,
};
