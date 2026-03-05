import apiClient from './client';
import type {
  CategoriaDTO,
  CategoriaArbolDTO,
  CrearCategoriaDTO,
  ActualizarCategoriaDTO,
  MoverCategoriaDTO,
} from '@/types/api';

export const categoriasApi = {
  getAll: async (incluirInactivas = false): Promise<CategoriaDTO[]> => {
    const { data } = await apiClient.get<CategoriaDTO[]>('/api/categorias', {
      params: { incluirInactivas },
    });
    return data;
  },

  getById: async (id: number): Promise<CategoriaDTO> => {
    const { data } = await apiClient.get<CategoriaDTO>(`/api/categorias/${id}`);
    return data;
  },

  getArbol: async (incluirInactivas = false): Promise<CategoriaArbolDTO[]> => {
    const { data } = await apiClient.get<CategoriaArbolDTO[]>('/api/categorias/arbol', {
      params: { incluirInactivas },
    });
    return data;
  },

  getRaiz: async (incluirInactivas = false): Promise<CategoriaDTO[]> => {
    const { data } = await apiClient.get<CategoriaDTO[]>('/api/categorias/raiz', {
      params: { incluirInactivas },
    });
    return data;
  },

  getSubCategorias: async (id: number, incluirInactivas = false): Promise<CategoriaDTO[]> => {
    const { data } = await apiClient.get<CategoriaDTO[]>(`/api/categorias/${id}/subcategorias`, {
      params: { incluirInactivas },
    });
    return data;
  },

  create: async (categoria: CrearCategoriaDTO): Promise<CategoriaDTO> => {
    const { data } = await apiClient.post<CategoriaDTO>('/api/categorias', categoria);
    return data;
  },

  update: async (id: number, categoria: ActualizarCategoriaDTO): Promise<void> => {
    await apiClient.put(`/api/categorias/${id}`, categoria);
  },

  mover: async (dto: MoverCategoriaDTO): Promise<void> => {
    await apiClient.post('/api/categorias/mover', dto);
  },

  delete: async (id: number): Promise<void> => {
    await apiClient.delete(`/api/categorias/${id}`);
  },
};
