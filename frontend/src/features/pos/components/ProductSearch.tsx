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
import { CameraInput } from './CameraInput';
import type { ProductoDTO } from '@/types/api';

interface ProductSearchProps {
  onSelectProduct: (producto: ProductoDTO) => void;
  fechaVenta?: string; // ISO UTC. Si está en el pasado, muestra stock histórico en esa fecha.
}

export function ProductSearch({ onSelectProduct, fechaVenta }: ProductSearchProps) {
  const [searchTerm, setSearchTerm] = useState('');
  const debouncedSearch = useDebounce(searchTerm, 300);
  const searchInputRef = useRef<HTMLInputElement>(null);
  const { activeSucursalId, activeEmpresaId } = useAuth();

  const usarHistorico = !!fechaVenta;

  const { data: productosData, isLoading } = useQuery({
    queryKey: ['productos', debouncedSearch, activeEmpresaId],
    queryFn: () =>
      productosApi.getAll({
        query: debouncedSearch || undefined,
        incluirInactivos: false,
      }),
    enabled: true,
  });

  const productos = productosData?.items || [];

  // Cargar inventario — histórico si la sesión tiene fecha retroactiva
  const { data: inventarios = [] } = useQuery({
    queryKey: ['inventario', activeSucursalId, usarHistorico ? fechaVenta : null],
    queryFn: () => usarHistorico && activeSucursalId
      ? inventarioApi.getStockHistorico({ sucursalId: activeSucursalId, fecha: fechaVenta! })
      : inventarioApi.getStock({ sucursalId: activeSucursalId }),
    enabled: !!activeSucursalId,
  });

  // Resolver precios de todos los productos activos para la sucursal (una sola llamada)
  const { data: preciosResueltos = [] } = useQuery({
    queryKey: ['precios-resueltos', activeSucursalId],
    queryFn: () => preciosApi.resolverLote(activeSucursalId!),
    enabled: !!activeSucursalId,
    staleTime: 30_000,
  });

  // Mapa productoId → precioVenta resuelto para lookup O(1)
  const precioMap = new Map(
    Array.isArray(preciosResueltos)
      ? preciosResueltos.map((p) => [p.productoId, p.precioVenta])
      : []
  );

  useEffect(() => {
    searchInputRef.current?.focus();

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
            endAdornment: (
              <InputAdornment position="end">
                {isLoading
                  ? <CircularProgress size={20} />
                  : <CameraInput onDetected={(code) => setSearchTerm(code)} />
                }
              </InputAdornment>
            ),
          }}
          autoFocus
        />
      </Box>

      <Box sx={{ flexGrow: 1, overflowY: 'auto', overflowX: 'hidden',
          '&::-webkit-scrollbar': { width: 6 },
          '&::-webkit-scrollbar-track': { bgcolor: 'grey.100', borderRadius: 3 },
          '&::-webkit-scrollbar-thumb': { bgcolor: 'grey.400', borderRadius: 3,
            '&:hover': { bgcolor: 'grey.600' } },
        }}>
        {productos.length === 0 && !isLoading && (
          <Typography variant="body2" color="text.secondary" align="center" sx={{ mt: 4 }}>
            {searchTerm ? 'No se encontraron productos' : 'Escribe para buscar productos'}
          </Typography>
        )}

        <List sx={{ p: 0 }}>
          {Array.isArray(productos) && productos.map((producto) => {
            const stockInfo = Array.isArray(inventarios)
              ? inventarios.find((inv) => inv.productoId === producto.id)
              : null;
            const stock = stockInfo?.cantidad || 0;
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
