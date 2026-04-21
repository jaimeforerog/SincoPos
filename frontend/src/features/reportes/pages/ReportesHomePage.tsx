import { Box, Container, Typography, Skeleton, Chip } from '@mui/material';
import {
  Timeline,
  Inventory,
  PointOfSale,
  Assessment,
  ManageSearch,
  Store,
  TrendingUp,
  ShoppingCart,
  AttachMoney,
  ReceiptLong,
  EventBusy,
} from '@mui/icons-material';
import { useQuery } from '@tanstack/react-query';
import { useAuth } from '@/hooks/useAuth';
import { reportesApi } from '@/api/reportes';
import { ReporteCard } from '../components/ReporteCard';
import type { ReporteModule } from '../components/ReporteCard';

const reportModules: ReporteModule[] = [
  {
    id: 'ventas',
    title: 'Reporte de Ventas',
    description: 'Análisis de ventas por sucursal, método de pago y rango de fechas',
    icon: <Timeline fontSize="large" />,
    path: '/reportes/ventas',
    roles: ['supervisor', 'admin'],
    color: '#1976d2',
  },
  {
    id: 'inventario',
    title: 'Inventario Valorizado',
    description: 'Resumen del valor total del stock actual en almacén por categoría',
    icon: <Inventory fontSize="large" />,
    path: '/reportes/inventario',
    roles: ['supervisor', 'admin'],
    color: '#ed6c02',
  },
  {
    id: 'gestion-inventario',
    title: 'Gestión de Inventario',
    description: 'Gestión de stock actual, registro de movimientos físicos y trazabilidad',
    icon: <Store fontSize="large" />,
    path: '/reportes/gestion-inventario',
    roles: ['cajero', 'supervisor', 'admin'],
    color: '#00796b',
  },
  {
    id: 'caja',
    title: 'Reporte de Caja',
    description: 'Cuadre de caja, ingresos, egresos y movimientos de dinero',
    icon: <PointOfSale fontSize="large" />,
    path: '/reportes/caja',
    roles: ['supervisor', 'admin'],
    color: '#2e7d32',
  },
  {
    id: 'kardex',
    title: 'Kardex de Inventario',
    description: 'Historial detallado de entradas, salidas y saldos por producto',
    icon: <Assessment fontSize="large" />,
    path: '/reportes/kardex',
    roles: ['supervisor', 'admin'],
    color: '#7b1fa2',
  },
  {
    id: 'auditoria',
    title: 'Auditoría de Actividad',
    description: 'Registro de la trazabilidad y eventos del sistema',
    icon: <ManageSearch fontSize="large" />,
    path: '/reportes/auditoria',
    roles: ['supervisor', 'admin'],
    color: '#c62828',
  },
  {
    id: 'auditoria-compras',
    title: 'Auditoría de Compras',
    description: 'Trazabilidad de aprobaciones, recepciones y devoluciones a proveedores',
    icon: <ShoppingCart fontSize="large" />,
    path: '/reportes/auditoria-compras',
    roles: ['supervisor', 'admin'],
    color: '#1565c0',
  },
  {
    id: 'lotes-vencimiento',
    title: 'Lotes y Vencimientos',
    description: 'Stock disponible por lote, fecha de vencimiento y estado de vigencia por sucursal',
    icon: <EventBusy fontSize="large" />,
    path: '/reportes/lotes-vencimiento',
    roles: ['supervisor', 'admin'],
    color: '#00695c',
  },
];

const formatCurrency = (value: number) =>
  new Intl.NumberFormat('es-CO', {
    style: 'currency',
    currency: 'COP',
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  }).format(value);

interface HeroStatProps {
  icon: React.ReactElement;
  label: string;
  value: string;
  loading: boolean;
}

function HeroStat({ icon, label, value, loading }: HeroStatProps) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
      <Box
        sx={{
          color: 'rgba(255,255,255,0.8)',
          display: 'flex',
          alignItems: 'center',
        }}
      >
        {icon}
      </Box>
      <Box>
        <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.7)', display: 'block', lineHeight: 1 }}>
          {label}
        </Typography>
        {loading ? (
          <Skeleton variant="text" width={80} sx={{ bgcolor: 'rgba(255,255,255,0.2)' }} />
        ) : (
          <Typography variant="subtitle1" fontWeight={700} sx={{ color: '#fff', lineHeight: 1.2 }}>
            {value}
          </Typography>
        )}
      </Box>
    </Box>
  );
}

