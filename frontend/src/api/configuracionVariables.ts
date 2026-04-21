import apiClient from './client';
import type {
  ConfiguracionVariableDTO,
  CrearConfiguracionVariableDTO,
  ActualizarConfiguracionVariableDTO,
} from '@/types/api';

export const configuracionVariablesApi = {
  getAll: async (incluirInactivas?: boolean): Promise<ConfiguracionVariableDTO[]> => {
    const { data } = await apiClient.get<ConfiguracionVariableDTO[]>('/configuracion-variables', {
      params: { incluirInactivas },
    });
    return data;
  },

  getById: async (id: number): Promise<ConfiguracionVariableDTO> => {
    const { data } = await apiClient.get<ConfiguracionVariableDTO>(`/configuracion-variables/${id}`);
    return data;
  },

  getByNombre: async (nombre: string): Promise<ConfiguracionVariableDTO | null> => {
    try {
      const { data } = await apiClient.get<ConfiguracionVariableDTO>(`/configuracion-variables/nombre/${nombre}`);
      return data;
    } catch (err: unknown) {
      // 404 = variable no configurada o inactiva → tratar como "deshabilitada" (valor null)
      if ((err as { statusCode?: number })?.statusCode === 404) return null;
      throw err;
    }
  },

  create: async (dto: CrearConfiguracionVariableDTO): Promise<ConfiguracionVariableDTO> => {
    const { data } = await apiClient.post<ConfiguracionVariableDTO>('/configuracion-variables', dto);
    return data;
  },

  update: async (id: number, dto: ActualizarConfiguracionVariableDTO): Promise<void> => {
    await apiClient.put(`/configuracion-variables/${id}`, dto);
  },

  delete: async (id: number): Promise<void> => {
    await apiClient.delete(`/configuracion-variables/${id}`);
  },

  activate: async (id: number): Promise<void> => {
    await apiClient.patch(`/configuracion-variables/${id}/activar`);
  },
};
