import { useState, useEffect, useRef } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Box,
  TextField,
  CircularProgress,
  Typography,
  InputAdornment,
  List,
} from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import { useDebounce } from '@/hooks/useDebounce';
import { useAuth } from '@/hooks/useAuth';
import { productosApi } from '@/api/productos';
import { inventarioApi } from '@/api/inventario';
import { preciosApi } from '@/api/precios';
import { ProductCard } from './ProductCard';
import type { ProductoDTO } from '@/types/api';

interface ProductSearchProps {
  onSelectProduct: (producto: ProductoDTO) => void;
}

export function ProductSearch({ onSelectProduct }: ProductSearchProps) {
  const [searchTerm, setSearchTerm] = useState('');
  const debouncedSearch = useDebounce(searchTerm, 300);
  const searchInputRef = useRef<HTMLInputElement>(null);
  const { activeSucursalId } = useAuth();

  const { data: productos = [], isLoading } = useQuery({
    queryKey: ['productos', debouncedSearch],
    queryFn: () =>
      productosApi.getAll({
        query: debouncedSearch || undefined,
        incluirInactivos: false,
      }),
    enabled: true,
  });

  // Cargar inventario para la sucursal activa
  const { data: inventarios = [] } = useQuery({
    queryKey: ['inventario', activeSucursalId],
    queryFn: () =>
      inventarioApi.getStock({
        sucursalId: activeSucursalId,
      }),
    enabled: !!activeSucursalId,
  });

  // Resolver precios de todos los productos activos para la sucursal (una sola llamada)
  // Incluye fallback a Costo × Margen, igual que al agregar al carrito
  const { data: preciosResueltos = [] } = useQuery({
    queryKey: ['precios-resueltos', activeSucursalId],
    queryFn: () => preciosApi.resolverLote(activeSucursalId!),
    enabled: !!activeSucursalId,
    staleTime: 30_000,
  });

  // Mapa productoId → precioVenta resuelto para lookup O(1)
  const precioMap = new Map(preciosResueltos.map((p) => [p.productoId, p.precioVenta]));

  useEffect(() => {
    // Focus en el campo de búsqueda al montar
    searchInputRef.current?.focus();

    // Shortcut Ctrl+K para enfocar búsqueda
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.ctrlKey && e.key === 'k') {
        e.preventDefault();
        searchInputRef.current?.focus();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);

  return (
    <Box sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
      <Box sx={{ mb: 2 }}>
        <TextField
          inputRef={searchInputRef}
          fullWidth
          placeholder="Buscar producto... (Ctrl+K)"
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          InputProps={{
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon />
              </InputAdornment>
            ),
            endAdornment: isLoading && (
              <InputAdornment position="end">
                <CircularProgress size={20} />
              </InputAdornment>
            ),
          }}
          autoFocus
        />
      </Box>

      <Box sx={{ flexGrow: 1, overflow: 'auto' }}>
        {productos.length === 0 && !isLoading && (
          <Typography variant="body2" color="text.secondary" align="center" sx={{ mt: 4 }}>
            {searchTerm
              ? 'No se encontraron productos'
              : 'Escribe para buscar productos'}
          </Typography>
        )}

        <List sx={{ p: 0 }}>
          {productos.map((producto) => {
            // Buscar stock del producto
            const stockInfo = inventarios.find((inv) => inv.productoId === producto.id);
            const stock = stockInfo?.cantidad || 0;

            // Precio resuelto (Sucursal → Base → Margen×Costo)
            const precioDisplay = precioMap.get(producto.id);

            return (
              <ProductCard
                key={producto.id}
                producto={producto}
                stock={stock}
                precio={precioDisplay}
                onClick={onSelectProduct}
              />
            );
          })}
        </List>
      </Box>
    </Box>
  );
}
