import { useMemo } from 'react';
import {
  Box,
  Card,
  CardContent,
  Typography,
  Chip,
  Grid,
} from '@mui/material';
import TrendingUpIcon    from '@mui/icons-material/TrendingUp';
import TrendingDownIcon  from '@mui/icons-material/TrendingDown';
import TrendingFlatIcon  from '@mui/icons-material/TrendingFlat';
import WarningAmberIcon  from '@mui/icons-material/WarningAmber';
import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  Tooltip as RechartsTooltip,
  ResponsiveContainer,
  ReferenceLine,
} from 'recharts';
import { sincoColors } from '@/theme/tokens';
import type { MetricasDelDiaDTO, VentaPorHoraDTO, AlertaStockDTO } from '@/types/api';

// ── Tipos ──────────────────────────────────────────────────────────────────

interface RadarMetric {
  label:   string;
  value:   number;
  format:  'currency' | 'units' | 'percent';
  trend?:  'up' | 'down' | 'stable';
  alert?:  string;
}

// ── Helpers ────────────────────────────────────────────────────────────────

function fmt(value: number, format: RadarMetric['format']): string {
  if (format === 'currency') return `$${value.toLocaleString('es-CO')}`;
  if (format === 'percent')  return `${value.toFixed(1)}%`;
  return value.toLocaleString('es-CO');
}

/**
 * Construye una proyección intradiaria desde ventasPorHora.
 * Horas ya transcurridas → valor real.
 * Horas restantes → proyección basada en el promedio de las últimas 3 horas con datos.
 */
function buildIntradayForecast(
  ventasPorHora: VentaPorHoraDTO[],
): { hour: string; actual?: number; projected: number }[] {
  const nowHour = new Date().getHours();
  const byHour  = new Map(ventasPorHora.map(v => [v.hora, v.total]));

  // Promedio de las últimas 3 horas con ventas (para proyección)
  const pastWithSales = ventasPorHora
    .filter(v => v.hora <= nowHour && v.total > 0)
    .slice(-3);
  const avgRate = pastWithSales.length
    ? pastWithSales.reduce((s, v) => s + v.total, 0) / pastWithSales.length
    : 0;

  return Array.from({ length: 24 }, (_, h) => ({
    hour:      `${String(h).padStart(2, '0')}h`,
    actual:    h <= nowHour ? (byHour.get(h) ?? 0) : undefined,
    projected: h > nowHour ? Math.round(avgRate) : 0,
  }));
}

// ── Sub-componentes ────────────────────────────────────────────────────────

function MetricCard({ m }: { m: RadarMetric }) {
  const trendColor =
    m.trend === 'up'   ? sincoColors.success.main :
    m.trend === 'down' ? sincoColors.error.main   : sincoColors.text.secondary;

  const TrendIcon =
    m.trend === 'up'   ? TrendingUpIcon   :
    m.trend === 'down' ? TrendingDownIcon : TrendingFlatIcon;

  return (
    <Card sx={{ height: '100%' }}>
      <CardContent sx={{ pb: '12px !important' }}>
        <Typography
          variant="caption"
          color="text.secondary"
          fontWeight={600}
          textTransform="uppercase"
          letterSpacing="0.05em"
          display="block"
        >
          {m.label}
        </Typography>
        <Typography variant="h5" fontWeight={700} sx={{ my: 0.5 }}>
          {fmt(m.value, m.format)}
        </Typography>
        {m.trend && (
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
            <TrendIcon sx={{ fontSize: 14, color: trendColor }} />
            <Typography variant="caption" sx={{ color: trendColor }}>
              {m.trend === 'up' ? 'En alza' : m.trend === 'down' ? 'En baja' : 'Estable'}
            </Typography>
          </Box>
        )}
        {m.alert && (
          <Chip
            icon={<WarningAmberIcon sx={{ fontSize: '14px !important' }} />}
            label={m.alert}
            size="small"
            sx={{
              mt: 1,
              bgcolor: sincoColors.warning.bg,
              color:   sincoColors.warning.main,
              fontSize: '0.7rem',
              height: 22,
            }}
          />
        )}
      </CardContent>
    </Card>
  );
}

// ── Componente principal ───────────────────────────────────────────────────

interface BusinessRadarProps {
  metricas:      MetricasDelDiaDTO;
  ventasPorHora: VentaPorHoraDTO[];
  stockRisks:    AlertaStockDTO[];
}

/**
 * Capa 14 — Radar de Negocio.
 * Visión predictiva para supervisores y gerentes. Combina datos actuales
 * con proyecciones intradiarias y alertas de ruptura de stock.
 * Visible solo para roles con showDashboard = true.
 */
