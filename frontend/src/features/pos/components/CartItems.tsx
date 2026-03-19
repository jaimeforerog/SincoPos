import {
  Box,
  IconButton,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import DeleteIcon from '@mui/icons-material/Delete';
import RemoveIcon from '@mui/icons-material/Remove';
import AddIcon from '@mui/icons-material/Add';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import type { CartItem } from '@/stores/cart.store';

interface CartItemsProps {
  items: CartItem[];
  onUpdateQuantity: (productoId: string, cantidad: number) => void;
  onUpdatePrice: (productoId: string, precio: number) => void;
  onUpdateDiscount: (productoId: string, descuento: number) => void;
  onRemove: (productoId: string) => void;
}

const fmt = (v: number) =>
  new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', minimumFractionDigits: 0 }).format(v);

export function CartItems({ items, onUpdateQuantity, onRemove }: CartItemsProps) {
  if (items.length === 0) {
    return (
      <Box sx={{ py: 3, textAlign: 'center' }}>
        <Typography variant="body2" color="text.secondary">No hay productos en el carrito</Typography>
        <Typography variant="caption" color="text.secondary">Busca y selecciona productos para agregar</Typography>
      </Box>
    );
  }

  return (
    <Table size="small" stickyHeader sx={{ tableLayout: 'fixed', width: '100%' }}>
      <TableHead>
        <TableRow>
          <TableCell sx={{ fontWeight: 700, py: 0.75 }}>Producto</TableCell>
          <TableCell align="center" sx={{ fontWeight: 700, py: 0.75, width: 108 }}>Cant.</TableCell>
          <TableCell align="right" sx={{ fontWeight: 700, py: 0.75, width: 76 }}>Precio</TableCell>
          <TableCell align="right" sx={{ fontWeight: 700, py: 0.75, width: 76 }}>Total</TableCell>
          <TableCell sx={{ py: 0.75, width: 32 }} />
        </TableRow>
      </TableHead>
      <TableBody>
        {items.map((item) => {
          const subtotal = item.precioUnitario * item.cantidad;
          const descuento = (subtotal * item.descuentoPorcentaje) / 100;
          const total = subtotal - descuento;
          const precioMenorCosto = item.precioUnitario < item.producto.precioCosto;

          return (
            <TableRow
              key={item.producto.id}
              sx={{ bgcolor: precioMenorCosto ? 'error.50' : undefined }}
            >
              {/* Nombre */}
              <TableCell sx={{ py: 0.5 }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                  {precioMenorCosto && (
                    <Tooltip title={`Precio < Costo (${fmt(item.producto.precioCosto)})`}>
                      <WarningAmberIcon fontSize="small" color="error" />
                    </Tooltip>
                  )}
                  <Box>
                    <Typography variant="body2" sx={{ fontWeight: 600, lineHeight: 1.2, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      {item.producto.nombre}
                    </Typography>
                    {item.descuentoPorcentaje > 0 && (
                      <Typography variant="caption" color="success.main">
                        -{item.descuentoPorcentaje}% dto.
                      </Typography>
                    )}
                  </Box>
                </Box>
              </TableCell>

              {/* Cantidad */}
              <TableCell align="center" sx={{ py: 0.5 }}>
                <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                  <IconButton size="small" onClick={() => onUpdateQuantity(item.producto.id, item.cantidad - 1)} disabled={item.cantidad <= 1} sx={{ p: 0.25 }}>
                    <RemoveIcon sx={{ fontSize: 16 }} />
                  </IconButton>
                  <TextField
                    type="number"
                    value={item.cantidad}
                    onChange={(e) => { const v = parseFloat(e.target.value); if (v > 0) onUpdateQuantity(item.producto.id, v); }}
                    size="small"
                    sx={{ width: 44, '& .MuiOutlinedInput-root': { '& fieldset': { border: 'none' } }, '& input': { textAlign: 'center', p: 0.25, fontSize: '0.85rem' } }}
                  />
                  <IconButton size="small" onClick={() => onUpdateQuantity(item.producto.id, item.cantidad + 1)} sx={{ p: 0.25 }}>
                    <AddIcon sx={{ fontSize: 16 }} />
                  </IconButton>
                </Box>
              </TableCell>

              {/* Precio */}
              <TableCell align="right" sx={{ py: 0.5 }}>
                <Typography variant="body2">{fmt(item.precioUnitario)}</Typography>
              </TableCell>

              {/* Total */}
              <TableCell align="right" sx={{ py: 0.5 }}>
                <Typography variant="body2" sx={{ fontWeight: 700 }}>{fmt(total)}</Typography>
              </TableCell>

              {/* Eliminar */}
              <TableCell sx={{ py: 0.5 }}>
                <IconButton size="small" color="error" onClick={() => onRemove(item.producto.id)} sx={{ p: 0.25 }}>
                  <DeleteIcon sx={{ fontSize: 16 }} />
                </IconButton>
              </TableCell>
            </TableRow>
          );
        })}
      </TableBody>
    </Table>
  );
}
