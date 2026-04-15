/**
 * Utilidades de fecha para inputs HTML.
 *
 * Los inputs `date` y `datetime-local` del navegador trabajan en hora LOCAL,
 * pero `Date.toISOString()` devuelve UTC. En Colombia (UTC-5) esto produce
 * una diferencia de 5 horas y puede cambiar el día después de las 7 PM.
 *
 * Estas funciones usan los métodos `getFullYear / getMonth / getDate /
 * getHours / getMinutes` que devuelven la hora local del navegador.
 */

const pad = (n: number) => String(n).padStart(2, '0');

/** "YYYY-MM-DD" en hora local — para inputs type="date" */
export function localDateStr(d: Date = new Date()): string {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/** "YYYY-MM-DDTHH:mm" en hora local — para inputs type="datetime-local" */
export function localDateTimeStr(d: Date = new Date()): string {
  return `${localDateStr(d)}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** Resta N días a hoy y devuelve "YYYY-MM-DD" en hora local */
export function localDateStrDaysAgo(days: number): string {
  const d = new Date();
  d.setDate(d.getDate() - days);
  return localDateStr(d);
}

/** Resta N días a hoy y devuelve "YYYY-MM-DDTHH:mm" en hora local */
export function localDateTimeStrDaysAgo(days: number): string {
  const d = new Date();
  d.setDate(d.getDate() - days);
  d.setSeconds(0, 0);
  return localDateTimeStr(d);
}
