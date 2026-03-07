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
    const { data } = await apiClient.get<CategoriaDTO[]>('/categorias', {
      params: { incluirInactivas },
    });
    return data;
  },

  getById: async (id: number): Promise<CategoriaDTO> => {
    const { data } = await apiClient.get<CategoriaDTO>(`/categorias/${id}`);
    return data;
  },

  getArbol: async (incluirInactivas = false): Promise<CategoriaArbolDTO[]> => {
    const { data } = await apiClient.get<CategoriaArbolDTO[]>('/categorias/arbol', {
      params: { incluirInactivas },
    });
    return data;
  },

  getRaiz: async (incluirInactivas = false): Promise<CategoriaDTO[]> => {
    const { data } = await apiClient.get<CategoriaDTO[]>('/categorias/raiz', {
      params: { incluirInactivas },
    });
    return data;
  },

  getSubCategorias: async (id: number, incluirInactivas = false): Promise<CategoriaDTO[]> => {
    const { data } = await apiClient.get<CategoriaDTO[]>(`/categorias/${id}/subcategorias`, {
      params: { incluirInactivas },
    });
    return data;
  },

  create: async (categoria: CrearCategoriaDTO): Promise<CategoriaDTO> => {
    const { data } = await apiClient.post<CategoriaDTO>('/categorias', categoria);
    return data;
  },

  update: async (id: number, categoria: ActualizarCategoriaDTO): Promise<void> => {
    await apiClient.put(`/categorias/${id}`, categoria);
  },

  mover: async (dto: MoverCategoriaDTO): Promise<void> => {
    await apiClient.post('/categorias/mover', dto);
  },

  delete: async (id: number): Promise<void> => {
    await apiClient.delete(`/categorias/${id}`);
  },
};