export function BusinessRadar({ metricas, ventasPorHora, stockRisks }: BusinessRadarProps) {
  const metrics: RadarMetric[] = [
    {
      label:  'Ventas del día',
      value:  metricas.ventasTotales,
      format: 'currency',
      trend:  metricas.porcentajeCambio > 5 ? 'up' : metricas.porcentajeCambio < -5 ? 'down' : 'stable',
      alert:  metricas.porcentajeCambio < -20 ? `${Math.abs(metricas.porcentajeCambio).toFixed(0)}% vs ayer` : undefined,
    },
    {
      label:  'Utilidad del día',
      value:  metricas.utilidadDelDia,
      format: 'currency',
      trend:  metricas.margenPromedio > 25 ? 'up' : metricas.margenPromedio < 10 ? 'down' : 'stable',
    },
    {
      label:  'Margen promedio',
      value:  metricas.margenPromedio,
      format: 'percent',
      alert:  metricas.margenPromedio < 10 ? 'Margen bajo' : undefined,
    },
    {
      label:  'Ticket promedio',
      value:  metricas.ticketPromedio,
      format: 'currency',
    },
  ];

  const forecastData = useMemo(
    () => buildIntradayForecast(ventasPorHora),
    [ventasPorHora]
  );

  const nowHour = new Date().getHours();
  const nowLabel = `${String(nowHour).padStart(2, '0')}h`;

  return (
    <Box>
      {/* Encabezado */}
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 2 }}>
        <TrendingUpIcon sx={{ color: sincoColors.brand[700] }} />
        <Typography variant="h6" fontWeight={700}>Radar de Negocio</Typography>
        <Chip
          label="Supervisor / Admin"
          size="small"
          sx={{ bgcolor: sincoColors.brand[50], color: sincoColors.brand[800], fontSize: '0.7rem' }}
        />
      </Box>

      {/* Métricas clave */}
      <Grid container spacing={2} sx={{ mb: 3 }}>
        {metrics.map((m, i) => (
          <Grid key={i} size={{ xs: 12, sm: 6, md: 3 }}>
            <MetricCard m={m} />
          </Grid>
        ))}
      </Grid>

      {/* Proyección intradiaria */}
      <Card sx={{ mb: 3 }}>
        <CardContent>
          <Typography variant="subtitle1" fontWeight={600} mb={2}>
            Ventas por hora — proyección del día
          </Typography>
          <ResponsiveContainer width="100%" height={180}>
            <AreaChart data={forecastData} margin={{ top: 4, right: 8, left: 0, bottom: 0 }}>
              <defs>
                <linearGradient id="radarActualGrad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%"  stopColor={sincoColors.brand[700]} stopOpacity={0.18} />
                  <stop offset="95%" stopColor={sincoColors.brand[700]} stopOpacity={0} />
                </linearGradient>
              </defs>
              <XAxis
                dataKey="hour"
                tick={{ fontSize: 10 }}
                interval={2}
              />
              <YAxis
                tick={{ fontSize: 10 }}
                tickFormatter={(v) => v >= 1000 ? `${(v / 1000).toFixed(0)}k` : v}
                width={40}
              />
              <RechartsTooltip
                formatter={(v: number, name: string) => [
                  `$${v.toLocaleString('es-CO')}`,
                  name === 'actual' ? 'Real' : 'Proyectado',
                ]}
              />
              {/* Línea "ahora" */}
              <ReferenceLine x={nowLabel} stroke={sincoColors.brand[600]} strokeDasharray="4 4" label="" />
              <Area
                type="monotone"
                dataKey="actual"
                stroke={sincoColors.brand[700]}
                fill="url(#radarActualGrad)"
                strokeWidth={2}
                dot={false}
                connectNulls
              />
              <Area
                type="monotone"
                dataKey="projected"
                stroke={sincoColors.warning.main}
                fill="none"
                strokeWidth={2}
                strokeDasharray="5 5"
                dot={false}
                connectNulls
              />
            </AreaChart>
          </ResponsiveContainer>
          <Box sx={{ display: 'flex', gap: 2, mt: 1 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
              <Box sx={{ width: 16, height: 2, bgcolor: sincoColors.brand[700], borderRadius: 1 }} />
              <Typography variant="caption" color="text.secondary">Real</Typography>
            </Box>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
              <Box sx={{ width: 16, height: 2, borderTop: `2px dashed ${sincoColors.warning.main}`, borderRadius: 1 }} />
              <Typography variant="caption" color="text.secondary">Proyectado</Typography>
            </Box>
          </Box>
        </CardContent>
      </Card>

      {/* Riesgos de ruptura de stock */}
      {stockRisks.length > 0 && (
        <Card>
          <CardContent>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 2 }}>
              <WarningAmberIcon sx={{ color: sincoColors.warning.main }} />
              <Typography variant="subtitle1" fontWeight={600}>
                Productos en riesgo de ruptura ({stockRisks.length})
              </Typography>
            </Box>
            {stockRisks.map((risk, i) => {
              const pctRestante = risk.stockMinimo > 0
                ? (risk.cantidadActual / risk.stockMinimo) * 100
                : 100;
              const isCritical = pctRestante <= 50;

              return (
                <Box
                  key={i}
                  sx={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    p: 1.5,
                    mb: 1,
                    bgcolor:    isCritical ? sincoColors.error.bg   : sincoColors.warning.bg,
                    borderRadius: '8px',
                    borderLeft: `4px solid ${isCritical ? sincoColors.error.main : sincoColors.warning.main}`,
                  }}
                >
                  <Typography variant="body2" fontWeight={600}>
                    {risk.nombreProducto}
                  </Typography>
                  <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
                    <Typography variant="caption" color="text.secondary">
                      {risk.cantidadActual} uds · mín {risk.stockMinimo}
                    </Typography>
                    <Chip
                      label={isCritical ? 'Crítico' : 'Bajo'}
                      size="small"
                      sx={{
                        bgcolor:  isCritical ? sincoColors.error.main : sincoColors.warning.main,
                        color:    'white',
                        fontWeight: 700,
                        fontSize: '0.7rem',
                        height: 20,
                      }}
                    />
                  </Box>
                </Box>
              );
            })}
          </CardContent>
        </Card>
      )}
    </Box>
  );
}
