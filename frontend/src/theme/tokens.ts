// Design tokens centralizados — única fuente de verdad de colores y espaciado.
// Todos los componentes importan de aquí, nunca hardcodean hex.

export const sincoColors = {
  brand: {
    900: '#0D2F5E',
    800: '#1565c0',
    700: '#1976d2',
    600: '#1E88E5',
    500: '#2196F3',
    100: '#BBDEFB',
    50:  '#E3F2FD',
  },
  success: { main: '#2e7d32', light: '#4caf50', dark: '#1b5e20', bg: '#F1F8E9' },
  warning: { main: '#E65100', light: '#ff9800', dark: '#BF360C', bg: '#FFF3E0' },
  error:   { main: '#c62828', light: '#ef5350', dark: '#b71c1c', bg: '#FFEBEE' },
  info:    { main: '#0277BD', light: '#03a9f4', dark: '#01579b', bg: '#E1F5FE' },
  surface: {
    page:    '#F5F7FA',
    paper:   '#FFFFFF',
    subtle:  '#F0F4F8',
    overlay: 'rgba(13, 47, 94, 0.06)',
  },
  text: {
    primary:   '#1A2332',
    secondary: '#4A5568',
    disabled:  '#A0AEC0',
    inverse:   '#FFFFFF',
  },
  gradients: {
    heroBlue:    'linear-gradient(135deg, #1565c0 0%, #0d47a1 50%, #01579b 100%)',
    heroSubtle:  'linear-gradient(135deg, #1976d2 0%, #1565c0 100%)',
    heroSuccess: 'linear-gradient(135deg, #2e7d32 0%, #1b5e20 100%)',
    heroWarning: 'linear-gradient(135deg, #E65100 0%, #BF360C 100%)',
  },
} as const;

export const sincoSpacing = {
  heroRadius:   '16px',
  cardRadius:   '12px',
  chipRadius:   '8px',
  drawerWidth:  260,
  appBarHeight: 64,
} as const;
