import { useQuery } from '@tanstack/react-query';
import {
  Container, Box, Typography, Paper, Chip, Tooltip,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  LinearProgress, Alert,
  Grid,
} from '@mui/material';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import ErrorIcon from '@mui/icons-material/Error';
import SpeedIcon from '@mui/icons-material/Speed';
import AccountTreeIcon from '@mui/icons-material/AccountTree';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip as RechartTooltip, ResponsiveContainer, Cell } from 'recharts';
import { orquestadorApi, type EjecucionResumenDto } from '@/api/orquestador';

// ── Helpers ───────────────────────────────────────────────────────────────────

const LATENCIA_OK  = 200;
const LATENCIA_MED = 400;

function latenciaColor(ms: number): string {
  if (ms <= LATENCIA_OK) return '#2e7d32';
  if (ms <= LATENCIA_MED) return '#e65100';
  return '#c62828';
}

function LatenciaChip({ ms }: { ms: number }) {
  return (
    <Chip
      label={`${ms} ms`}
      size="small"
      sx={{
        fontWeight: 700, fontSize: '0.72rem',
        bgcolor: `${latenciaColor(ms)}18`,
        color: latenciaColor(ms),
      }}
    />
  );
}

function PasosBadges({ pasos }: { pasos: EjecucionResumenDto['pasos'] }) {
  return (
    <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
      {pasos.map((p) => (
        <Tooltip key={p.nombre} title={p.error ?? `${p.ms} ms`}>
          <Chip
            size="small"
            icon={p.exitoso
              ? <CheckCircleIcon sx={{ fontSize: '12px !important', color: '#2e7d32 !important' }} />
              : <ErrorIcon sx={{ fontSize: '12px !important', color: '#c62828 !important' }} />}
            label={`${p.nombre} ${p.ms}ms`}
            sx={{
              fontSize: '0.68rem',
              bgcolor: p.exitoso ? '#f1f8e9' : '#ffebee',
              color: p.exitoso ? '#2e7d32' : '#c62828',
            }}
          />
        </Tooltip>
      ))}
    </Box>
  );
}

// ── Métricas KPI ──────────────────────────────────────────────────────────────

function KpiCard({ label, value, sub, color = '#1565c0' }: {
  label: string; value: string; sub?: string; color?: string;
}) {
  return (
    <Paper variant="outlined" sx={{ p: 2, flex: 1 }}>
      <Typography variant="caption" color="text.secondary" fontWeight={600} textTransform="uppercase">
        {label}
      </Typography>
      <Typography variant="h4" fontWeight={700} sx={{ color, my: 0.5 }}>{value}</Typography>
      {sub && <Typography variant="caption" color="text.secondary">{sub}</Typography>}
    </Paper>
  );
}

// ── Página ────────────────────────────────────────────────────────────────────

