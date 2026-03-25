import { List, ListSubheader, Divider } from '@mui/material';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/hooks/useAuth';
import { CollapsibleMenuItem } from './CollapsibleMenuItem';
import { MenuItem } from './MenuItem';

export interface NavItem {
  text: string;
  icon: React.ReactElement;
  path: string;
  roles?: string[];
}

export interface MenuSection {
  title: string;
  roles?: string[];
  items: NavItem[];
  collapsible?: boolean;
  icon?: React.ReactElement;
  landingPath?: string;
}

interface MenuSectionProps {
  section: MenuSection;
}

export function MenuSection({ section }: MenuSectionProps) {
  const { hasAnyRole } = useAuth();
  const navigate = useNavigate();

  // Si el usuario no tiene ningún rol de la sección, no mostrar nada
  if (section.roles && !hasAnyRole(section.roles)) {
    return null;
  }

  // Filtrar items por rol
  const visibleItems = section.items.filter(
    item => !item.roles || hasAnyRole(item.roles)
  );

  // Si no hay items visibles, no mostrar la sección
  if (visibleItems.length === 0) {
    return null;
  }

  return (
    <>
      {/* Label de la sección */}
      <ListSubheader
        sx={{
          bgcolor: 'transparent',
          fontWeight: 700,
          fontSize: '0.65rem',
          textTransform: 'uppercase',
          color: 'text.disabled',
          lineHeight: '22px',
          mt: 0.5,
          px: 1.5,
          letterSpacing: '0.6px',
        }}
      >
        {section.title}
      </ListSubheader>

      {/* Items de la sección */}
      {section.collapsible ? (
        <CollapsibleMenuItem
          text={section.title}
          icon={section.icon!}
          items={visibleItems}
          landingPath={section.landingPath}
        />
      ) : (
        <List component="div" disablePadding>
          {visibleItems.map((item) => (
            <MenuItem
              key={item.text}
              text={item.text}
              icon={item.icon}
              path={item.path}
              onClick={() => navigate(item.path)}
            />
          ))}
        </List>
      )}

      {/* Divider después de cada sección */}
      <Divider sx={{ my: 1 }} />
    </>
  );
}
