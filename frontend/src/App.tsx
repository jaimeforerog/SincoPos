import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { ThemeProvider, CssBaseline } from '@mui/material';
import { SnackbarProvider } from 'notistack';
import { AuthProvider } from './features/auth/AuthProvider';
import { LoginPage } from './features/auth/pages/LoginPage';
import { CallbackPage } from './features/auth/pages/CallbackPage';
import { UnauthorizedPage } from './features/auth/pages/UnauthorizedPage';
import { ProtectedRoute } from './components/common/ProtectedRoute';
import { AppLayout } from './components/layout/AppLayout';
import {
  ReporteVentasPage,
  ReporteInventarioPage,
  ReporteCajaPage,
} from './features/reportes/pages';
import { DevolucionesPage } from './features/devoluciones/pages';
import { InventarioPage } from './features/inventario/pages';
import { SucursalesPage } from './features/sucursales/pages/SucursalesPage';
import { TrasladosPage } from './features/traslados/pages/TrasladosPage';
import { POSPage } from './features/pos/pages/POSPage';
import { CajasPage } from './features/cajas/pages/CajasPage';
import { VentasPage } from './features/ventas/pages/VentasPage';
import { ProductosPage } from './features/productos/pages/ProductosPage';
import { PreciosPage } from './features/precios/pages/PreciosPage';
import { CategoriasPage } from './features/categorias/pages/CategoriasPage';
import { ConfiguracionPage } from './features/configuracion/pages/ConfiguracionPage';
import { ComprasPage } from './features/compras/pages/ComprasPage';
import { DashboardPage } from './features/dashboard/pages/DashboardPage';
import ImpuestosPage from './features/impuestos/pages/ImpuestosPage';
import { TercerosPage } from './features/terceros/pages';
import { UsuariosPage } from './features/usuarios/pages/UsuariosPage';
import { theme } from './theme/theme';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      refetchOnWindowFocus: false,
      retry: 1,
      staleTime: 5 * 60 * 1000, // 5 minutes
    },
  },
});

function App() {
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
          <AuthProvider>
            <BrowserRouter>
              <Routes>
                <Route path="/login" element={<LoginPage />} />
                <Route path="/callback" element={<CallbackPage />} />
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
                  <Route path="inventario" element={<InventarioPage />} />
                  <Route path="traslados" element={<ProtectedRoute requiredRoles={['supervisor', 'admin']}><TrasladosPage /></ProtectedRoute>} />
                  <Route path="ventas" element={<VentasPage />} />
                  <Route path="devoluciones" element={<ProtectedRoute requiredRoles={['supervisor', 'admin']}><DevolucionesPage /></ProtectedRoute>} />
                  <Route path="cajas" element={<ProtectedRoute requiredRoles={['cajero', 'supervisor', 'admin']}><CajasPage /></ProtectedRoute>} />
                  <Route path="sucursales" element={<ProtectedRoute requiredRoles={['supervisor', 'admin']}><SucursalesPage /></ProtectedRoute>} />
                  <Route path="reportes">
                    <Route index element={<Navigate to="/reportes/ventas" replace />} />
                    <Route path="ventas" element={<ReporteVentasPage />} />
                    <Route path="inventario" element={<ReporteInventarioPage />} />
                    <Route path="caja" element={<ReporteCajaPage />} />
                  </Route>
                  <Route
                    path="usuarios"
                    element={
                      <ProtectedRoute requiredRoles={['Admin']}>
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
                </Route>

                <Route path="/unauthorized" element={<UnauthorizedPage />} />
                <Route path="*" element={<Navigate to="/" replace />} />
              </Routes>
            </BrowserRouter>
          </AuthProvider>
        </SnackbarProvider>
      </ThemeProvider>
      <ReactQueryDevtools initialIsOpen={false} />
    </QueryClientProvider>
  );
}

export default App;
