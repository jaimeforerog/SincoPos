import { LogLevel, PublicClientApplication, EventType } from '@azure/msal-browser';
import type { Configuration, AuthenticationResult } from '@azure/msal-browser';

const TENANT_ID = import.meta.env.VITE_ENTRA_TENANT_ID || 'common';
const CLIENT_ID = import.meta.env.VITE_ENTRA_CLIENT_ID || '';
const API_SCOPE = import.meta.env.VITE_ENTRA_API_SCOPE || `api://${CLIENT_ID}/.default`;

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
  scopes: [API_SCOPE],
};

// ── MSAL instance (singleton) ──────────────────────────────────────────────
export const msalInstance = new PublicClientApplication(msalConfig);

msalInstance.initialize().then(() => {
  const accounts = msalInstance.getAllAccounts();
  if (accounts.length > 0) {
    msalInstance.setActiveAccount(accounts[0]);
  }

  msalInstance.addEventCallback((event) => {
    if (event.eventType === EventType.LOGIN_SUCCESS && event.payload) {
      const result = event.payload as AuthenticationResult;
      msalInstance.setActiveAccount(result.account);
    }
  });
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
