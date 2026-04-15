import { lazy, Suspense, useEffect } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { ThemeProvider, CssBaseline, LinearProgress, Box } from '@mui/material';
import { SnackbarProvider } from 'notistack';
import { AuthProvider } from './features/auth/AuthProvider';
import { ErrorBoundary } from './components/common/ErrorBoundary';
import { LoginPage } from './features/auth/pages/LoginPage';
import { CallbackPage } from './features/auth/pages/CallbackPage';
import { UnauthorizedPage } from './features/auth/pages/UnauthorizedPage';
import { ProtectedRoute } from './components/common/ProtectedRoute';
import { AppLayout } from './components/layout/AppLayout';
import { SeleccionarEmpresaDialog } from './components/common/SeleccionarEmpresaDialog';
import { theme } from './theme/theme';

// Rutas con mayor frecuencia de uso — carga diferida con prefetch inmediato
const DashboardPage = lazy(() => import('./features/dashboard/pages/DashboardPage').then(m => ({ default: m.DashboardPage })));
const POSPage = lazy(() => import('./features/pos/pages/POSPage').then(m => ({ default: m.POSPage })));
const VentasPage = lazy(() => import('./features/ventas/pages/VentasPage').then(m => ({ default: m.VentasPage })));
const CajasPage = lazy(() => import('./features/cajas/pages/CajasPage').then(m => ({ default: m.CajasPage })));
const InventarioPage = lazy(() => import('./features/inventario/pages/InventarioPage').then(m => ({ default: m.InventarioPage })));

// Rutas secundarias — carga diferida
const ReporteVentasPage = lazy(() => import('./features/reportes/pages/ReporteVentasPage').then(m => ({ default: m.ReporteVentasPage })));
const ReporteInventarioPage = lazy(() => import('./features/reportes/pages/ReporteInventarioPage').then(m => ({ default: m.ReporteInventarioPage })));
const ReporteCajaPage = lazy(() => import('./features/reportes/pages/ReporteCajaPage').then(m => ({ default: m.ReporteCajaPage })));
const ReportesHomePage = lazy(() => import('./features/reportes/pages/ReportesHomePage').then(m => ({ default: m.ReportesHomePage })));
const ReporteKardexPage = lazy(() => import('./features/reportes/pages/ReporteKardexPage').then(m => ({ default: m.ReporteKardexPage })));
const DevolucionesPage = lazy(() => import('./features/devoluciones/pages').then(m => ({ default: m.DevolucionesPage })));
const SucursalesPage = lazy(() => import('./features/sucursales/pages/SucursalesPage').then(m => ({ default: m.SucursalesPage })));
const TrasladosPage = lazy(() => import('./features/traslados/pages/TrasladosPage').then(m => ({ default: m.TrasladosPage })));
const ProductosPage = lazy(() => import('./features/productos/pages/ProductosPage').then(m => ({ default: m.ProductosPage })));
const PreciosPage = lazy(() => import('./features/precios/pages/PreciosPage').then(m => ({ default: m.PreciosPage })));
const CategoriasPage = lazy(() => import('./features/categorias/pages/CategoriasPage').then(m => ({ default: m.CategoriasPage })));
const ConfiguracionPage = lazy(() => import('./features/configuracion/pages/ConfiguracionPage').then(m => ({ default: m.ConfiguracionPage })));
const ComprasPage = lazy(() => import('./features/compras/pages/ComprasPage').then(m => ({ default: m.ComprasPage })));
const NuevaOrdenCompraPage = lazy(() => import('./features/compras/pages/NuevaOrdenCompraPage').then(m => ({ default: m.NuevaOrdenCompraPage })));
const DevolucionesCompraPage = lazy(() => import('./features/compras/pages/DevolucionesCompraPage').then(m => ({ default: m.DevolucionesCompraPage })));
const ImpuestosPage = lazy(() => import('./features/impuestos/pages/ImpuestosPage'));
const TercerosPage = lazy(() => import('./features/terceros/pages/TercerosPage'));
const UsuariosPage = lazy(() => import('./features/usuarios/pages/UsuariosPage').then(m => ({ default: m.UsuariosPage })));
const AuditoriaPage = lazy(() => import('./features/auditoria/pages/AuditoriaPage'));
const EmpresasPage = lazy(() => import('./features/empresas/pages/EmpresasPage').then(m => ({ default: m.EmpresasPage })));
const ConfiguracionEmisorPage = lazy(() => import('./features/facturacion/pages/ConfiguracionEmisorPage').then(m => ({ default: m.ConfiguracionEmisorPage })));
const DocumentosElectronicosPage = lazy(() => import('./features/facturacion/pages/DocumentosElectronicosPage').then(m => ({ default: m.DocumentosElectronicosPage })));
const ReglasEticasPage = lazy(() => import('./features/eticas/pages/ReglasEticasPage').then(m => ({ default: m.ReglasEticasPage })));
const ConfiguracionVariablesPage = lazy(() => import('./features/configuracion-variables/pages/ConfiguracionVariablesPage').then(m => ({ default: m.ConfiguracionVariablesPage })));
const InteligenciaColectivaPage = lazy(() => import('./features/inteligencia/pages/InteligenciaColectivaPage').then(m => ({ default: m.InteligenciaColectivaPage })));
const PipelineMonitorPage = lazy(() => import('./features/orquestador/pages/PipelineMonitorPage').then(m => ({ default: m.PipelineMonitorPage })));

