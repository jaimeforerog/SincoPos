import { Container, Typography, Box, CircularProgress, Alert } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import AttachMoneyIcon from '@mui/icons-material/AttachMoney';
import ShoppingCartIcon from '@mui/icons-material/ShoppingCart';
import InventoryIcon from '@mui/icons-material/Inventory';
import PeopleIcon from '@mui/icons-material/People';
import ReceiptIcon from '@mui/icons-material/Receipt';
import TrendingUpIcon from '@mui/icons-material/TrendingUp';
import SyncProblemIcon from '@mui/icons-material/SyncProblem';
import { reportesApi } from '@/api/reportes';
import { lotesApi } from '@/api/lotes';
import { ventasApi } from '@/api/ventas';
import { MetricCard } from '../components/MetricCard';
import { SalesChart } from '../components/SalesChart';
import { TopProductsTable } from '../components/TopProductsTable';
import { StockAlertsTable } from '../components/StockAlertsTable';
import { AlertasVencimientoTable } from '../components/AlertasVencimientoTable';
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
  const { activeSucursalId } = useAuth();

  // Cargar datos del dashboard
  const { data: dashboard, isLoading, error } = useQuery({
    queryKey: ['dashboard', activeSucursalId],
    queryFn: () => reportesApi.dashboard({ sucursalId: activeSucursalId }),
    refetchInterval: 60000,
  });

  const { data: erpPendientes = 0 } = useQuery({
    queryKey: ['ventas', 'erp-pendientes', activeSucursalId],
    queryFn: () => ventasApi.getErpPendientesCount(activeSucursalId ?? undefined),
    refetchInterval: 30000,
  });

  const { data: alertasLotes = [] } = useQuery({
    queryKey: ['lotes', 'proximos-vencer', activeSucursalId],
    queryFn: () =>
      activeSucursalId
        ? lotesApi.proximosAVencer(activeSucursalId)
        : lotesApi.obtenerAlertas(),
    enabled: true,
    refetchInterval: 300000, // 5 minutos
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

  const HERO_COLOR = '#1565c0';

  return (
    <Container maxWidth="xl">
      {/* Hero */}
      <Box
        sx={{
          background: `linear-gradient(135deg, ${HERO_COLOR} 0%, #0d47a1 50%, #01579b 100%)`,
          borderRadius: 3,
          px: { xs: 3, md: 4 },
          py: { xs: 2.5, md: 3 },
          mb: 4,
          mt: 1,
          position: 'relative',
          overflow: 'hidden',
          '&::before': {
            content: '""', position: 'absolute', top: -60, right: -60,
            width: 200, height: 200, borderRadius: '50%', background: 'rgba(255,255,255,0.05)',
          },
          '&::after': {
            content: '""', position: 'absolute', bottom: -40, right: 80,
            width: 120, height: 120, borderRadius: '50%', background: 'rgba(255,255,255,0.05)',
          },
        }}
      >
        <Box sx={{ position: 'relative', zIndex: 1 }}>
          <Typography variant="h5" fontWeight={700} sx={{ color: '#fff', lineHeight: 1.2 }}>
            Dashboard
          </Typography>
          <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.75)', mt: 0.5 }}>
            Resumen de ventas y métricas del día
          </Typography>
        </Box>
      </Box>

      {/* Métricas Principales */}
      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: {
            xs: '1fr',
            sm: 'repeat(2, 1fr)',
            md: 'repeat(3, 1fr)',
            lg: 'repeat(7, 1fr)',
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
        <MetricCard
          title="Pendiente ERP"
          value={erpPendientes}
          icon={<SyncProblemIcon />}
          color={erpPendientes > 0 ? '#f57c00' : '#388e3c'}
          subtitle={erpPendientes > 0 ? 'Sin sincronizar' : 'Todo sincronizado'}
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
          gridTemplateColumns: { xs: '1fr', lg: '58.33% 41.67%' },
          gap: 3,
          mb: 3,
        }}
      >
        <TopProductsTable products={dashboard.topProductos} />
        <StockAlertsTable alerts={dashboard.alertasStock} />
      </Box>

      {/* Alertas de Vencimiento de Lotes */}
      {alertasLotes.length > 0 && (
        <AlertasVencimientoTable alertas={alertasLotes} />
      )}
    </Container>
  );
}
