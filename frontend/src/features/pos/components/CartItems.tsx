import { Box, Typography } from '@mui/material';
import { CartItemComponent } from './CartItem';
import type { CartItem } from '@/stores/cart.store';

interface CartItemsProps {
  items: CartItem[];
  onUpdateQuantity: (productoId: string, cantidad: number) => void;
  onUpdatePrice: (productoId: string, precio: number) => void;
  onUpdateDiscount: (productoId: string, descuento: number) => void;
  onRemove: (productoId: string) => void;
}

export function CartItems({
  items,
  onUpdateQuantity,
  onUpdatePrice,
  onUpdateDiscount,
  onRemove,
}: CartItemsProps) {
  if (items.length === 0) {
    return (
      <Box sx={{ p: 3, textAlign: 'center' }}>
        <Typography variant="body2" color="text.secondary">
          No hay productos en el carrito
        </Typography>
        <Typography variant="caption" color="text.secondary">
          Busca y selecciona productos para agregar
        </Typography>
      </Box>
    );
  }

  return (
    <Box sx={{ maxHeight: '400px', overflow: 'auto', mb: 2 }}>
      {items.map((item) => (
        <CartItemComponent
          key={item.producto.id}
          item={item}
          onUpdateQuantity={onUpdateQuantity}
          onUpdatePrice={onUpdatePrice}
          onUpdateDiscount={onUpdateDiscount}
          onRemove={onRemove}
        />
      ))}
    </Box>
  );
}
