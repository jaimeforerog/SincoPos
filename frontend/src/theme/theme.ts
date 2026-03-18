import { createTheme } from '@mui/material/styles';
import { sincoColors, sincoSpacing } from './tokens';

export const theme = createTheme({
  palette: {
    primary: {
      main:         sincoColors.brand[800],
      light:        sincoColors.brand[600],
      dark:         sincoColors.brand[900],
      contrastText: sincoColors.text.inverse,
    },
    secondary: {
      main:         sincoColors.brand[900],
      light:        sincoColors.brand[700],
      dark:         '#061829',
      contrastText: sincoColors.text.inverse,
    },
    error:   { main: sincoColors.error.main,   light: sincoColors.error.light,   dark: sincoColors.error.dark },
    warning: { main: sincoColors.warning.main, light: sincoColors.warning.light, dark: sincoColors.warning.dark },
    info:    { main: sincoColors.info.main,    light: sincoColors.info.light,    dark: sincoColors.info.dark },
    success: { main: sincoColors.success.main, light: sincoColors.success.light, dark: sincoColors.success.dark },
    background: {
      default: sincoColors.surface.page,
      paper:   sincoColors.surface.paper,
    },
    text: {
      primary:   sincoColors.text.primary,
      secondary: sincoColors.text.secondary,
      disabled:  sincoColors.text.disabled,
    },
  },

  typography: {
    fontFamily: [
      '-apple-system',
      'BlinkMacSystemFont',
      '"Segoe UI"',
      'Roboto',
      'sans-serif',
    ].join(','),
    h1: { fontSize: '2rem',    fontWeight: 700, letterSpacing: '-0.01em' },
    h2: { fontSize: '1.75rem', fontWeight: 600, letterSpacing: '-0.01em' },
    h3: { fontSize: '1.5rem',  fontWeight: 600 },
    h4: { fontSize: '1.25rem', fontWeight: 600 },
    h5: { fontSize: '1.1rem',  fontWeight: 600 },
    h6: { fontSize: '1rem',    fontWeight: 600 },
    body1:   { fontSize: '0.9375rem', lineHeight: 1.6 },
    body2:   { fontSize: '0.875rem',  lineHeight: 1.5 },
    caption: { fontSize: '0.75rem',   letterSpacing: '0.02em' },
  },

  shape: { borderRadius: 10 },

  components: {
    MuiButton: {
      styleOverrides: {
        root: {
          textTransform: 'none',
          fontWeight: 600,
          borderRadius: sincoSpacing.chipRadius,
        },
        containedPrimary: {
          background: sincoColors.gradients.heroSubtle,
          '&:hover': { background: sincoColors.gradients.heroBlue },
        },
      },
    },

    MuiCard: {
      styleOverrides: {
        root: {
          borderRadius: sincoSpacing.cardRadius,
          boxShadow: '0 1px 3px rgba(0,0,0,0.08), 0 1px 2px rgba(0,0,0,0.04)',
          border: '1px solid rgba(0,0,0,0.06)',
        },
      },
    },

    MuiPaper: {
      styleOverrides: {
        root: {
          borderRadius: sincoSpacing.cardRadius,
          boxShadow: '0 1px 3px rgba(0,0,0,0.08)',
        },
      },
    },

    MuiAppBar: {
      styleOverrides: {
        root: {
          background: sincoColors.gradients.heroBlue,
          boxShadow: '0 2px 8px rgba(13,47,94,0.3)',
        },
      },
    },

    MuiDrawer: {
      styleOverrides: {
        paper: {
          background: sincoColors.surface.paper,
          color: sincoColors.text.primary,
          borderRight: `1px solid rgba(0,0,0,0.08)`,
          boxShadow: '2px 0 8px rgba(13,47,94,0.08)',
        },
      },
    },

    MuiChip: {
      styleOverrides: {
        root: { borderRadius: sincoSpacing.chipRadius, fontWeight: 500 },
      },
    },

    MuiAlert: {
      styleOverrides: {
        root: { borderRadius: sincoSpacing.chipRadius },
      },
    },
  },
});
