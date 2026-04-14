import { useQuery } from '@tanstack/react-query';
import { configuracionVariablesApi } from '@/api/configuracionVariables';

/**
 * Devuelve el valor entero de una variable de configuración.
 * Retorna `defaultValue` (0 por defecto) mientras carga o si la variable no existe.
 */
export function useConfiguracionVariableInt(nombre: string, defaultValue = 0): number {
  const { data } = useQuery({
    queryKey: ['configuracion-variable', nombre],
    queryFn: () => configuracionVariablesApi.getByNombre(nombre),
    retry: false,
    staleTime: 60 * 1000,
  });
  return data ? parseInt(data.valor, 10) || defaultValue : defaultValue;
}
