/**
 * Referencia mutable al getAccessToken del SDK de WorkOS.
 * Se inyecta desde AuthProvider para que el interceptor de axios
 * pueda obtener un token fresco sin depender de hooks de React.
 */
export let getWorkosToken: (() => Promise<string | null>) | null = null;

export function setWorkosTokenGetter(fn: () => Promise<string | null>) {
  getWorkosToken = fn;
}

// refresh_token en memoria — nunca persiste a localStorage para evitar exposición XSS.
// Se pierde al recargar la página; si el SDK de WorkOS tiene sesión activa, se renueva
// automáticamente. Si no, el usuario debe volver a autenticarse.
let _refreshToken: string | null = null;

export function getRefreshToken(): string | null { return _refreshToken; }
export function setRefreshToken(token: string): void { _refreshToken = token; }
export function clearRefreshToken(): void { _refreshToken = null; }
