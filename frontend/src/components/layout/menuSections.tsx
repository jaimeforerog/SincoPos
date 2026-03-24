import {
  Dashboard,
  PointOfSale,
  Receipt,
  ShoppingCart,
  SwapHoriz,
  KeyboardReturn,
  Assessment,
  Settings,
  ReceiptLong,
  Business,
  Inventory,
  Article,
  Hub,
  AccountTree,
} from '@mui/icons-material';
import { MenuSection } from './MenuSection';

export const menuSections: MenuSection[] = [
  // OPERACIONES BÁSICAS
  {
    title: 'Operaciones Básicas',
    roles: ['cajero', 'supervisor', 'admin'],
    items: [
      {
        text: 'Dashboard',
        icon: <Dashboard />,
        path: '/dashboard',
        roles: ['cajero', 'supervisor', 'admin'],
      },
      {
        text: 'Punto de Venta',
        icon: <PointOfSale />,
        path: '/pos',
        roles: ['cajero', 'supervisor', 'admin'],
      },
      {
        text: 'Ventas',
        icon: <Receipt />,
        path: '/ventas',
        roles: ['cajero', 'supervisor', 'admin'],
      },
    ],
  },

  // OPERACIONES ADMINISTRATIVAS
  {
    title: 'Operaciones Administrativas',
    roles: ['supervisor', 'admin'],
    items: [
      {
        text: 'Compras',
        icon: <ShoppingCart />,
        path: '/compras',
        roles: ['supervisor', 'admin'],
      },
      {
        text: 'Traslados',
        icon: <SwapHoriz />,
        path: '/traslados',
        roles: ['supervisor', 'admin'],
      },
      {
        text: 'Devoluciones',
        icon: <KeyboardReturn />,
        path: '/devoluciones',
        roles: ['supervisor', 'admin'],
      },
      {
        text: 'Inventario',
        icon: <Inventory />,
        path: '/reportes/gestion-inventario',
        roles: ['supervisor', 'admin'],
      },
      {
        text: 'Facturación DIAN',
        icon: <Article />,
        path: '/facturacion',
        roles: ['supervisor', 'admin'],
      },
    ],
  },

  // REPORTES
  {
    title: 'Reportes y Análisis',
    roles: ['supervisor', 'admin'],
    items: [
      {
        text: 'Reportes',
        icon: <Assessment />,
        path: '/reportes',
        roles: ['supervisor', 'admin'],
      },
      {
        text: 'Inteligencia Colectiva',
        icon: <Hub />,
        path: '/inteligencia',
        roles: ['supervisor', 'admin'],
      },
      {
        text: 'Monitor Pipeline',
        icon: <AccountTree />,
        path: '/pipeline',
        roles: ['supervisor', 'admin'],
      },
    ],
  },


  // CONFIGURACIÓN
  {
    title: 'Configuración',
    roles: ['supervisor', 'admin'],
    items: [
      {
        text: 'Configuración',
        icon: <Settings />,
        path: '/configuracion',
        roles: ['supervisor', 'admin'],
      },
      {
        text: 'Empresas',
        icon: <Business />,
        path: '/empresas',
        roles: ['admin'],
      },
    ],
  },
];
