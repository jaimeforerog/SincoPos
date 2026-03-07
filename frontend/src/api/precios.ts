import apiClient from './client';
import type {
  PrecioSucursalDTO,
  CrearPrecioSucursalDTO,
  PrecioResueltoDTO,
  PrecioResueltoLoteItemDTO,
} from '@/types/api';

export const preciosApi = {
  // Resolver precio de un producto en una sucursal
  resolver: async (productoId: string, sucursalId: number) => {
    const response = await apiClient.get<PrecioResueltoDTO>('/precios/resolver', {
      params: { productoId, sucursalId },
    });
    return response.data;
  },

  // Crear o actualizar precio de producto en sucursal
  createOrUpdate: async (data: CrearPrecioSucursalDTO) => {
    const response = await apiClient.post<PrecioSucursalDTO>('/precios', data);
    return response.data;
  },

  // Listar todos los precios configurados para una sucursal (para el POS)
  getBySucursal: async (sucursalId: number) => {
    const response = await apiClient.get<PrecioSucursalDTO[]>('/precios', {
      params: { sucursalId },
    });
    return response.data;
  },

  // Resolver precios de TODOS los productos activos para una sucursal (una sola llamada)
  // Misma cascada: PrecioSucursal → PrecioBase → Costo × Margen
  resolverLote: async (sucursalId: number) => {
    const response = await apiClient.get<PrecioResueltoLoteItemDTO[]>('/precios/resolver-lote', {
      params: { sucursalId },
    });
    return response.data;
  },

  // Listar todos los precios de un producto
  getByProducto: async (productoId: string) => {
    const response = await apiClient.get<PrecioSucursalDTO[]>(
      `/precios/producto/${productoId}`
    );
    return response.data;
  },
};
