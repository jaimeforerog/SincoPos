import { useSnackbar } from 'notistack';

/**
 * Capa 7 — Comunicación contextual con propósito.
 *
 * Jerarquía de niveles:
 *  - 'operational'   → algo pasó que el usuario debe atender (warning, abajo derecha)
 *  - 'system'        → error técnico que requiere acción inmediata (error, arriba centro)
 *  - 'anticipation'  → sugerencia proactiva del sistema (info, abajo izquierda)
 *  - 'informational' → ⛔ SILENCIADO — no agrega valor, no genera acción
 *
 * Regla: si la notificación no responde "¿qué hago ahora?", es informacional → no enviar.
 */
type NotificationLevel = 'operational' | 'informational' | 'system' | 'anticipation';

interface ContextualNotif {
  message:    string;
  action?:    string;   // qué puede hacer el usuario (futuro: botón inline)
  context?:   string;   // Capa 10 — por qué se notifica
  level:      NotificationLevel;
  expiresIn?: number;
}

export function useContextualNotification() {
  const { enqueueSnackbar } = useSnackbar();

  const notify = ({ message, context, level, expiresIn = 4000 }: ContextualNotif) => {
    if (level === 'informational') return; // Capa 7 — silenciar ruido

    const fullMessage = context ? `${message} — ${context}` : message;

    enqueueSnackbar(fullMessage, {
      variant: level === 'system'
        ? 'error'
        : level === 'operational'
        ? 'warning'
        : 'info',
      autoHideDuration: expiresIn,
      anchorOrigin: level === 'system'
        ? { vertical: 'top',    horizontal: 'center' }  // errores: arriba centro, no se ignoran
        : level === 'anticipation'
        ? { vertical: 'bottom', horizontal: 'left' }    // sugerencias: esquina opuesta al carrito
        : { vertical: 'bottom', horizontal: 'right' },  // operacionales: abajo derecha
    });
  };

  return {
    notify,
    /** Algo que requiere atención del usuario — stock bajo, sync pendiente, etc. */
    operacional: (message: string, context?: string) =>
      notify({ message, context, level: 'operational', expiresIn: 5000 }),
    /** Error técnico bloqueante — falla de red, error de servidor. */
    sistema: (message: string, context?: string) =>
      notify({ message, context, level: 'system', expiresIn: 8000 }),
    /** Sugerencia proactiva — producto frecuente, precio actualizado, etc. */
    anticipacion: (message: string, context: string) =>
      notify({ message, context, level: 'anticipation', expiresIn: 6000 }),
  };
}
