import {
  Dashboard,
  PointOfSale,
  Receipt,
  Store,
  ShoppingCart,
  SwapHoriz,
  KeyboardReturn,
  Assessment,
  Settings,
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
      {
        text: 'Inventario',
        icon: <Store />,
        path: '/inventario',
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
    ],
  },
];
