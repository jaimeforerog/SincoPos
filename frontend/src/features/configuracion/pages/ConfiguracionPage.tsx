import { Container, Box, Typography, Chip } from '@mui/material';
import {
  StoreMallDirectory,
  PointOfSale,
  Groups,
  Inventory2,
  PriceChange,
  CategoryOutlined,
  Receipt,
  AdminPanelSettings,
  ReceiptLong,
  StorefrontOutlined,
  MenuBookOutlined,
  TuneOutlined,
  Shield,
  SettingsSuggest,
} from '@mui/icons-material';
import { useAuth } from '@/hooks/useAuth';
import { ConfigCard } from '../components/ConfigCard';
import type { ConfigModule } from '../components/ConfigCard';

const configModules: ConfigModule[] = [
  {
    id: 'sucursales',
    title: 'Sucursales',
    description: 'Gestión de puntos de venta y ubicaciones físicas del negocio',
    icon: <StoreMallDirectory fontSize="large" />,
    path: '/sucursales',
    roles: ['admin'],
    category: 'negocio',
    color: '#1565c0',
  },
  {
    id: 'cajas',
    title: 'Cajas',
    description: 'Configuración de cajas registradoras y control de apertura/cierre',
    icon: <PointOfSale fontSize="large" />,
    path: '/cajas',
    roles: ['supervisor', 'admin'],
    category: 'negocio',
    color: '#00796b',
  },
  {
    id: 'terceros',
    title: 'Terceros',
    description: 'Gestión de clientes, proveedores y terceros del sistema',
    icon: <Groups fontSize="large" />,
    path: '/terceros',
    roles: ['supervisor', 'admin'],
    category: 'negocio',
    color: '#7b1fa2',
  },
  {
    id: 'productos',
    title: 'Productos',
    description: 'Catálogo de productos, códigos de barras y costos',
    icon: <Inventory2 fontSize="large" />,
    path: '/productos',
    roles: ['supervisor', 'admin'],
    category: 'catalogo',
    color: '#ed6c02',
  },
  {
    id: 'precios',
    title: 'Precios Sucursal',
    description: 'Configuración de precios por sucursal e importación masiva',
    icon: <PriceChange fontSize="large" />,
    path: '/precios',
    roles: ['supervisor', 'admin'],
    category: 'catalogo',
    color: '#2e7d32',
  },
  {
    id: 'categorias',
    title: 'Categorías',
    description: 'Categorías de productos y márgenes de ganancia',
    icon: <CategoryOutlined fontSize="large" />,
    path: '/categorias',
    roles: ['supervisor', 'admin'],
    category: 'catalogo',
    color: '#f57f17',
  },
  {
    id: 'impuestos',
    title: 'Impuestos',
    description: 'Configuración de IVA, INC y otros impuestos aplicables',
    icon: <Receipt fontSize="large" />,
    path: '/impuestos',
    roles: ['admin'],
    category: 'sistema',
    color: '#c62828',
  },
  {
    id: 'usuarios',
    title: 'Usuarios',
    description: 'Gestión de usuarios del sistema y asignación de roles',
    icon: <AdminPanelSettings fontSize="large" />,
    path: '/usuarios',
    roles: ['admin'],
    category: 'sistema',
    color: '#283593',
  },
  {
    id: 'facturacion',
    title: 'Facturación Electrónica',
    description: 'Configuración del emisor DIAN: resolución, certificado digital y ambiente',
    icon: <ReceiptLong fontSize="large" />,
    path: '/configuracion/facturacion',
    roles: ['admin'],
    category: 'sistema',
    color: '#6a1b9a',
  },
  {
    id: 'eticas',
    title: 'Supervisión Ética',
    description: 'Reglas de negocio configurables: descuentos, montos y precios mínimos',
    icon: <Shield fontSize="large" />,
    path: '/eticas',
    roles: ['admin'],
    category: 'sistema',
    color: '#c62828',
  },
  {
    id: 'variables',
    title: 'Variables del Sistema',
    description: 'Parámetros globales: montos máximos, límites y valores configurables',
    icon: <SettingsSuggest fontSize="large" />,
    path: '/configuracion/variables',
    roles: ['admin'],
    category: 'sistema',
    color: '#37474f',
  },
];

