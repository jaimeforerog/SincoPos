import { Card, CardContent, Typography, Box } from '@mui/material';
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from 'recharts';
import type { VentaPorHoraDTO } from '@/types/api';

interface SalesChartProps {
  data: VentaPorHoraDTO[];
}

const formatCurrency = (value: number) => {
  return new Intl.NumberFormat('es-CO', {
    style: 'currency',
    currency: 'COP',
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  }).format(value);
};

const CustomTooltip = ({ active, payload }: any) => {
  if (active && payload && payload.length) {
    const data = payload[0].payload as { hora: number; total: number; cantidad: number };
    return (
      <Box
        sx={{
          backgroundColor: 'background.paper',
          p: 1.5,
          border: 1,
          borderColor: 'divider',
          borderRadius: 1,
          boxShadow: 2,
        }}
      >
        <Typography variant="body2" sx={{ fontWeight: 600, mb: 0.5 }}>
          {data.hora}:00
        </Typography>
        <Typography variant="body2" color="primary" sx={{ fontWeight: 600 }}>
          {formatCurrency(payload[0].value as number)}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          {data.cantidad} venta(s)
        </Typography>
      </Box>
    );
  }
  return null;
};

export function SalesChart({ data }: SalesChartProps) {
  // Formatear datos para el gráfico
  const chartData = data.map((item) => ({
    hora: item.hora,
    total: item.total,
    cantidad: item.cantidad,
  }));

  return (
    <Card>
      <CardContent>
        <Typography variant="h6" sx={{ mb: 3, fontWeight: 600 }}>
          Ventas por Hora
        </Typography>

        {chartData.length === 0 ? (
          <Box
            sx={{
              height: 300,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <Typography color="text.secondary">
              No hay datos de ventas para hoy
            </Typography>
          </Box>
        ) : (
          <ResponsiveContainer width="100%" height={300}>
            <LineChart data={chartData}>
              <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
              <XAxis
                dataKey="hora"
                tickFormatter={(value) => `${value}:00`}
                stroke="#999"
              />
              <YAxis
                tickFormatter={(value) => `$${(value / 1000).toFixed(0)}k`}
                stroke="#999"
              />
              <Tooltip content={<CustomTooltip />} />
              <Line
                type="monotone"
                dataKey="total"
                stroke="#1976d2"
                strokeWidth={3}
                dot={{ fill: '#1976d2', r: 4 }}
                activeDot={{ r: 6 }}
              />
            </LineChart>
          </ResponsiveContainer>
        )}
      </CardContent>
    </Card>
  );
}
