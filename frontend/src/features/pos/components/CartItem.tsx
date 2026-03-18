import {
  Box,
  IconButton,
  TextField,
  Typography,
  Paper,
  Alert,
} from '@mui/material';
import DeleteIcon from '@mui/icons-material/Delete';
import RemoveIcon from '@mui/icons-material/Remove';
import AddIcon from '@mui/icons-material/Add';
import WarningIcon from '@mui/icons-material/Warning';
import type { CartItem } from '@/stores/cart.store';
import { useUiConfig } from '@/hooks/useUiConfig';

interface CartItemProps {
  item: CartItem;
  onUpdateQuantity: (productoId: string, cantidad: number) => void;
  onUpdatePrice: (productoId: string, precio: number) => void;
  onUpdateDiscount: (productoId: string, descuento: number) => void;
  onRemove: (productoId: string) => void;
}

export function CartItemComponent({
  item,
  onUpdateQuantity,
  onUpdatePrice,
  onUpdateDiscount,
  onRemove,
}: CartItemProps) {
  const { showPriceOverride, showDiscountOverride } = useUiConfig();

  const formatCurrency = (value: number) => {
    return new Intl.NumberFormat('es-CO', {
      style: 'currency',
      currency: 'COP',
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(value);
  };

  const subtotal = item.precioUnitario * item.cantidad;
  const descuentoValor = (subtotal * item.descuentoPorcentaje) / 100;
  const total = subtotal - descuentoValor;

  // Validar si precio es menor al costo
  const precioMenorAlCosto = item.precioUnitario < item.producto.precioCosto;

  return (
    <Paper
      sx={{
        p: 2,
        mb: 1,
        border: precioMenorAlCosto ? '2px solid' : 'none',
        borderColor: precioMenorAlCosto ? 'error.main' : 'transparent',
      }}
    >
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'start', mb: 1 }}>
        <Box sx={{ flexGrow: 1 }}>
          <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
            {item.producto.nombre}
          </Typography>
          <Typography variant="caption" color="text.secondary">
            {item.producto.codigoBarras}
          </Typography>
        </Box>
        <IconButton
          size="small"
          color="error"
          onClick={() => onRemove(item.producto.id)}
        >
          <DeleteIcon fontSize="small" />
        </IconButton>
      </Box>

      <Box sx={{ display: 'flex', gap: 1, mb: 1, alignItems: 'center' }}>
        <Box sx={{ display: 'flex', alignItems: 'center', border: '1px solid', borderColor: 'divider', borderRadius: 1 }}>
          <IconButton
            size="small"
            onClick={() => onUpdateQuantity(item.producto.id, item.cantidad - 1)}
            disabled={item.cantidad <= 1}
          >
            <RemoveIcon fontSize="small" />
          </IconButton>
          <TextField
            type="number"
            value={item.cantidad}
            onChange={(e) => {
              const val = parseFloat(e.target.value);
              if (val > 0) onUpdateQuantity(item.producto.id, val);
            }}
            size="small"
            sx={{
              width: '60px',
              '& .MuiOutlinedInput-root': {
                '& fieldset': { border: 'none' },
              },
              '& input': { textAlign: 'center', p: 0.5 },
            }}
          />
          <IconButton
            size="small"
            onClick={() => onUpdateQuantity(item.producto.id, item.cantidad + 1)}
          >
            <AddIcon fontSize="small" />
          </IconButton>
        </Box>

        <TextField
          label={item.precioEditable && showPriceOverride ? "Precio" : "Precio (Sucursal)"}
          type="number"
          value={item.precioUnitario}
          onChange={(e) => {
            const val = parseFloat(e.target.value);
            if (val >= 0) onUpdatePrice(item.producto.id, val);
          }}
          size="small"
          sx={{ flexGrow: 1 }}
          InputProps={{
            readOnly: !(item.precioEditable && showPriceOverride),
          }}
          helperText={!(item.precioEditable && showPriceOverride) ? 'Precio fijo' : undefined}
        />

        {showDiscountOverride && (
          <TextField
            label="Desc %"
            type="number"
            value={item.descuentoPorcentaje}
            onChange={(e) => {
              const val = parseFloat(e.target.value);
              if (val >= 0 && val <= 100) onUpdateDiscount(item.producto.id, val);
            }}
            size="small"
            sx={{ width: '80px' }}
            inputProps={{ min: 0, max: 100 }}
          />
        )}
      </Box>

      {precioMenorAlCosto && (
        <Alert severity="error" icon={<WarningIcon />} sx={{ mt: 1, py: 0 }}>
          Precio ${item.precioUnitario.toLocaleString()} &lt; Costo ${item.producto.precioCosto.toLocaleString()}
        </Alert>
      )}

      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="caption" color="text.secondary">
          Subtotal: {formatCurrency(subtotal)}
        </Typography>
        {descuentoValor > 0 && (
          <Typography variant="caption" color="error">
            -{formatCurrency(descuentoValor)}
          </Typography>
        )}
        <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
          {formatCurrency(total)}
        </Typography>
      </Box>
    </Paper>
  );
}
