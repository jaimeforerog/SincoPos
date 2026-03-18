import { useQuery } from '@tanstack/react-query';
import { cajasApi } from '@/api/cajas';
import { useAuth } from '@/hooks/useAuth';
import { useOfflineStore } from '@/stores/offline.store';
import { posSessionCache } from '@/offline/posSessionCache';

export const useCajasAbiertas = () => {
  const { user } = useAuth();
  const isOnline = useOfflineStore((s) => s.isOnline);

  const query = useQuery({
    queryKey: ['cajas', 'abiertas', user?.id],
    queryFn: async () => {
      const data = await cajasApi.getMisAbiertas();
      posSessionCache.saveCajas(data); // guardar para uso offline
      return data;
    },
    enabled: !!user && isOnline,
    refetchInterval: isOnline ? 60000 : false,
    staleTime: 30000,
  });

  // Fallback offline: devolver cajas cacheadas si la query no tiene datos
  if (!isOnline && (!query.data || query.data.length === 0)) {
    const cached = posSessionCache.loadCajas();
    if (cached.length > 0) {
      return { ...query, data: cached };
    }
  }

  return query;
};
