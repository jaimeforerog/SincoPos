export const WORKOS_CLIENT_ID = import.meta.env.VITE_WORKOS_CLIENT_ID as string;

if (!WORKOS_CLIENT_ID) {
  console.warn('[WorkOS] VITE_WORKOS_CLIENT_ID no está configurado');
}
