import { Container, Box, Typography } from '@mui/material';
import {
  Timeline,
  Inventory,
  PointOfSale,
  Assessment,
  ManageSearch,
} from '@mui/icons-material';
import { useAuth } from '@/hooks/useAuth';
import { ReporteCard } from '../components/ReporteCard';
import { PageHeader } from '@/components/common/PageHeader';
import type { ReporteModule } from '../components/ReporteCard';

const reportModules: ReporteModule[] = [
  {
    id: 'ventas',
    title: 'Reporte de Ventas',
    description: 'Análisis de ventas por sucursal, método de pago y rango de fechas',
    icon: <Timeline fontSize="large" />,
    path: '/reportes/ventas',
    roles: ['supervisor', 'admin'],
  },
  {
    id: 'inventario',
    title: 'Inventario Valorizado',
    description: 'Resumen del valor total del stock actual en almacén por categoría',
    icon: <Inventory fontSize="large" />,
    path: '/reportes/inventario',
    roles: ['supervisor', 'admin'],
  },
  {
    id: 'caja',
    title: 'Reporte de Caja',
    description: 'Cuadre de caja, ingresos, egresos y movimientos de dinero',
    icon: <PointOfSale fontSize="large" />,
    path: '/reportes/caja',
    roles: ['supervisor', 'admin'],
  },
  {
    id: 'kardex',
    title: 'Kardex de Inventario',
    description: 'Historial detallado de entradas, salidas y saldos por producto',
    icon: <Assessment fontSize="large" />,
    path: '/reportes/kardex',
    roles: ['supervisor', 'admin'],
  },
  {
    id: 'auditoria',
    title: 'Auditoría de Actividad',
    description: 'Registro de la trazabilidad y eventos del sistema',
    icon: <ManageSearch fontSize="large" />,
    path: '/reportes/auditoria',
    roles: ['supervisor', 'admin'],
  },
];

export function ReportesHomePage() {
  const { hasAnyRole } = useAuth();

  const availableModules = reportModules.filter(
    (m) => !m.roles || hasAnyRole(m.roles)
  );

  return (
    <Container maxWidth="xl">
      <PageHeader
        title="Centro de Reportes y Análisis"
        breadcrumbs={[
          { label: 'Reportes' }
        ]}
      />

      <Typography variant="body1" color="text.secondary" sx={{ mb: 4 }}>
        Explora los indicadores y detalles financieros y operativos de tu negocio
      </Typography>

      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: {
            xs: '1fr',
            sm: 'repeat(2, 1fr)',
            md: 'repeat(3, 1fr)',
          },
          gap: 3,
        }}
      >
        {availableModules.map((module) => (
          <ReporteCard key={module.id} module={module} />
        ))}
      </Box>
    </Container>
  );
}
