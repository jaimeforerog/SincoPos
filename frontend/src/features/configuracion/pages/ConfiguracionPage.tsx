import { Container, Box, Typography } from '@mui/material';
import {
  StoreMallDirectory,
  PointOfSale,
  Groups,
  Inventory2,
  PriceChange,
  CategoryOutlined,
  Receipt,
  AdminPanelSettings,
  TuneOutlined,
  ManageSearch,
} from '@mui/icons-material';
import { useAuth } from '@/hooks/useAuth';
import { ConfigCard } from '../components/ConfigCard';
import { PageHeader } from '@/components/common/PageHeader';
import type { ConfigModule } from '../components/ConfigCard';

const configModules: ConfigModule[] = [
  // MAESTROS DE NEGOCIO
  {
    id: 'sucursales',
    title: 'Sucursales',
    description: 'Gestión de puntos de venta y ubicaciones físicas del negocio',
    icon: <StoreMallDirectory fontSize="large" />,
    path: '/sucursales',
    roles: ['admin'],
    category: 'negocio',
  },
  {
    id: 'cajas',
    title: 'Cajas',
    description: 'Configuración de cajas registradoras y control de apertura/cierre',
    icon: <PointOfSale fontSize="large" />,
    path: '/cajas',
    roles: ['supervisor', 'admin'],
    category: 'negocio',
  },
  {
    id: 'terceros',
    title: 'Terceros',
    description: 'Gestión de clientes, proveedores y terceros del sistema',
    icon: <Groups fontSize="large" />,
    path: '/terceros',
    roles: ['supervisor', 'admin'],
    category: 'negocio',
  },

  // CATÁLOGO DE PRODUCTOS
  {
    id: 'productos',
    title: 'Productos',
    description: 'Catálogo de productos, códigos de barras y costos',
    icon: <Inventory2 fontSize="large" />,
    path: '/productos',
    roles: ['supervisor', 'admin'],
    category: 'catalogo',
  },
  {
    id: 'precios',
    title: 'Precios Sucursal',
    description: 'Configuración de precios por sucursal e importación masiva',
    icon: <PriceChange fontSize="large" />,
    path: '/precios',
    roles: ['supervisor', 'admin'],
    category: 'catalogo',
  },
  {
    id: 'categorias',
    title: 'Categorías',
    description: 'Categorías de productos y márgenes de ganancia',
    icon: <CategoryOutlined fontSize="large" />,
    path: '/categorias',
    roles: ['supervisor', 'admin'],
    category: 'catalogo',
  },

  // CONFIGURACIÓN FISCAL Y SISTEMA
  {
    id: 'impuestos',
    title: 'Impuestos',
    description: 'Configuración de IVA, INC y otros impuestos aplicables',
    icon: <Receipt fontSize="large" />,
    path: '/impuestos',
    roles: ['admin'],
    category: 'sistema',
  },
  {
    id: 'usuarios',
    title: 'Usuarios',
    description: 'Gestión de usuarios del sistema y asignación de roles',
    icon: <AdminPanelSettings fontSize="large" />,
    path: '/usuarios',
    roles: ['admin'],
    category: 'sistema',
  },
  {
    id: 'auditoria',
    title: 'Auditoría',
    description: 'Registro de actividades del sistema: acciones por usuario, fecha y tipo',
    icon: <ManageSearch fontSize="large" />,
    path: '/auditoria',
    roles: ['supervisor', 'admin'],
    category: 'sistema',
  },
  {
    id: 'sistema',
    title: 'Sistema',
    description: 'Configuración avanzada, migraciones y parámetros del sistema',
    icon: <TuneOutlined fontSize="large" />,
    path: '/configuracion/sistema',
    roles: ['admin'],
    category: 'sistema',
  },
];

const categories = {
  negocio: 'Maestros de Negocio',
  catalogo: 'Catálogo de Productos',
  sistema: 'Configuración Fiscal y Sistema',
};

export function ConfiguracionPage() {
  const { hasAnyRole } = useAuth();

  return (
    <Container maxWidth="xl">
      <PageHeader
        title="Configuración del Sistema"
        breadcrumbs={[
          { label: 'Configuración' }
        ]}
      />

      <Typography variant="body1" color="text.secondary" sx={{ mb: 4 }}>
        Administra los datos maestros y configuración del sistema
      </Typography>

      {Object.entries(categories).map(([key, label]) => {
        const modules = configModules.filter(
          (m) =>
            m.category === key &&
            (!m.roles || hasAnyRole(m.roles))
        );

        if (modules.length === 0) return null;

        return (
          <Box key={key} sx={{ mb: 4 }}>
            <Typography
              variant="h6"
              sx={{ mb: 2, fontWeight: 600, color: 'text.primary' }}
            >
              {label}
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
              {modules.map((module) => (
                <ConfigCard key={module.id} module={module} />
              ))}
            </Box>
          </Box>
        );
      })}
    </Container>
  );
}
