import { useQuery } from '@tanstack/react-query';
import { cajasApi } from '@/api/cajas';
import { useAuth } from '@/hooks/useAuth';

export const useCajasAbiertas = () => {
  const { user } = useAuth();

  return useQuery({
    queryKey: ['cajas', 'abiertas', user?.id],
    queryFn: () => cajasApi.getMisAbiertas(),
    enabled: !!user,
    refetchInterval: 60000, // Refrescar cada minuto
    staleTime: 30000,
  });
};
