import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Container, Box, Typography, Tabs, Tab, Chip, Alert,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper,
  LinearProgress, Tooltip,
} from '@mui/material';
import HubIcon from '@mui/icons-material/Hub';
import LinkIcon from '@mui/icons-material/Link';
import CompareArrowsIcon from '@mui/icons-material/CompareArrows';
import CloudOffIcon from '@mui/icons-material/CloudOff';
import { colectivaApi } from '@/api/colectiva';
import { useAuth } from '@/hooks/useAuth';

/** Barra proporcional para frecuencia */
function FreqBar({ value }: { value: number }) {
  const pct = Math.round(value * 100);
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
      <LinearProgress
        variant="determinate"
        value={pct}
        sx={{ flexGrow: 1, height: 6, borderRadius: 3, bgcolor: 'grey.200',
          '& .MuiLinearProgress-bar': { bgcolor: '#1565c0', borderRadius: 3 } }}
      />
      <Typography variant="caption" sx={{ minWidth: 32, color: 'text.secondary' }}>
        {pct}%
      </Typography>
    </Box>
  );
}

export function InteligenciaColectivaPage() {
  const [tab, setTab] = useState(0);
  const { activeSucursalId, activeEmpresaId } = useAuth();

  const { data: combos = [], isLoading: loadingCombos } = useQuery({
    queryKey: ['colectiva-combos', activeSucursalId],
    queryFn: () => colectivaApi.getCombos(activeSucursalId!, 15),
    enabled: !!activeSucursalId,
  });

  const { data: comparativo, isLoading: loadingComparativo } = useQuery({
    queryKey: ['colectiva-comparativo', activeEmpresaId],
    queryFn: () => colectivaApi.compararSucursales(activeEmpresaId!),
    enabled: !!activeEmpresaId && tab === 1,
  });

  const { data: estadoGlobal } = useQuery({
    queryKey: ['colectiva-estado-global'],
    queryFn: colectivaApi.getEstadoGlobal,
    enabled: tab === 2,
  });

  const maxVelocidad = comparativo
    ? Math.max(
        1,
        ...comparativo.items.flatMap((item) => Object.values(item.velocidadPorSucursal))
      )
    : 1;

  return (
    <Container maxWidth="xl">
      {/* Hero */}
      <Box
        sx={{
          background: 'linear-gradient(135deg, #01579b 0%, #0277bd 50%, #0288d1 100%)',
          borderRadius: 3, px: { xs: 3, md: 4 }, py: { xs: 2.5, md: 3 },
          mb: 2, mt: 1,
        }}
      >
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <HubIcon sx={{ color: 'rgba(255,255,255,0.9)', fontSize: 32 }} />
          <Box>
            <Typography variant="h5" fontWeight={700} sx={{ color: '#fff' }}>
              Inteligencia Colectiva
            </Typography>
            <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.75)' }}>
              Patrones cross-selling y comparación entre sucursales — Capa 13 del Blueprint
            </Typography>
          </Box>
        </Box>
      </Box>

      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2 }}>
        <Tab icon={<LinkIcon fontSize="small" />} iconPosition="start" label="Combos de Productos" />
        <Tab icon={<CompareArrowsIcon fontSize="small" />} iconPosition="start" label="Comparación Sucursales" />
        <Tab icon={<CloudOffIcon fontSize="small" />} iconPosition="start" label="Estado Global" />
      </Tabs>

      {/* ── Tab 0: Combos ─────────────────────────────────────────────── */}
      {tab === 0 && (
        <>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Productos comprados juntos con más frecuencia en esta sucursal.
            Úsalos para sugerencias de cross-selling en el POS y configuración de bundles.
          </Typography>
          <TableContainer component={Paper} variant="outlined">
            <Table size="small">
              <TableHead sx={{ bgcolor: 'grey.50' }}>
                <TableRow>
                  <TableCell>#</TableCell>
                  <TableCell>Producto A</TableCell>
                  <TableCell>Producto B</TableCell>
                  <TableCell align="right">Veces juntos</TableCell>
                  <TableCell sx={{ minWidth: 160 }}>Frecuencia</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {loadingCombos && (
                  <TableRow>
                    <TableCell colSpan={5} align="center">Cargando...</TableCell>
                  </TableRow>
                )}
                {!loadingCombos && combos.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={5} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                      Sin datos de combos. Se requieren ventas con 2+ productos distintos.
                    </TableCell>
                  </TableRow>
                )}
                {combos.map((c, i) => (
                  <TableRow key={`${c.productoAId}:${c.productoBId}`} hover>
                    <TableCell>
                      <Chip label={i + 1} size="small" sx={{ fontWeight: 700, minWidth: 28 }} />
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" fontWeight={500}>{c.productoANombre}</Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" fontWeight={500}>{c.productoBNombre}</Typography>
                    </TableCell>
                    <TableCell align="right">
                      <Typography variant="body2" fontWeight={700}>{c.vecesJuntos}</Typography>
                    </TableCell>
                    <TableCell>
                      <FreqBar value={c.frecuencia} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </>
      )}

      {/* ── Tab 1: Comparación cross-sucursal ────────────────────────── */}
      {tab === 1 && (
        <>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Velocidad de ventas (unidades totales) por producto y sucursal.
            Las celdas más oscuras indican mayor volumen de ventas.
          </Typography>
          {loadingComparativo && (
            <LinearProgress sx={{ mb: 2 }} />
          )}
          {!loadingComparativo && comparativo && comparativo.items.length === 0 && (
            <Alert severity="info">
              Sin datos comparativos. Se requieren ventas en al menos una sucursal de la empresa.
            </Alert>
          )}
          {!loadingComparativo && comparativo && comparativo.items.length > 0 && (
            <TableContainer component={Paper} variant="outlined" sx={{ overflowX: 'auto' }}>
              <Table size="small">
                <TableHead sx={{ bgcolor: 'grey.50' }}>
                  <TableRow>
                    <TableCell sx={{ minWidth: 180 }}>Producto</TableCell>
                    {comparativo.sucursales.map((s) => (
                      <TableCell key={s} align="center" sx={{ minWidth: 110 }}>
                        <Typography variant="caption" fontWeight={700}>{s}</Typography>
                      </TableCell>
                    ))}
                  </TableRow>
                </TableHead>
                <TableBody>
                  {comparativo.items.map((item) => (
                    <TableRow key={item.productoId} hover>
                      <TableCell>
                        <Typography variant="body2" fontWeight={500}>{item.nombreProducto}</Typography>
                      </TableCell>
                      {comparativo.sucursales.map((s) => {
                        const v = item.velocidadPorSucursal[s] ?? 0;
                        const intensity = maxVelocidad > 0 ? v / maxVelocidad : 0;
                        return (
                          <TableCell key={s} align="center">
                            <Tooltip title={`${v} uds`}>
                              <Box
                                sx={{
                                  display: 'inline-block',
                                  px: 1.5, py: 0.5,
                                  borderRadius: 1,
                                  bgcolor: `rgba(21, 101, 192, ${0.08 + intensity * 0.72})`,
                                  color: intensity > 0.6 ? '#fff' : 'text.primary',
                                  fontWeight: 600, fontSize: '0.78rem',
                                  minWidth: 48, textAlign: 'center',
                                }}
                              >
                                {v > 0 ? v.toLocaleString('es-CO') : '—'}
                              </Box>
                            </Tooltip>
                          </TableCell>
                        );
                      })}
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </>
      )}

      {/* ── Tab 2: Estado global ──────────────────────────────────────── */}
      {tab === 2 && (
        <Box sx={{ maxWidth: 640 }}>
          <Alert
            severity="info"
            icon={<CloudOffIcon />}
            sx={{ mb: 2 }}
          >
            <Typography variant="subtitle2" fontWeight={700} sx={{ mb: 0.5 }}>
              Servicio Central Sinco — No disponible (modo local)
            </Typography>
            <Typography variant="body2">
              {estadoGlobal?.mensaje ??
                'Cargando estado del servicio central...'}
            </Typography>
          </Alert>

          <Paper variant="outlined" sx={{ p: 2.5 }}>
            <Typography variant="subtitle2" fontWeight={700} sx={{ mb: 1.5 }}>
              Arquitectura de Inteligencia Colectiva (Capa 13)
            </Typography>

            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
              {[
                { label: 'Patrones locales por sucursal', estado: '✅ Activo', color: '#2e7d32' },
                { label: 'Combos cross-selling local', estado: '✅ Activo', color: '#2e7d32' },
                { label: 'Comparación cross-sucursal (empresa)', estado: '✅ Activo', color: '#2e7d32' },
                { label: 'Propagación a servicio central Sinco', estado: '🔮 Futuro', color: '#7b1fa2' },
                { label: 'Bus de mensajes global (multi-empresa)', estado: '🔮 Futuro', color: '#7b1fa2' },
                { label: 'Patrones globales (≥5 tiendas, ≥90 días)', estado: '🔮 Futuro', color: '#7b1fa2' },
              ].map(({ label, estado, color }) => (
                <Box key={label} sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                  <Typography variant="body2">{label}</Typography>
                  <Chip label={estado} size="small" sx={{ fontWeight: 600, color }} />
                </Box>
              ))}
            </Box>
          </Paper>
        </Box>
      )}
    </Container>
  );
}