export function ReportesHomePage() {
  const { hasAnyRole, activeSucursalId } = useAuth();

  const { data: dashboard, isLoading } = useQuery({
    queryKey: ['dashboard', activeSucursalId],
    queryFn: () => reportesApi.dashboard({ sucursalId: activeSucursalId }),
    refetchInterval: 60000,
  });

  const availableModules = reportModules.filter(
    (m) => !m.roles || hasAnyRole(m.roles)
  );

  const metricas = dashboard?.metricasDelDia;

  return (
    <Container maxWidth="xl">
      {/* Hero dinámico */}
      <Box
        sx={{
          background: 'linear-gradient(135deg, #1565c0 0%, #0d47a1 50%, #01579b 100%)',
          borderRadius: 3,
          px: { xs: 3, md: 4 },
          py: { xs: 2.5, md: 3 },
          mb: 3,
          mt: 1,
          position: 'relative',
          overflow: 'hidden',
          '&::before': {
            content: '""',
            position: 'absolute',
            top: -60,
            right: -60,
            width: 200,
            height: 200,
            borderRadius: '50%',
            background: 'rgba(255,255,255,0.05)',
          },
          '&::after': {
            content: '""',
            position: 'absolute',
            bottom: -40,
            right: 80,
            width: 120,
            height: 120,
            borderRadius: '50%',
            background: 'rgba(255,255,255,0.05)',
          },
        }}
      >
        <Box
          sx={{
            display: 'flex',
            flexDirection: { xs: 'column', md: 'row' },
            alignItems: { xs: 'flex-start', md: 'center' },
            justifyContent: 'space-between',
            gap: { xs: 2.5, md: 0 },
            position: 'relative',
            zIndex: 1,
          }}
        >
          {/* Título */}
          <Box>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5 }}>
              <Chip
                label="HOY"
                size="small"
                sx={{
                  bgcolor: 'rgba(255,255,255,0.2)',
                  color: '#fff',
                  fontWeight: 700,
                  fontSize: '0.65rem',
                  height: 20,
                }}
              />
            </Box>
            <Typography variant="h5" sx={{ color: '#fff', lineHeight: 1.2 }}>
              Centro de Reportes
            </Typography>
            <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.75)', mt: 0.5 }}>
              {availableModules.length} módulo{availableModules.length !== 1 ? 's' : ''} disponibles
            </Typography>
          </Box>

          {/* KPIs del día */}
          <Box
            sx={{
              display: 'flex',
              flexWrap: 'wrap',
              gap: { xs: 2.5, md: 4 },
              '& > *': {
                position: 'relative',
                '&:not(:last-child)::after': {
                  content: '""',
                  position: 'absolute',
                  right: { xs: 'unset', md: -16 },
                  top: '10%',
                  height: '80%',
                  width: '1px',
                  bgcolor: 'rgba(255,255,255,0.2)',
                  display: { xs: 'none', md: 'block' },
                },
              },
            }}
          >
            <HeroStat
              icon={<AttachMoney />}
              label="Ventas hoy"
              value={metricas ? formatCurrency(metricas.ventasTotales) : '—'}
              loading={isLoading}
            />
            <HeroStat
              icon={<ShoppingCart />}
              label="Transacciones"
              value={metricas ? `${metricas.cantidadVentas}` : '—'}
              loading={isLoading}
            />
            <HeroStat
              icon={<ReceiptLong />}
              label="Ticket promedio"
              value={metricas ? formatCurrency(metricas.ticketPromedio) : '—'}
              loading={isLoading}
            />
            <HeroStat
              icon={<TrendingUp />}
              label="Margen"
              value={metricas ? `${metricas.margenPromedio.toFixed(1)}%` : '—'}
              loading={isLoading}
            />
          </Box>
        </Box>
      </Box>

      {/* Grid de módulos */}
      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: {
            xs: '1fr',
            sm: 'repeat(2, 1fr)',
            md: 'repeat(3, 1fr)',
          },
          gap: 2.5,
        }}
      >
        {availableModules.map((module, index) => (
          <ReporteCard key={module.id} module={module} index={index} />
        ))}
      </Box>
    </Container>
  );
}