const categoryMeta = {
  negocio: {
    label: 'Maestros de Negocio',
    icon: <StorefrontOutlined fontSize="small" />,
    color: '#1565c0',
  },
  catalogo: {
    label: 'Catálogo de Productos',
    icon: <MenuBookOutlined fontSize="small" />,
    color: '#ed6c02',
  },
  sistema: {
    label: 'Configuración Fiscal y Sistema',
    icon: <TuneOutlined fontSize="small" />,
    color: '#6a1b9a',
  },
} as const;

export function ConfiguracionPage() {
  const { hasAnyRole } = useAuth();

  const availableModules = configModules.filter(
    (m) => !m.roles || hasAnyRole(m.roles)
  );

  const countByCategory = (key: string) =>
    availableModules.filter((m) => m.category === key).length;

  let cardIndex = 0;

  return (
    <Container maxWidth="xl">
      {/* Hero */}
      <Box
        sx={{
          background: 'linear-gradient(135deg, #1565c0 0%, #0d47a1 50%, #01579b 100%)',
          borderRadius: 3,
          px: { xs: 3, md: 4 },
          py: { xs: 2.5, md: 3 },
          mb: 2,
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
                label={`${availableModules.length} MÓDULOS`}
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
            <Typography variant="h5" fontWeight={700} sx={{ color: '#fff', lineHeight: 1.2 }}>
              Configuración del Sistema
            </Typography>
            <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.75)', mt: 0.5 }}>
              Administra los datos maestros y parámetros del negocio
            </Typography>
          </Box>

          {/* Conteos por categoría */}
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
            {(Object.keys(categoryMeta) as Array<keyof typeof categoryMeta>).map((key) => {
              const count = countByCategory(key);
              if (count === 0) return null;
              const meta = categoryMeta[key];
              return (
                <Box key={key} sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
                  <Box sx={{ color: 'rgba(255,255,255,0.8)', display: 'flex', alignItems: 'center' }}>
                    {meta.icon}
                  </Box>
                  <Box>
                    <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.7)', display: 'block', lineHeight: 1 }}>
                      {meta.label.split(' ')[0]}
                    </Typography>
                    <Typography variant="subtitle1" fontWeight={700} sx={{ color: '#fff', lineHeight: 1.2 }}>
                      {count} módulo{count !== 1 ? 's' : ''}
                    </Typography>
                  </Box>
                </Box>
              );
            })}
          </Box>
        </Box>
      </Box>

      {/* Secciones por categoría */}
      {(Object.keys(categoryMeta) as Array<keyof typeof categoryMeta>).map((key) => {
        const modules = availableModules.filter((m) => m.category === key);
        if (modules.length === 0) return null;
        const meta = categoryMeta[key];

        return (
          <Box key={key} sx={{ mb: 2 }}>
            {/* Encabezado de sección */}
            <Box
              sx={{
                display: 'flex',
                alignItems: 'center',
                gap: 1,
                mb: 1,
                pb: 0.5,
                borderBottom: '2px solid',
                borderColor: meta.color,
              }}
            >
              <Box sx={{ color: meta.color, display: 'flex', alignItems: 'center' }}>
                {meta.icon}
              </Box>
              <Typography variant="body2" fontWeight={700} sx={{ color: 'text.primary' }}>
                {meta.label}
              </Typography>
            </Box>

            <Box
              sx={{
                display: 'grid',
                gridTemplateColumns: {
                  xs: '1fr',
                  sm: 'repeat(2, 1fr)',
                  md: 'repeat(3, 1fr)',
                  lg: 'repeat(4, 1fr)',
                },
                gap: 1.5,
              }}
            >
              {modules.map((module) => (
                <ConfigCard key={module.id} module={module} index={cardIndex++} />
              ))}
            </Box>
          </Box>
        );
      })}
    </Container>
  );
}