export function PipelineMonitorPage() {
  const { data: metricas, isLoading: loadingMetricas } = useQuery({
    queryKey: ['pipeline-metricas'],
    queryFn: orquestadorApi.getMetricas,
    refetchInterval: 10_000,  // refresca cada 10s
  });

  const { data: ejecuciones = [], isLoading: loadingEjecuciones } = useQuery({
    queryKey: ['pipeline-ejecuciones'],
    queryFn: () => orquestadorApi.getEjecuciones(30),
    refetchInterval: 10_000,
  });

  const sinDatos = !loadingMetricas && metricas?.totalEjecuciones === 0;

  return (
    <Container maxWidth="xl">
      {/* Hero */}
      <Box
        sx={{
          background: 'linear-gradient(135deg, #1565c0 0%, #0d47a1 50%, #01579b 100%)',
          borderRadius: 3, px: { xs: 3, md: 4 }, py: { xs: 2.5, md: 3 },
          mb: 2, mt: 1,
        }}
      >
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <AccountTreeIcon sx={{ color: 'rgba(255,255,255,0.9)', fontSize: 32 }} />
          <Box>
            <Typography variant="h5" fontWeight={700} sx={{ color: '#fff' }}>
              Monitor de Pipeline
            </Typography>
            <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.75)' }}>
              Trazabilidad por paso del orquestador de ventas — Capa 15 del Blueprint
            </Typography>
          </Box>
          <Box sx={{ ml: 'auto' }}>
            <Chip
              label="Auto-refresco 10s"
              size="small"
              sx={{ bgcolor: 'rgba(255,255,255,0.15)', color: '#fff', fontWeight: 600 }}
            />
          </Box>
        </Box>
      </Box>

      {sinDatos && (
        <Alert severity="info" sx={{ mb: 2 }}>
          Sin ejecuciones registradas aún. Las métricas aparecerán cuando se procesen ventas
          vía <code>POST /api/v1/orquestador/venta</code>.
        </Alert>
      )}

      {/* KPIs */}
      {metricas && metricas.totalEjecuciones > 0 && (
        <Box sx={{ display: 'flex', gap: 2, mb: 2, flexWrap: 'wrap' }}>
          <KpiCard
            label="Total ejecuciones"
            value={metricas.totalEjecuciones.toString()}
            sub="En memoria (última sesión)"
          />
          <KpiCard
            label="Tasa de éxito"
            value={`${metricas.tasaExitoPorc.toFixed(1)}%`}
            color={metricas.tasaExitoPorc >= 99 ? '#2e7d32' : metricas.tasaExitoPorc >= 90 ? '#e65100' : '#c62828'}
            sub={`${metricas.exitosas} exitosas · ${metricas.fallidas} fallidas`}
          />
          <KpiCard
            label="Latencia promedio"
            value={`${metricas.latenciaPromedioMs} ms`}
            color={latenciaColor(metricas.latenciaPromedioMs)}
            sub={`Min ${metricas.latenciaMinimaMs}ms · Max ${metricas.latenciaMaximaMs}ms`}
          />
          <KpiCard
            label="Objetivo SLA"
            value={metricas.latenciaPromedioMs <= 500 ? '✅ OK' : '⚠️ Lento'}
            color={metricas.latenciaPromedioMs <= 500 ? '#2e7d32' : '#c62828'}
            sub="Objetivo: < 500ms end-to-end"
          />
        </Box>
      )}

      <Grid container spacing={2} sx={{ mb: 2 }}>
        {/* Gráfico de latencia por paso */}
        {metricas && metricas.pasos.length > 0 && (
          <Grid size={{ xs: 12, md: 6 }}>
            <Paper variant="outlined" sx={{ p: 2 }}>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1.5 }}>
                <SpeedIcon sx={{ color: '#1565c0', fontSize: 18 }} />
                <Typography variant="subtitle2" fontWeight={700}>
                  Latencia promedio por paso (ms)
                </Typography>
              </Box>
              <ResponsiveContainer width="100%" height={180}>
                <BarChart data={metricas.pasos} margin={{ top: 4, right: 8, left: -10, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
                  <XAxis dataKey="nombre" tick={{ fontSize: 11 }} />
                  <YAxis tick={{ fontSize: 11 }} unit="ms" />
                  <RechartTooltip
                    formatter={(v: number) => [`${v}ms`, 'Promedio']}
                    contentStyle={{ fontSize: 12 }}
                  />
                  <Bar dataKey="promedioMs" radius={[4, 4, 0, 0]}>
                    {metricas.pasos.map((p) => (
                      <Cell key={p.nombre} fill={latenciaColor(p.promedioMs)} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            </Paper>
          </Grid>
        )}

        {/* Tasa de éxito por paso */}
        {metricas && metricas.pasos.length > 0 && (
          <Grid size={{ xs: 12, md: 6 }}>
            <Paper variant="outlined" sx={{ p: 2 }}>
              <Typography variant="subtitle2" fontWeight={700} sx={{ mb: 1.5 }}>
                Tasa de éxito por paso
              </Typography>
              <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
                {metricas.pasos.map((p) => (
                  <Box key={p.nombre}>
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
                      <Typography variant="caption" fontWeight={600}>{p.nombre}</Typography>
                      <Typography variant="caption" fontWeight={700}
                        sx={{ color: p.tasaExitoPorc >= 99 ? '#2e7d32' : '#c62828' }}>
                        {p.tasaExitoPorc.toFixed(1)}%
                      </Typography>
                    </Box>
                    <LinearProgress
                      variant="determinate"
                      value={p.tasaExitoPorc}
                      sx={{
                        height: 6, borderRadius: 3, bgcolor: 'grey.200',
                        '& .MuiLinearProgress-bar': {
                          bgcolor: p.tasaExitoPorc >= 99 ? '#2e7d32' : '#c62828',
                          borderRadius: 3,
                        },
                      }}
                    />
                  </Box>
                ))}
              </Box>
            </Paper>
          </Grid>
        )}
      </Grid>

      {/* Tabla de ejecuciones recientes */}
      <Paper variant="outlined">
        <Box sx={{ px: 2, py: 1.5, borderBottom: '1px solid', borderColor: 'divider' }}>
          <Typography variant="subtitle2" fontWeight={700}>
            Ejecuciones recientes ({ejecuciones.length})
          </Typography>
        </Box>
        <TableContainer>
          <Table size="small">
            <TableHead sx={{ bgcolor: 'grey.50' }}>
              <TableRow>
                <TableCell>Timestamp</TableCell>
                <TableCell>Estado</TableCell>
                <TableCell>Latencia total</TableCell>
                <TableCell>Pasos del pipeline</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {loadingEjecuciones && (
                <TableRow>
                  <TableCell colSpan={4} align="center">Cargando...</TableCell>
                </TableRow>
              )}
              {!loadingEjecuciones && ejecuciones.length === 0 && (
                <TableRow>
                  <TableCell colSpan={4} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                    Sin ejecuciones registradas en esta sesión
                  </TableCell>
                </TableRow>
              )}
              {ejecuciones.map((e, i) => (
                <TableRow key={i} hover sx={{ bgcolor: e.exitoso ? undefined : '#fff8f8' }}>
                  <TableCell>
                    <Typography variant="caption">
                      {new Date(e.timestamp).toLocaleTimeString('es-CO')}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    {e.exitoso
                      ? <Chip label="Exitoso" size="small" color="success" />
                      : (
                        <Tooltip title={e.error ?? ''}>
                          <Chip label="Fallido" size="small" color="error" />
                        </Tooltip>
                      )}
                  </TableCell>
                  <TableCell>
                    <LatenciaChip ms={e.totalMs} />
                  </TableCell>
                  <TableCell>
                    <PasosBadges pasos={e.pasos} />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </Paper>
    </Container>
  );
}
