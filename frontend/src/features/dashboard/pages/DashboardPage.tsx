import { Container, Box, CircularProgress, Alert } from '@mui/material';
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
import { sugerenciasApi } from '@/api/sugerencias';
import { MetricCard } from '../components/MetricCard';
import { SalesChart } from '../components/SalesChart';
import { TopProductsTable } from '../components/TopProductsTable';
import { StockAlertsTable } from '../components/StockAlertsTable';
import { AlertasVencimientoTable } from '../components/AlertasVencimientoTable';
import { BusinessRadar } from '../components/BusinessRadar';
import { SugerenciasPanel } from '../components/SugerenciasPanel';
import { HeroBanner } from '@/components/common/HeroBanner';
import { useAuth } from '@/hooks/useAuth';
import { useUiConfig } from '@/hooks/useUiConfig';

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
  const { showDashboard } = useUiConfig();

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

  const { data: sugerencias = [] } = useQuery({
    queryKey: ['sugerencias', 'reabastecimiento', activeSucursalId],
    queryFn:  () => sugerenciasApi.getReabastecimiento(activeSucursalId!),
    enabled:  !!activeSucursalId && showDashboard,
    refetchInterval: 300000, // 5 minutos
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

  return (
    <Container maxWidth="xl">
      <HeroBanner
        title="Dashboard"
        subtitle="Resumen de ventas y métricas del día"
      />

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
        <Box sx={{ mb: 3 }}>
          <AlertasVencimientoTable alertas={alertasLotes} />
        </Box>
      )}

      {/* Capa 10 — Sugerencias inteligentes (solo Supervisor / Admin) */}
      {showDashboard && sugerencias.length > 0 && (
        <SugerenciasPanel sugerencias={sugerencias} />
      )}

      {/* Capa 14 — Radar de Negocio (solo Supervisor / Admin) */}
      {showDashboard && (
        <BusinessRadar
          metricas={metricas}
          ventasPorHora={dashboard.ventasPorHora}
          stockRisks={dashboard.alertasStock}
        />
      )}
    </Container>
  );
}
