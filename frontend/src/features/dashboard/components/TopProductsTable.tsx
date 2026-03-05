import {
  Card,
  CardContent,
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Box,
  Chip,
} from '@mui/material';
import type { TopProductoDTO } from '@/types/api';

interface TopProductsTableProps {
  products: TopProductoDTO[];
}

const formatCurrency = (value: number) => {
  return new Intl.NumberFormat('es-CO', {
    style: 'currency',
    currency: 'COP',
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  }).format(value);
};

export function TopProductsTable({ products }: TopProductsTableProps) {
  return (
    <Card>
      <CardContent>
        <Typography variant="h6" sx={{ mb: 2, fontWeight: 600 }}>
          Top 5 Productos Más Vendidos
        </Typography>

        {products.length === 0 ? (
          <Box
            sx={{
              py: 4,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <Typography color="text.secondary">
              No hay datos de productos vendidos hoy
            </Typography>
          </Box>
        ) : (
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell sx={{ fontWeight: 700 }}>#</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>Producto</TableCell>
                  <TableCell sx={{ fontWeight: 700 }} align="center">
                    Unidades
                  </TableCell>
                  <TableCell sx={{ fontWeight: 700 }} align="right">
                    Total Ventas
                  </TableCell>
                  <TableCell sx={{ fontWeight: 700 }} align="right">
                    Utilidad
                  </TableCell>
                  <TableCell sx={{ fontWeight: 700 }} align="center">
                    Margen
                  </TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {products.map((product, index) => (
                  <TableRow
                    key={product.productoId}
                    hover
                    sx={{
                      '&:last-child td, &:last-child th': { border: 0 },
                      backgroundColor:
                        index === 0 ? 'action.hover' : 'transparent',
                    }}
                  >
                    <TableCell>
                      <Chip
                        label={index + 1}
                        size="small"
                        color={index === 0 ? 'primary' : 'default'}
                        sx={{ fontWeight: 700 }}
                      />
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>
                        {product.nombre}
                      </Typography>
                    </TableCell>
                    <TableCell align="center">
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>
                        {product.cantidadVendida}
                      </Typography>
                    </TableCell>
                    <TableCell align="right">
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>
                        {formatCurrency(product.totalVentas)}
                      </Typography>
                    </TableCell>
                    <TableCell align="right">
                      <Typography
                        variant="body2"
                        sx={{
                          fontWeight: 600,
                          color:
                            product.utilidad > 0 ? 'success.main' : 'error.main',
                        }}
                      >
                        {formatCurrency(product.utilidad)}
                      </Typography>
                    </TableCell>
                    <TableCell align="center">
                      <Chip
                        label={`${product.margenPorcentaje.toFixed(1)}%`}
                        size="small"
                        color={
                          product.margenPorcentaje >= 30
                            ? 'success'
                            : product.margenPorcentaje >= 15
                            ? 'warning'
                            : 'error'
                        }
                        sx={{ fontWeight: 600 }}
                      />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </CardContent>
    </Card>
  );
}
