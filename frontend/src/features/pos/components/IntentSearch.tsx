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
import { sincoColors } from '@/theme/tokens';
import type { ProductoDTO } from '@/types/api';

/**
 * Capa 1 — Entrada multimodal.
 *
 * Unifica texto, código de barras y cámara bajo un único campo de intención.
 * Agrega chips de productos frecuentes (Capa 5) cuando el campo está vacío.
 */
interface IntentSearchProps {
  onSelectProduct: (producto: ProductoDTO) => void;
}

export function IntentSearch({ onSelectProduct }: IntentSearchProps) {
  const [searchTerm, setSearchTerm] = useState('');
  const debouncedSearch             = useDebounce(searchTerm, 300);
  const searchInputRef              = useRef<HTMLInputElement>(null);
  const { activeSucursalId }        = useAuth();
  // Catálogo paginado con búsqueda debounced
  const { data: productosData, isLoading } = useQuery({
    queryKey: ['productos', debouncedSearch],
    queryFn:  () => productosApi.getAll({ query: debouncedSearch || undefined, incluirInactivos: false }),
    enabled:  true,
  });
  const productos = productosData?.items ?? [];

  // Stock de la sucursal activa
  const { data: inventarios = [] } = useQuery({
    queryKey: ['inventario', activeSucursalId],
    queryFn:  () => inventarioApi.getStock({ sucursalId: activeSucursalId }),
    enabled:  !!activeSucursalId,
  });

  // Precios resueltos en lote
  const { data: preciosResueltos = [] } = useQuery({
    queryKey: ['precios-resueltos', activeSucursalId],
    queryFn:  () => preciosApi.resolverLote(activeSucursalId!),
    enabled:  !!activeSucursalId,
    staleTime: 30_000,
  });
  const precioMap = new Map(
    Array.isArray(preciosResueltos)
      ? preciosResueltos.map((p) => [p.productoId, p.precioVenta])
      : [],
  );

  // Foco automático + Ctrl+K
  useEffect(() => {
    searchInputRef.current?.focus();
    const onKey = (e: KeyboardEvent) => {
      if (e.ctrlKey && e.key === 'k') {
        e.preventDefault();
        searchInputRef.current?.focus();
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  return (
    <Box sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>

      {/* Título + campo de búsqueda en la misma fila */}
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, mb: 2 }}>
        <Typography variant="h6" sx={{ fontWeight: 600, whiteSpace: 'nowrap' }}>
          Productos
        </Typography>
        <TextField
          inputRef={searchInputRef}
          fullWidth
          size="small"
          placeholder="Nombre, código o cámara… (Ctrl+K)"
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          autoFocus
          sx={{
            '& .MuiOutlinedInput-root': {
              borderRadius: '12px',
              '& fieldset': { borderColor: sincoColors.brand[600], borderWidth: 2 },
              '&:hover fieldset': { borderColor: sincoColors.brand[700] },
              '&.Mui-focused fieldset': {
                borderColor: sincoColors.brand[800],
                boxShadow: `0 0 0 4px ${sincoColors.brand[50]}`,
              },
            },
          }}
          InputProps={{
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon sx={{ color: sincoColors.brand[700] }} />
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
        />
      </Box>

      {/* Lista de resultados */}
      <Box
        sx={{
          flexGrow: 1, overflowY: 'auto', overflowX: 'hidden',
          '&::-webkit-scrollbar': { width: 6 },
          '&::-webkit-scrollbar-track': { bgcolor: 'grey.100', borderRadius: 3 },
          '&::-webkit-scrollbar-thumb': {
            bgcolor: 'grey.400', borderRadius: 3,
            '&:hover': { bgcolor: 'grey.600' },
          },
        }}
      >
        {productos.length === 0 && !isLoading && (
          <Typography variant="body2" color="text.secondary" align="center" sx={{ mt: 4 }}>
            {searchTerm ? 'No se encontraron productos' : 'Escribe para buscar productos'}
          </Typography>
        )}

        <List sx={{ p: 0 }}>
          {productos.map((producto) => {
            const stockInfo = Array.isArray(inventarios)
              ? inventarios.find((inv) => inv.productoId === producto.id)
              : null;
            return (
              <ProductCard
                key={producto.id}
                producto={producto}
                stock={stockInfo?.cantidad ?? 0}
                precio={precioMap.get(producto.id)}
                onClick={onSelectProduct}
              />
            );
          })}
        </List>
      </Box>
    </Box>
  );
}
