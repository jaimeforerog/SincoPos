import { Box, Typography, Chip, Divider, Avatar, Tooltip } from '@mui/material';
import PersonIcon   from '@mui/icons-material/Person';
import SmartToyIcon from '@mui/icons-material/SmartToy';
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline';
import { sincoColors } from '@/theme/tokens';
import type { ActivityLogFullDTO } from '@/types/api';

// ── Tipos ──────────────────────────────────────────────────────────────────

export interface AuditEntry {
  id:          string;
  timestamp:   string;
  actor:       string;
  actorType:   'human' | 'system';
  action:      string;
  details?:    string;
  reason?:     string;  // Capa 10 — por qué ocurrió la acción
  isAutomated: boolean;
  isError:     boolean;
}

// ── Adaptador ActivityLogFullDTO → AuditEntry ──────────────────────────────

const SYSTEM_ACTIONS = ['Ajuste automático', 'Sync ERP', 'Sistema', 'Background'];

export function activityLogToAuditEntry(log: ActivityLogFullDTO): AuditEntry {
  const isAutomated = SYSTEM_ACTIONS.some(s => log.accion.includes(s)) || log.tipo === 99;
  const details = [
    log.tipoEntidad && log.entidadNombre ? `${log.tipoEntidad}: ${log.entidadNombre}` : null,
    log.descripcion ?? null,
    log.nombreSucursal ? `Sucursal: ${log.nombreSucursal}` : null,
  ].filter(Boolean).join(' · ') || undefined;

  return {
    id:          String(log.id),
    timestamp:   log.fechaHora,
    actor:       log.usuarioNombre ?? log.usuarioEmail,
    actorType:   isAutomated ? 'system' : 'human',
    action:      log.accion,
    details,
    reason:      log.mensajeError ?? undefined,  // si falló, el "por qué" es el error
    isAutomated,
    isError:     !log.exitosa,
  };
}

// ── Componente ─────────────────────────────────────────────────────────────

interface AuditTimelineProps {
  entries: AuditEntry[];
  emptyMessage?: string;
}

/**
 * Capa 11 — Transparencia operativa.
 * Timeline visual de acciones del sistema. Diferencia acciones humanas de automáticas.
 * Incluye el "por qué" de cada acción (Capa 10 — Explicabilidad).
 */
export function AuditTimeline({ entries, emptyMessage = 'Sin actividad reciente' }: AuditTimelineProps) {
  if (entries.length === 0) {
    return (
      <Box sx={{ py: 4, textAlign: 'center' }}>
        <Typography variant="body2" color="text.secondary">{emptyMessage}</Typography>
      </Box>
    );
  }

  return (
    <Box>
      {entries.map((entry, i) => (
        <Box key={entry.id}>
          <Box sx={{ display: 'flex', gap: 2, py: 1.5, alignItems: 'flex-start' }}>

            {/* Avatar: persona vs sistema */}
            <Tooltip title={entry.actorType === 'human' ? 'Acción humana' : 'Acción automática'}>
              <Avatar
                sx={{
                  width: 32, height: 32, mt: 0.25, flexShrink: 0,
                  bgcolor: entry.isError
                    ? sincoColors.error.bg
                    : entry.actorType === 'human'
                    ? sincoColors.brand[50]
                    : sincoColors.surface.subtle,
                  color: entry.isError
                    ? sincoColors.error.main
                    : entry.actorType === 'human'
                    ? sincoColors.brand[800]
                    : sincoColors.text.secondary,
                }}
              >
                {entry.isError
                  ? <ErrorOutlineIcon sx={{ fontSize: 16 }} />
                  : entry.actorType === 'human'
                  ? <PersonIcon sx={{ fontSize: 16 }} />
                  : <SmartToyIcon sx={{ fontSize: 16 }} />
                }
              </Avatar>
            </Tooltip>

            {/* Contenido */}
            <Box sx={{ flex: 1, minWidth: 0 }}>

              {/* Título + chips */}
              <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flexWrap: 'wrap', mb: 0.25 }}>
                <Typography
                  variant="body2"
                  fontWeight={600}
                  color={entry.isError ? 'error.main' : 'text.primary'}
                  sx={{ lineHeight: 1.3 }}
                >
                  {entry.action}
                </Typography>

                {entry.isAutomated && !entry.isError && (
                  <Chip
                    label="automático"
                    size="small"
                    sx={{
                      height: 18, fontSize: '0.65rem',
                      bgcolor: sincoColors.info.bg,
                      color:   sincoColors.info.main,
                    }}
                  />
                )}

                {entry.isError && (
                  <Chip
                    label="fallida"
                    size="small"
                    sx={{
                      height: 18, fontSize: '0.65rem',
                      bgcolor: sincoColors.error.bg,
                      color:   sincoColors.error.main,
                    }}
                  />
                )}
              </Box>

              {/* Detalles */}
              {entry.details && (
                <Typography variant="caption" color="text.secondary" display="block" sx={{ mb: 0.25 }}>
                  {entry.details}
                </Typography>
              )}

              {/* Capa 10 — Explicabilidad: el sistema explica sus acciones */}
              {entry.reason && (
                <Box
                  sx={{
                    mt: 0.5, mb: 0.25,
                    p: '4px 8px',
                    bgcolor: entry.isError ? sincoColors.error.bg : sincoColors.surface.subtle,
                    borderRadius: '6px',
                    borderLeft: `3px solid ${entry.isError ? sincoColors.error.main : sincoColors.brand[600]}`,
                  }}
                >
                  <Typography variant="caption" color="text.secondary">
                    <strong>Por qué:</strong> {entry.reason}
                  </Typography>
                </Box>
              )}

              {/* Actor + timestamp */}
              <Typography variant="caption" color="text.disabled" display="block">
                {entry.actor} · {new Date(entry.timestamp).toLocaleString('es-CO', {
                  dateStyle: 'short', timeStyle: 'short',
                })}
              </Typography>
            </Box>
          </Box>

          {i < entries.length - 1 && <Divider sx={{ ml: 6 }} />}
        </Box>
      ))}
    </Box>
  );
}