function PageLoader() {
  return (
    <Box sx={{ width: '100%', position: 'fixed', top: 0, left: 0, zIndex: 9999 }}>
      <LinearProgress />
    </Box>
  );
}

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      refetchOnWindowFocus: false,
      retry: 1,
      staleTime: 5 * 60 * 1000, // 5 minutes
    },
  },
});

/** Prefetch de los chunks más usados una vez que la app está lista. */
function useCriticalPrefetch() {
  useEffect(() => {
    const prefetch = () => {
      void import('./features/pos/pages/POSPage');
      void import('./features/dashboard/pages/DashboardPage');
      void import('./features/ventas/pages/VentasPage');
    };
    if ('requestIdleCallback' in window) {
      const id = requestIdleCallback(prefetch, { timeout: 3000 });
      return () => cancelIdleCallback(id);
    }
    const id = setTimeout(prefetch, 2000);
    return () => clearTimeout(id);
  }, []);
}

/**
 * All routes that require the WorkOS SDK (AuthKitProvider).
 * /callback is intentionally excluded — placing it outside AuthProvider
 * prevents the SDK from intercepting the ?code= and attempting its own
 * (CORS-blocked) token exchange before our backend can handle it.
 */
function AuthenticatedApp() {
  return (
    <AuthProvider>
      <SeleccionarEmpresaDialog />
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route
          path="/"
          element={
            <ProtectedRoute>
              <AppLayout />
            </ProtectedRoute>
          }
        >
          <Route index element={<Navigate to="/dashboard" replace />} />
          <Route path="dashboard" element={<DashboardPage />} />
          <Route path="pos" element={<POSPage />} />
          <Route path="productos" element={<ProtectedRoute requiredRoles={['supervisor', 'admin']}><ProductosPage /></ProtectedRoute>} />
          <Route path="precios" element={<ProtectedRoute requiredRoles={['supervisor', 'admin']}><PreciosPage /></ProtectedRoute>} />
          <Route path="compras" element={<ProtectedRoute requiredRoles={['supervisor', 'admin']}><ComprasPage /></ProtectedRoute>} />
          <Route path="compras/nueva" element={<ProtectedRoute requiredRoles={['supervisor', 'admin']}><NuevaOrdenCompraPage /></ProtectedRoute>} />
          <Route path="compras/devoluciones" element={<ProtectedRoute requiredRoles={['supervisor', 'admin']}><DevolucionesCompraPage /></ProtectedRoute>} />
          <Route path="traslados" element={<ProtectedRoute requiredRoles={['supervisor', 'admin']}><TrasladosPage /></ProtectedRoute>} />
          <Route path="ventas" element={<VentasPage />} />
          <Route path="devoluciones" element={<ProtectedRoute requiredRoles={['supervisor', 'admin']}><DevolucionesPage /></ProtectedRoute>} />
          <Route path="cajas" element={<ProtectedRoute requiredRoles={['cajero', 'supervisor', 'admin']}><CajasPage /></ProtectedRoute>} />
          <Route path="sucursales" element={<ProtectedRoute requiredRoles={['supervisor', 'admin']}><SucursalesPage /></ProtectedRoute>} />
          <Route path="reportes">
            <Route index element={<ReportesHomePage />} />
            <Route path="ventas" element={<ReporteVentasPage />} />
            <Route path="inventario" element={<ReporteInventarioPage />} />
            <Route path="gestion-inventario" element={<InventarioPage />} />
            <Route path="caja" element={<ReporteCajaPage />} />
            <Route path="kardex" element={<ReporteKardexPage />} />
            <Route path="auditoria" element={<ProtectedRoute requiredRoles={['supervisor', 'admin']}><AuditoriaPage /></ProtectedRoute>} />
          </Route>
          <Route
            path="usuarios"
            element={
              <ProtectedRoute requiredRoles={['admin']}>
                <UsuariosPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="configuracion"
            element={
              <ProtectedRoute requiredRoles={['supervisor', 'admin']}>
                <ConfiguracionPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="configuracion/sistema"
            element={
              <ProtectedRoute requiredRoles={['supervisor', 'admin']}>
                <div>Sistema - En construcción</div>
              </ProtectedRoute>
            }
          />
          <Route
            path="categorias"
            element={
              <ProtectedRoute requiredRoles={['supervisor', 'admin']}>
                <CategoriasPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="impuestos"
            element={
              <ProtectedRoute requiredRoles={['supervisor', 'admin']}>
                <ImpuestosPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="terceros"
            element={
              <ProtectedRoute requiredRoles={['supervisor', 'admin']}>
                <TercerosPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="facturacion"
            element={
              <ProtectedRoute requiredRoles={['supervisor', 'admin']}>
                <DocumentosElectronicosPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="configuracion/facturacion"
            element={
              <ProtectedRoute requiredRoles={['admin']}>
                <ConfiguracionEmisorPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="empresas"
            element={
              <ProtectedRoute requiredRoles={['admin']}>
                <EmpresasPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="eticas"
            element={
              <ProtectedRoute requiredRoles={['admin']}>
                <ReglasEticasPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="configuracion/variables"
            element={
              <ProtectedRoute requiredRoles={['admin']}>
                <ConfiguracionVariablesPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="inteligencia"
            element={
              <ProtectedRoute requiredRoles={['supervisor', 'admin']}>
                <InteligenciaColectivaPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="pipeline"
            element={
              <ProtectedRoute requiredRoles={['supervisor', 'admin']}>
                <PipelineMonitorPage />
              </ProtectedRoute>
            }
          />
        </Route>

        <Route path="/unauthorized" element={<UnauthorizedPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </AuthProvider>
  );
}

function App() {
  useCriticalPrefetch();
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <SnackbarProvider
          maxSnack={3}
          anchorOrigin={{
            vertical: 'top',
            horizontal: 'right',
          }}
        >
          <BrowserRouter>
            <ErrorBoundary>
              <Suspense fallback={<PageLoader />}>
                <Routes>
                  {/* /callback must be outside AuthProvider so the WorkOS SDK
                      never initializes on this URL and cannot intercept the code. */}
                  <Route path="/callback" element={<CallbackPage />} />
                  <Route path="*" element={<AuthenticatedApp />} />
                </Routes>
              </Suspense>
            </ErrorBoundary>
          </BrowserRouter>
        </SnackbarProvider>
      </ThemeProvider>
      <ReactQueryDevtools initialIsOpen={false} />
    </QueryClientProvider>
  );
}

export default App;
