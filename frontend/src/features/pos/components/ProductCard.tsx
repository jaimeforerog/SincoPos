import { Box, Typography } from '@mui/material';
import type { ProductoDTO } from '@/types/api';

interface ProductCardProps {
  producto: ProductoDTO;
  stock?: number;
  precio?: number;
  onClick: (producto: ProductoDTO) => void;
}

const fmt = (v: number) =>
  new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', minimumFractionDigits: 0 }).format(v);

export function ProductCard({ producto, stock, precio, onClick }: ProductCardProps) {
  const sinStock = stock !== undefined && stock <= 0;
  const stockBajo = stock !== undefined && stock > 0 && stock <= 10;

  return (
    <Box
      onClick={sinStock ? undefined : () => onClick(producto)}
      sx={{
        display: 'grid',
        gridTemplateColumns: '110px 1fr 52px 90px',
        alignItems: 'center',
        gap: 1,
        px: 1,
        py: '3px',
        borderBottom: '1px solid',
        borderColor: 'divider',
        cursor: sinStock ? 'not-allowed' : 'pointer',
        opacity: sinStock ? 0.45 : 1,
        '&:hover': sinStock ? {} : { bgcolor: 'action.hover' },
      }}
    >
      {/* Código */}
      <Typography
        variant="caption"
        noWrap
        sx={{ fontFamily: 'monospace', color: 'text.secondary', fontSize: '0.7rem' }}
      >
        {producto.codigoBarras}
      </Typography>

      {/* Nombre */}
      <Typography variant="caption" noWrap sx={{ fontWeight: 600, fontSize: '0.78rem' }}>
        {producto.nombre}
      </Typography>

      {/* Stock */}
      <Typography
        variant="caption"
        noWrap
        sx={{
          textAlign: 'right',
          fontSize: '0.75rem',
          fontWeight: 600,
          color: sinStock ? 'error.main' : stockBajo ? 'warning.main' : 'success.main',
        }}
      >
        {stock !== undefined ? stock : '—'}
      </Typography>

      {/* Precio */}
      <Typography
        variant="caption"
        noWrap
        sx={{ textAlign: 'right', fontSize: '0.75rem', fontWeight: 700, color: 'primary.main' }}
      >
        {precio !== undefined && precio > 0 ? fmt(precio) : '—'}
      </Typography>
    </Box>
  );
}
