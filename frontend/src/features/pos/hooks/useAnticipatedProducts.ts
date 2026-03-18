import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { productosApi } from '@/api/productos';
import type { ProductoDTO } from '@/types/api';

/**
 * Capa 5 — Anticipación funcional.
 * Llama al endpoint /productos/anticipados que usa UserBehaviorProjection
 * para retornar los productos más frecuentes del cajero autenticado.
 * Fallback: si no hay historial, retorna [] y el campo de búsqueda sigue funcionando normalmente.
 */
export function useAnticipatedProducts(
  activeSucursalId: number | undefined | null,
  limit = 12,
): ProductoDTO[] {
  const { data = [] } = useQuery<ProductoDTO[]>({
    queryKey: ['anticipated-products', activeSucursalId],
    queryFn:  () => productosApi.getAnticipated(limit),
    enabled:  true,
    staleTime: 5 * 60_000,
    placeholderData: [],
  });

  return useMemo(() => data.slice(0, limit), [data, limit]);
}
