import { useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import {
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Collapse,
  Box,
} from '@mui/material';
import { ExpandLess, ExpandMore } from '@mui/icons-material';
import type { NavItem } from './MenuSection';

interface CollapsibleMenuItemProps {
  text: string;
  icon: React.ReactElement;
  items: NavItem[];
  landingPath?: string;
}

export function CollapsibleMenuItem({ text, icon, items, landingPath }: CollapsibleMenuItemProps) {
  const { pathname } = useLocation();
  const isChildActive = items.some(
    (item) => pathname === item.path || pathname.startsWith(item.path + '/')
  );
  const [open, setOpen] = useState(isChildActive);
  const navigate = useNavigate();

  return (
    <>
      <Box sx={{ px: 0.75, mb: 0.25 }}>
        <ListItemButton
          onClick={() => setOpen(!open)}
          sx={{
            borderRadius: 1.5,
            py: 0.6,
            px: 1,
            '&:hover': { bgcolor: 'action.hover' },
          }}
        >
          <ListItemIcon sx={{ minWidth: 30, color: 'primary.main' }}>
            <Box sx={{ display: 'flex', fontSize: 18 }}>{icon}</Box>
          </ListItemIcon>
          <ListItemText
            primary={text}
            primaryTypographyProps={{ fontSize: '0.8rem', fontWeight: isChildActive ? 600 : 400 }}
          />
          {open
            ? <ExpandLess sx={{ color: 'text.secondary', fontSize: 18 }} />
            : <ExpandMore sx={{ color: 'text.secondary', fontSize: 18 }} />}
        </ListItemButton>
      </Box>

      <Collapse in={open} timeout="auto" unmountOnExit>
        <List component="div" disablePadding>
          {landingPath && (
            <Box sx={{ pl: 2, pr: 1, mb: 0.5 }}>
              <ListItemButton
                onClick={() => navigate(landingPath)}
                selected={pathname === landingPath}
                sx={{
                  borderRadius: 2,
                  py: 0.75,
                  '&.Mui-selected': {
                    bgcolor: 'primary.main',
                    color: 'white',
                    '&:hover': { bgcolor: 'primary.dark' },
                  },
                  '&:hover': { bgcolor: 'action.hover' },
                }}
              >
                <ListItemText
                  primary="Ver todas"
                  primaryTypographyProps={{ fontSize: '0.8rem', fontWeight: pathname === landingPath ? 600 : 400 }}
                />
              </ListItemButton>
            </Box>
          )}

          {items.map((item) => {
            const active = pathname === item.path || pathname.startsWith(item.path + '/');
            return (
              <Box key={item.text} sx={{ pl: 2, pr: 1, mb: 0.5 }}>
                <ListItemButton
                  onClick={() => navigate(item.path)}
                  selected={active}
                  sx={{
                    borderRadius: 2,
                    py: 0.75,
                    '&.Mui-selected': {
                      bgcolor: 'primary.main',
                      color: 'white',
                      '& .MuiListItemIcon-root': { color: 'white' },
                      '&:hover': { bgcolor: 'primary.dark' },
                    },
                    '&:hover': { bgcolor: 'action.hover' },
                  }}
                >
                  <ListItemIcon sx={{ minWidth: 32, color: 'primary.main' }}>
                    <Box sx={{ display: 'flex', fontSize: 18 }}>{item.icon}</Box>
                  </ListItemIcon>
                  <ListItemText
                    primary={item.text}
                    primaryTypographyProps={{ fontSize: '0.8rem', fontWeight: active ? 600 : 400 }}
                  />
                </ListItemButton>
              </Box>
            );
          })}
        </List>
      </Collapse>
    </>
  );
}
