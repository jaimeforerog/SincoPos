import { ListItem, ListItemButton, ListItemIcon, ListItemText, Box } from '@mui/material';
import { useLocation } from 'react-router-dom';

interface MenuItemProps {
  text: string;
  icon: React.ReactElement;
  path?: string;
  onClick: () => void;
}

export function MenuItem({ text, icon, path, onClick }: MenuItemProps) {
  const { pathname } = useLocation();
  const isActive = path ? pathname === path || pathname.startsWith(path + '/') : false;

  return (
    <ListItem disablePadding sx={{ px: 0.75, mb: 0.25 }}>
      <ListItemButton
        onClick={onClick}
        selected={isActive}
        sx={{
          borderRadius: 1.5,
          py: 0.6,
          px: 1,
          '&.Mui-selected': {
            bgcolor: 'primary.main',
            color: 'white',
            '& .MuiListItemIcon-root': { color: 'white' },
            '&:hover': { bgcolor: 'primary.dark' },
          },
          '&:hover': { bgcolor: 'action.hover' },
        }}
      >
        <ListItemIcon sx={{ minWidth: 30, color: 'primary.main' }}>
          <Box sx={{ display: 'flex', fontSize: 18 }}>{icon}</Box>
        </ListItemIcon>
        <ListItemText
          primary={text}
          primaryTypographyProps={{ fontSize: '0.8rem', fontWeight: isActive ? 600 : 400 }}
        />
      </ListItemButton>
    </ListItem>
  );
}
