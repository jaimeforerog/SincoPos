import { Container, Typography, Box, CircularProgress, Alert } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import AttachMoneyIcon from '@mui/icons-material/AttachMoney';
import ShoppingCartIcon from '@mui/icons-material/ShoppingCart';
import InventoryIcon from '@mui/icons-material/Inventory';
import PeopleIcon from '@mui/icons-material/People';
import ReceiptIcon from '@mui/icons-material/Receipt';
import TrendingUpIcon from '@mui/icons-material/TrendingUp';
import { reportesApi } from '@/api/reportes';
import { MetricCard } from '../components/MetricCard';
import { SalesChart } from '../components/SalesChart';
import { TopProductsTable } from '../components/TopProductsTable';
import { StockAlertsTable } from '../components/StockAlertsTable';
import { useAuth } from '@/hooks/useAuth';

const formatCurrency = (value: number) => {
  return new Intl.NumberFormat('es-CO', {
    style: 'currency',
    currency: 'COP',
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  }).format(value);
};

export function DashboardPage() {
  const { user } = useAuth();

  // Cargar datos del dashboard
  const { data: dashboard, isLoading, error } = useQuery({
    queryKey: ['dashboard', user?.sucursalId],
    queryFn: () => reportesApi.dashboard({ sucursalId: user?.sucursalId }),
    refetchInterval: 60000, // Refrescar cada 60 segundos
  });

  if (isLoading) {
    return (
      <Container maxWidth="xl">
        <Box
          sx={{
            display: 'flex',
            justifyContent: 'center',
            alignItems: 'center',
            minHeight: '60vh',
          }}
        >
          <CircularProgress size={60} />
        </Box>
      </Container>
    );
  }

  if (error) {
    return (
      <Container maxWidth="xl">
        <Alert severity="error" sx={{ mt: 3 }}>
          Error al cargar el dashboard. Por favor, intenta de nuevo.
        </Alert>
      </Container>
    );
  }

  if (!dashboard) return null;

  const metricas = dashboard.metricasDelDia;

  return (
    <Container maxWidth="xl">
      <Box sx={{ mb: 4 }}>
        <Typography variant="h4" sx={{ fontWeight: 700, mb: 1 }}>
          Dashboard
        </Typography>
        <Typography variant="body1" color="text.secondary">
          Resumen de ventas y métricas del día
        </Typography>
      </Box>

      {/* Métricas Principales */}
      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: {
            xs: '1fr',
            sm: 'repeat(2, 1fr)',
            md: 'repeat(3, 1fr)',
            lg: 'repeat(6, 1fr)',
          },
          gap: 3,
          mb: 4,
        }}
      >
        <MetricCard
          title="Ventas Totales"
          value={formatCurrency(metricas.ventasTotales)}
          icon={<AttachMoneyIcon />}
          color="#1976d2"
          change={metricas.porcentajeCambio}
        />
        <MetricCard
          title="Cantidad de Ventas"
          value={metricas.cantidadVentas}
          icon={<ShoppingCartIcon />}
          color="#2e7d32"
          subtitle={`${metricas.cantidadVentas} transacciones`}
        />
        <MetricCard
          title="Productos Vendidos"
          value={metricas.productosVendidos}
          icon={<InventoryIcon />}
          color="#ed6c02"
          subtitle={`${metricas.productosVendidos} unidades`}
        />
        <MetricCard
          title="Clientes Atendidos"
          value={metricas.clientesAtendidos}
          icon={<PeopleIcon />}
          color="#9c27b0"
          subtitle={`${metricas.clientesAtendidos} clientes`}
        />
        <MetricCard
          title="Ticket Promedio"
          value={formatCurrency(metricas.ticketPromedio)}
          icon={<ReceiptIcon />}
          color="#0288d1"
          subtitle="Por venta"
        />
        <MetricCard
          title="Margen Promedio"
          value={`${metricas.margenPromedio.toFixed(1)}%`}
          icon={<TrendingUpIcon />}
          color="#d32f2f"
          subtitle={formatCurrency(metricas.utilidadDelDia)}
        />
      </Box>

      {/* Gráfico de Ventas por Hora */}
      <Box sx={{ mb: 4 }}>
        <SalesChart data={dashboard.ventasPorHora} />
      </Box>

      {/* Top Productos y Alertas de Stock */}
      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: {
            xs: '1fr',
            lg: '58.33% 41.67%',
          },
          gap: 3,
        }}
      >
        <TopProductsTable products={dashboard.topProductos} />
        <StockAlertsTable alerts={dashboard.alertasStock} />
      </Box>
    </Container>
  );
}
