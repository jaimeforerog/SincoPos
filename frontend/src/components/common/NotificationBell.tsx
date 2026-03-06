import { useState } from 'react';
import {
  Badge,
  IconButton,
  Popover,
  List,
  ListItem,
  ListItemIcon,
  ListItemText,
  Typography,
  Box,
  Button,
  Divider,
} from '@mui/material';
import {
  Notifications,
  CheckCircle,
  Warning,
  Error,
  Info,
} from '@mui/icons-material';
import { useNotifications } from '@/hooks/useNotifications';
import type { NotificacionDto } from '@/types/notifications';

function NivelIcon({ nivel }: { nivel: NotificacionDto['nivel'] }) {
  switch (nivel) {
    case 'success': return <CheckCircle sx={{ color: 'success.main' }} />;
    case 'warning': return <Warning sx={{ color: 'warning.main' }} />;
    case 'error': return <Error sx={{ color: 'error.main' }} />;
    default: return <Info sx={{ color: 'info.main' }} />;
  }
}

export function NotificationBell() {
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
  const { notifications, unreadCount, markAllRead } = useNotifications();

  const handleOpen = (e: React.MouseEvent<HTMLElement>) => {
    setAnchorEl(e.currentTarget);
    markAllRead();
  };

  const handleClose = () => setAnchorEl(null);

  const visible = notifications.slice(0, 10);

  return (
    <>
      <IconButton color="inherit" onClick={handleOpen} sx={{ mr: 1 }}>
        <Badge badgeContent={unreadCount > 99 ? '99+' : unreadCount} color="error">
          <Notifications />
        </Badge>
      </IconButton>

      <Popover
        open={Boolean(anchorEl)}
        anchorEl={anchorEl}
        onClose={handleClose}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        PaperProps={{ sx: { width: 360, maxHeight: 480 } }}
      >
        <Box sx={{ px: 2, py: 1, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <Typography variant="subtitle1" fontWeight={600}>Notificaciones</Typography>
          {notifications.length > 0 && (
            <Button size="small" onClick={() => { /* clear handled by markAllRead on open */ }}>
              Limpiar todo
            </Button>
          )}
        </Box>
        <Divider />

        {visible.length === 0 ? (
          <Box sx={{ p: 3, textAlign: 'center' }}>
            <Typography variant="body2" color="text.secondary">
              Sin notificaciones
            </Typography>
          </Box>
        ) : (
          <List dense disablePadding>
            {visible.map((n, i) => (
              <ListItem key={i} alignItems="flex-start" divider={i < visible.length - 1}>
                <ListItemIcon sx={{ minWidth: 36, mt: 0.5 }}>
                  <NivelIcon nivel={n.nivel} />
                </ListItemIcon>
                <ListItemText
                  primary={n.titulo}
                  secondary={
                    <>
                      <Typography component="span" variant="body2" display="block">
                        {n.mensaje}
                      </Typography>
                      <Typography component="span" variant="caption" color="text.secondary">
                        {new Date(n.timestamp).toLocaleTimeString('es-CO', {
                          hour: '2-digit', minute: '2-digit',
                        })}
                      </Typography>
                    </>
                  }
                />
              </ListItem>
            ))}
          </List>
        )}
      </Popover>
    </>
  );
}
