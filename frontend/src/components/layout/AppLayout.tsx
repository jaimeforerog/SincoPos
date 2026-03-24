import { useState } from 'react';
import { Outlet, useNavigate } from 'react-router-dom';
import {
  AppBar,
  Box,
  Chip,
  Drawer,
  IconButton,
  Toolbar,
  Typography,
  Avatar,
  Menu,
  MenuItem,
  Divider,
  useTheme,
  useMediaQuery,
  ListItemIcon,
  Select,
} from '@mui/material';
import {
  Menu as MenuIcon,
  Settings,
  Logout,
} from '@mui/icons-material';
import { useAuth } from '@/hooks/useAuth';
import { useAuthStore } from '@/stores/auth.store';
import { useUiConfig } from '@/hooks/useUiConfig';
import { sincoColors } from '@/theme/tokens';
import { APP_NAME } from '@/utils/constants';
import { MenuSection } from './MenuSection';
import { menuSections } from './menuSections';
import { NotificationBell } from '@/components/common/NotificationBell';

const DRAWER_WIDTH = 260;

export function AppLayout() {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const [drawerOpen, setDrawerOpen] = useState(!isMobile);
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
  const navigate = useNavigate();
  const { user } = useAuth();
  const { activeSucursalId, setActiveSucursal, activeEmpresaId, empresasDisponibles, setActiveEmpresa, logout } = useAuthStore();

  // Empresa activa — fallback desde la sucursal activa si activeEmpresaId no está seteado
  const activeSucursalInfo = user?.sucursalesDisponibles.find(s => s.id === activeSucursalId);
  const empresaActiva =
    empresasDisponibles.find(e => e.id === activeEmpresaId) ??
    (activeSucursalInfo?.empresaId
      ? { id: activeSucursalInfo.empresaId, nombre: activeSucursalInfo.empresaNombre ?? `Empresa ${activeSucursalInfo.empresaId}` }
      : empresasDisponibles[0] ?? null);

  // Sucursales visibles = las de la empresa activa (o todas si no hay empresa configurada)
  const sucursalesVisibles = user?.sucursalesDisponibles.filter(
    s => activeEmpresaId == null || s.empresaId === activeEmpresaId || s.empresaId == null
  ) ?? [];

  // Nombre de sucursal activa con fallback robusto
  const sucursalActivaNombre =
    sucursalesVisibles.find(s => s.id === activeSucursalId)?.nombre ??
    activeSucursalInfo?.nombre ??
    user?.sucursalNombre ??
    'Sin sucursal';

  const uiConfig = useUiConfig();

  const handleDrawerToggle = () => {
    setDrawerOpen(!drawerOpen);
  };

  const handleProfileMenuOpen = (event: React.MouseEvent<HTMLElement>) => {
    setAnchorEl(event.currentTarget);
  };

  const handleProfileMenuClose = () => {
    setAnchorEl(null);
  };

  const handleNavigate = (path: string) => {
    navigate(path);
    if (isMobile) {
      setDrawerOpen(false);
    }
  };

  const handleLogout = () => {
    handleProfileMenuClose();
    logout();
  };

  const drawer = (
    <Box sx={{ overflowY: 'auto' }}>
      <Toolbar
        sx={{
          background: sincoColors.gradients.heroBlue,
          color: 'white',
          gap: 1,
          justifyContent: 'space-between',
        }}
      >
        <Typography variant="h6" fontWeight={700} color="white">
          {APP_NAME}
        </Typography>
        <Chip
          label={uiConfig.rolLabel}
          size="small"
          sx={{
            bgcolor: 'rgba(255,255,255,0.15)',
            color: 'white',
            fontSize: '0.7rem',
            fontWeight: 600,
            border: '1px solid rgba(255,255,255,0.25)',
          }}
        />
      </Toolbar>
      <Divider />

      {/* Renderizar todas las secciones del menú */}
      {menuSections.map((section, index) => (
        <MenuSection key={index} section={section} />
      ))}
    </Box>
  );

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      <AppBar
        position="fixed"
        sx={{
          zIndex: theme.zIndex.drawer + 1,
        }}
      >
        <Toolbar>
          <IconButton
            color="inherit"
            edge="start"
            onClick={handleDrawerToggle}
            sx={{ mr: 2 }}
          >
            <MenuIcon />
          </IconButton>

          <Box sx={{ flexGrow: 1, display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}>
            {/* Selector de empresa (múltiples) o nombre fijo (una sola) */}
            {empresaActiva && (
              <>
                {empresasDisponibles.length > 1 ? (
                  <Select
                    value={activeEmpresaId ?? ''}
                    onChange={(e) => setActiveEmpresa(Number(e.target.value))}
                    variant="standard"
                    disableUnderline
                    sx={{
                      color: 'rgba(255,255,255,0.9)',
                      fontWeight: 500,
                      fontSize: '0.875rem',
                      flexShrink: 0,
                      '& .MuiSvgIcon-root': { color: 'rgba(255,255,255,0.7)' },
                      '& .MuiSelect-select': { py: 0 },
                    }}
                  >
                    {empresasDisponibles.map((e) => (
                      <MenuItem key={e.id} value={e.id}>{e.nombre}</MenuItem>
                    ))}
                  </Select>
                ) : (
                  <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.9)', fontWeight: 500, flexShrink: 0 }}>
                    {empresaActiva.nombre}
                  </Typography>
                )}
                <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.5)', flexShrink: 0 }}>›</Typography>
              </>
            )}

            {/* Selector de sucursal */}
            {sucursalesVisibles.length > 1 ? (
              <Select
                value={activeSucursalId ?? ''}
                onChange={(e) => setActiveSucursal(Number(e.target.value))}
                variant="standard"
                disableUnderline
                sx={{
                  color: 'white',
                  fontWeight: 600,
                  fontSize: '1.1rem',
                  '& .MuiSvgIcon-root': { color: 'white' },
                  '& .MuiSelect-select': { py: 0 },
                }}
              >
                {sucursalesVisibles.map((s) => (
                  <MenuItem key={s.id} value={s.id}>{s.nombre}</MenuItem>
                ))}
              </Select>
            ) : (
              <Typography variant="h6" component="div">
                {sucursalActivaNombre}
              </Typography>
            )}
          </Box>

          <NotificationBell />

          <IconButton
            onClick={handleProfileMenuOpen}
            sx={{ p: 0 }}
          >
            <Avatar sx={{ bgcolor: theme.palette.secondary.main }}>
              {user?.nombre?.charAt(0).toUpperCase() || 'U'}
            </Avatar>
          </IconButton>

          <Menu
            anchorEl={anchorEl}
            open={Boolean(anchorEl)}
            onClose={handleProfileMenuClose}
            anchorOrigin={{
              vertical: 'bottom',
              horizontal: 'right',
            }}
            transformOrigin={{
              vertical: 'top',
              horizontal: 'right',
            }}
          >
            <Box sx={{ px: 2, py: 1 }}>
              <Typography variant="subtitle1" fontWeight={600}>
                {user?.nombre}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {user?.email}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                {user?.roles?.join(', ')}
              </Typography>
            </Box>
            <Divider />
            <MenuItem onClick={() => { handleProfileMenuClose(); handleNavigate('/configuracion'); }}>
              <ListItemIcon>
                <Settings fontSize="small" />
              </ListItemIcon>
              Configuración
            </MenuItem>
            <MenuItem onClick={handleLogout}>
              <ListItemIcon>
                <Logout fontSize="small" />
              </ListItemIcon>
              Cerrar Sesión
            </MenuItem>
          </Menu>
        </Toolbar>
      </AppBar>

      <Drawer
        variant={isMobile ? 'temporary' : 'persistent'}
        open={drawerOpen}
        onClose={handleDrawerToggle}
        sx={{
          width: DRAWER_WIDTH,
          flexShrink: 0,
          '& .MuiDrawer-paper': {
            width: DRAWER_WIDTH,
            boxSizing: 'border-box',
          },
        }}
      >
        {drawer}
      </Drawer>

      <Box
        component="main"
        sx={{
          flexGrow: 1,
          p: 3,
          width: { md: `calc(100% - ${drawerOpen ? DRAWER_WIDTH : 0}px)` },
          transition: theme.transitions.create(['width', 'margin'], {
            easing: theme.transitions.easing.sharp,
            duration: theme.transitions.duration.leavingScreen,
          }),
          mt: 8,
        }}
      >
        <Outlet />
      </Box>
    </Box>
  );
}
