import { apiClient } from './client';
import type { PaisDTO, CiudadDTO } from '@/types/api';

/**
 * Obtener lista de países
 */
export const getPaises = async (): Promise<PaisDTO[]> => {
  const response = await apiClient.get<PaisDTO[]>('/api/paises');
  return response.data;
};

/**
 * Obtener ciudades de un país específico
 */
export const getCiudadesPorPais = async (codigoPais: string): Promise<CiudadDTO[]> => {
  const response = await apiClient.get<CiudadDTO[]>(`/api/paises/${codigoPais}/ciudades`);
  return response.data;
};
