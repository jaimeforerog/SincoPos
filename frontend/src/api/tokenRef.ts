/**
 * Referencia mutable al getAccessToken del SDK de WorkOS.
 * Se inyecta desde AuthProvider para que el interceptor de axios
 * pueda obtener un token fresco sin depender de hooks de React.
 */
export let getWorkosToken: (() => Promise<string | null>) | null = null;

export function setWorkosTokenGetter(fn: () => Promise<string | null>) {
  getWorkosToken = fn;
}
