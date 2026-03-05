import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Collapse,
} from '@mui/material';
import {
  ExpandLess,
  ExpandMore,
  ViewModule,
} from '@mui/icons-material';
import type { NavItem } from './MenuSection';

interface CollapsibleMenuItemProps {
  text: string;
  icon: React.ReactElement;
  items: NavItem[];
  landingPath?: string;
}

export function CollapsibleMenuItem({
  text,
  icon,
  items,
  landingPath,
}: CollapsibleMenuItemProps) {
  const [open, setOpen] = useState(false);
  const navigate = useNavigate();

  const handleToggle = () => {
    setOpen(!open);
  };

  return (
    <>
      <ListItemButton onClick={handleToggle}>
        <ListItemIcon>{icon}</ListItemIcon>
        <ListItemText primary={text} />
        {open ? <ExpandLess /> : <ExpandMore />}
      </ListItemButton>

      <Collapse in={open} timeout="auto" unmountOnExit>
        <List component="div" disablePadding>
          {/* Mostrar landing page como primer item */}
          {landingPath && (
            <ListItemButton
              sx={{ pl: 4 }}
              onClick={() => navigate(landingPath)}
            >
              <ListItemIcon>
                <ViewModule fontSize="small" />
              </ListItemIcon>
              <ListItemText
                primary="Ver todas"
                primaryTypographyProps={{
                  variant: 'body2',
                  fontWeight: 500,
                }}
                secondary="Panel principal"
                secondaryTypographyProps={{
                  variant: 'caption',
                }}
              />
            </ListItemButton>
          )}

          {/* Items del submenu */}
          {items.map((item) => (
            <ListItemButton
              key={item.text}
              sx={{ pl: 4 }}
              onClick={() => navigate(item.path)}
            >
              <ListItemIcon>{item.icon}</ListItemIcon>
              <ListItemText
                primary={item.text}
                primaryTypographyProps={{
                  variant: 'body2',
                }}
              />
            </ListItemButton>
          ))}
        </List>
      </Collapse>
    </>
  );
}
