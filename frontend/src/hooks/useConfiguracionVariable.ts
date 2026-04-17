import { useQuery } from '@tanstack/react-query';
import { configuracionVariablesApi } from '@/api/configuracionVariables';

const queryConfig = (nombre: string) => ({
  queryKey: ['configuracion-variable', nombre] as const,
  queryFn: () => configuracionVariablesApi.getByNombre(nombre),
  retry: 1,
  staleTime: 0,
  gcTime: 5 * 60 * 1000,
});

/**
 * Devuelve el valor entero de una variable de configuración.
 * Retorna `defaultValue` (0 por defecto) mientras carga o si la variable no existe.
 */
export function useConfiguracionVariableInt(nombre: string, defaultValue = 0): number {
  const { data } = useQuery(queryConfig(nombre));
  return data ? parseInt(data.valor, 10) || defaultValue : defaultValue;
}

/**
 * Como `useConfiguracionVariableInt` pero también expone `isFetched`
 * para saber si la query ya terminó (éxito o error), útil para guards
 * que deben esperar la primera carga antes de tomar decisiones.
 */
export function useConfiguracionVariableIntQuery(nombre: string, defaultValue = 0) {
  const { data, isFetched } = useQuery(queryConfig(nombre));
  return {
    value: data ? parseInt(data.valor, 10) || defaultValue : defaultValue,
    isFetched,
  };
}
