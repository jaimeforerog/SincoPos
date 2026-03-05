import { ListItemButton, ListItemText, Typography, Box, Chip } from '@mui/material';
import InventoryIcon from '@mui/icons-material/Inventory';
import type { ProductoDTO } from '@/types/api';

interface ProductCardProps {
  producto: ProductoDTO;
  stock?: number;
  precio?: number;
  onClick: (producto: ProductoDTO) => void;
}

export function ProductCard({ producto, stock, precio, onClick }: ProductCardProps) {
  const formatCurrency = (value: number) => {
    return new Intl.NumberFormat('es-CO', {
      style: 'currency',
      currency: 'COP',
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(value);
  };

  const stockBajo = stock !== undefined && stock <= 10;
  const sinStock = stock !== undefined && stock <= 0;

  return (
    <ListItemButton
      onClick={() => onClick(producto)}
      disabled={sinStock}
      sx={{
        border: '1px solid',
        borderColor: 'divider',
        borderRadius: 1,
        mb: 0.5,
        '&:hover': {
          bgcolor: 'action.hover',
          borderColor: 'primary.main',
        },
      }}
    >
      <ListItemText
        primary={
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 600, flexGrow: 1 }}>
              {producto.nombre}
            </Typography>
            {stock !== undefined && (
              <Chip
                icon={<InventoryIcon />}
                label={`Stock: ${stock}`}
                size="small"
                color={sinStock ? 'error' : stockBajo ? 'warning' : 'success'}
                variant={sinStock ? 'filled' : 'outlined'}
              />
            )}
          </Box>
        }
        secondary={
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mt: 0.5 }}>
            <Typography variant="caption" color="text.secondary">
              Código: {producto.codigoBarras}
            </Typography>
            {precio !== undefined && precio > 0 ? (
              <Typography variant="body2" color="primary.main" sx={{ fontWeight: 700 }}>
                {formatCurrency(precio)}
              </Typography>
            ) : (
              <Typography variant="caption" color="text.secondary">
                Sin precio
              </Typography>
            )}
          </Box>
        }
        primaryTypographyProps={{ component: 'div' }}
        secondaryTypographyProps={{ component: 'div' }}
      />
    </ListItemButton>
  );
}
