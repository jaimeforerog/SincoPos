import { useState } from 'react';
import {
  Box,
  Card,
  CardContent,
  Typography,
  Chip,
  IconButton,
  Tooltip,
  LinearProgress,
  Button,
} from '@mui/material';
import AutoFixHighIcon    from '@mui/icons-material/AutoFixHigh';
import CloseIcon          from '@mui/icons-material/Close';
import ShoppingCartIcon   from '@mui/icons-material/ShoppingCart';
import InfoOutlinedIcon   from '@mui/icons-material/InfoOutlined';
import { sincoColors }    from '@/theme/tokens';
import type { AutomaticActionDTO } from '@/types/api';

interface SugerenciasPanelProps {
  sugerencias: AutomaticActionDTO[];
}

/**
 * Capa 10 — Explicabilidad.
 * Muestra sugerencias automáticas del sistema con el "por qué" visible,
 * la fuente de datos y la confianza. El usuario puede descartar cada una.
 */
export function SugerenciasPanel({ sugerencias }: SugerenciasPanelProps) {
  const [dismissed, setDismissed] = useState<Set<string>>(new Set());

  const visibles = sugerencias.filter(
    (s) => !dismissed.has(s.productoId ?? s.description)
  );

  if (visibles.length === 0) return null;

  const dismiss = (s: AutomaticActionDTO) =>
    setDismissed((prev) => new Set([...prev, s.productoId ?? s.description]));

  return (
    <Card sx={{ mb: 3, border: `1px solid ${sincoColors.brand[200]}` }}>
      <CardContent>
        {/* Encabezado */}
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 2 }}>
          <AutoFixHighIcon sx={{ color: sincoColors.brand[700] }} />
          <Typography variant="subtitle1" fontWeight={700}>
            Sugerencias inteligentes
          </Typography>
          <Chip
            label={`${visibles.length} pendiente${visibles.length !== 1 ? 's' : ''}`}
            size="small"
            sx={{
              bgcolor: sincoColors.brand[50],
              color:   sincoColors.brand[800],
              fontSize: '0.7rem',
            }}
          />
        </Box>

        {/* Lista de sugerencias */}
        {visibles.map((s) => (
          <Box
            key={s.productoId ?? s.description}
            sx={{
              p:  1.5,
              mb: 1.5,
              bgcolor:      sincoColors.surface.subtle,
              borderRadius: '10px',
              borderLeft:   `4px solid ${sincoColors.brand[600]}`,
              position:     'relative',
            }}
          >
            {/* Botón descartar */}
            <Tooltip title="Descartar sugerencia">
              <IconButton
                size="small"
                onClick={() => dismiss(s)}
                sx={{ position: 'absolute', top: 6, right: 6, opacity: 0.5 }}
                aria-label="Descartar"
              >
                <CloseIcon sx={{ fontSize: 14 }} />
              </IconButton>
            </Tooltip>

            {/* Descripción */}
            <Typography variant="body2" fontWeight={600} sx={{ pr: 3 }}>
              {s.description}
            </Typography>

            {/* Días restantes */}
            {s.diasRestantes !== undefined && (
              <Chip
                label={`${s.diasRestantes} días de stock`}
                size="small"
                sx={{
                  mt: 0.5,
                  height: 20,
                  fontSize: '0.68rem',
                  bgcolor: s.diasRestantes <= 3
                    ? sincoColors.error.bg
                    : s.diasRestantes <= 7
                    ? sincoColors.warning.bg
                    : sincoColors.surface.subtle,
                  color: s.diasRestantes <= 3
                    ? sincoColors.error.main
                    : s.diasRestantes <= 7
                    ? sincoColors.warning.main
                    : sincoColors.text.secondary,
                }}
              />
            )}

            {/* Por qué (Capa 10 — Explicabilidad) */}
            <Box
              sx={{
                mt: 1,
                p: 0.75,
                bgcolor:    'background.paper',
                borderRadius: '6px',
                borderLeft: `3px solid ${sincoColors.brand[600]}`,
              }}
            >
              <Typography variant="caption" color="text.secondary">
                <strong>Por qué:</strong> {s.reason}
              </Typography>
            </Box>

            {/* Fuente de datos */}
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mt: 0.75 }}>
              <InfoOutlinedIcon sx={{ fontSize: 12, color: 'text.disabled' }} />
              <Typography variant="caption" color="text.disabled">
                {s.dataSource}
              </Typography>
            </Box>

            {/* Barra de confianza */}
            <Box sx={{ mt: 1, display: 'flex', alignItems: 'center', gap: 1 }}>
              <Typography variant="caption" color="text.secondary" sx={{ minWidth: 70 }}>
                Confianza {Math.round(s.confidence * 100)}%
              </Typography>
              <LinearProgress
                variant="determinate"
                value={s.confidence * 100}
                sx={{
                  flexGrow: 1,
                  height: 4,
                  borderRadius: 2,
                  bgcolor: sincoColors.brand[100],
                  '& .MuiLinearProgress-bar': {
                    bgcolor: s.confidence >= 0.7
                      ? sincoColors.success.main
                      : s.confidence >= 0.4
                      ? sincoColors.warning.main
                      : sincoColors.error.main,
                  },
                }}
              />
            </Box>

            {/* Acción: crear orden */}
            {s.canOverride && (
              <Box sx={{ mt: 1.5, display: 'flex', gap: 1 }}>
                <Button
                  size="small"
                  variant="outlined"
                  startIcon={<ShoppingCartIcon />}
                  href="/compras"
                  sx={{ fontSize: '0.72rem', py: 0.25 }}
                >
                  Crear orden de compra
                </Button>
              </Box>
            )}
          </Box>
        ))}
      </CardContent>
    </Card>
  );
}
