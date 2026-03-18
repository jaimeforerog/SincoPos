import { useState, useEffect, useRef } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Box,
  TextField,
  CircularProgress,
  Typography,
  InputAdornment,
  List,
  Chip,
} from '@mui/material';
import SearchIcon  from '@mui/icons-material/Search';
import FlashOnIcon from '@mui/icons-material/FlashOn';
import { useDebounce } from '@/hooks/useDebounce';
import { useAuth } from '@/hooks/useAuth';
import { useUiConfig } from '@/hooks/useUiConfig';
import { productosApi } from '@/api/productos';
import { inventarioApi } from '@/api/inventario';
import { preciosApi } from '@/api/precios';
import { ProductCard } from './ProductCard';
import { CameraInput } from './CameraInput';
import { useAnticipatedProducts } from '../hooks/useAnticipatedProducts';
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
  const { quickProductsLimit }      = useUiConfig();

  // Capa 5 — productos frecuentes
  const anticipated = useAnticipatedProducts(activeSucursalId, quickProductsLimit || 12);

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

  const showAnticipated = searchTerm.length === 0 && anticipated.length > 0;

  return (
    <Box sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>

      {/* Campo de búsqueda unificado */}
      <Box sx={{ mb: 2 }}>
        <TextField
          inputRef={searchInputRef}
          fullWidth
          placeholder="Buscar producto por nombre, código o cámara… (Ctrl+K)"
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

      {/* Capa 5 — chips de productos frecuentes */}
      {showAnticipated && (
        <Box sx={{ mb: 2 }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mb: 0.75 }}>
            <FlashOnIcon sx={{ fontSize: 14, color: sincoColors.warning.main }} />
            <Typography variant="caption" color="text.secondary" fontWeight={600}>
              Frecuentes
            </Typography>
          </Box>
          <Box sx={{ display: 'flex', gap: 0.75, flexWrap: 'wrap' }}>
            {anticipated.map((p) => (
              <Chip
                key={p.id}
                label={p.nombre}
                size="small"
                onClick={() => onSelectProduct(p)}
                sx={{
                  cursor:     'pointer',
                  bgcolor:    sincoColors.brand[50],
                  color:      sincoColors.brand[800],
                  fontWeight: 500,
                  border:     `1px solid ${sincoColors.brand[100]}`,
                  '&:hover':  { bgcolor: sincoColors.brand[100] },
                }}
              />
            ))}
          </Box>
        </Box>
      )}

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
