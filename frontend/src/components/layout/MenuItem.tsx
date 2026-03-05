import { ListItem, ListItemButton, ListItemIcon, ListItemText } from '@mui/material';

interface MenuItemProps {
  text: string;
  icon: React.ReactElement;
  onClick: () => void;
}

export function MenuItem({ text, icon, onClick }: MenuItemProps) {
  return (
    <ListItem disablePadding>
      <ListItemButton onClick={onClick}>
        <ListItemIcon>{icon}</ListItemIcon>
        <ListItemText primary={text} />
      </ListItemButton>
    </ListItem>
  );
}
