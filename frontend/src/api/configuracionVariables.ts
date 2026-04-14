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

  getByNombre: async (nombre: string): Promise<ConfiguracionVariableDTO> => {
    const { data } = await apiClient.get<ConfiguracionVariableDTO>(`/configuracion-variables/nombre/${nombre}`);
    return data;
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
};
