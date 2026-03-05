import apiClient from './client';
import type { UserInfo, SucursalResumenDTO } from '@/types/api';

// Tipo del DTO que devuelve el backend
interface PerfilUsuarioBackend {
  id: number;
  email: string;
  nombreCompleto: string;
  telefono?: string;
  rol: string;
  sucursalDefaultId?: number;
  sucursalDefaultNombre?: string;
  ultimoAcceso?: string;
  permisos: string[];
  sucursalesAsignadas: SucursalResumenDTO[];
}

export interface UsuarioDto {
  id: number;
  keycloakId: string;
  email: string;
  nombreCompleto: string;
  telefono?: string;
  rol: string;
  sucursalDefaultId?: number;
  sucursalDefaultNombre?: string;
  activo: boolean;
  fechaCreacion: string;
  ultimoAcceso?: string;
  sucursalesAsignadas: SucursalResumenDTO[];
}

export interface FiltrosUsuario {
  busqueda?: string;
  rol?: string;
  activo?: boolean;
  sucursalId?: number;
}

export const usuariosApi = {
  me: async (): Promise<UserInfo> => {
    const response = await apiClient.get<PerfilUsuarioBackend>('/api/usuarios/me');
    const d = response.data;
    return {
      id: String(d.id),
      username: d.email,
      email: d.email,
      nombre: d.nombreCompleto,
      roles: [d.rol],
      sucursalId: d.sucursalDefaultId,
      sucursalNombre: d.sucursalDefaultNombre,
      sucursalesDisponibles: d.sucursalesAsignadas ?? [],
    };
  },

  listar: async (filtros?: FiltrosUsuario): Promise<UsuarioDto[]> => {
    const response = await apiClient.get<UsuarioDto[]>('/api/usuarios', {
      params: filtros,
    });
    return response.data;
  },

  actualizarSucursal: async (id: number, sucursalId: number): Promise<void> => {
    await apiClient.put(`/api/usuarios/${id}/sucursal`, { sucursalId });
  },

  actualizarMiSucursal: async (sucursalId: number): Promise<void> => {
    await apiClient.put('/api/usuarios/me/sucursal', { sucursalId });
  },

  asignarSucursales: async (id: number, sucursalIds: number[]): Promise<void> => {
    await apiClient.put(`/api/usuarios/${id}/sucursales`, { sucursalIds });
  },

  cambiarEstado: async (id: number, activo: boolean, motivo?: string): Promise<void> => {
    await apiClient.put(`/api/usuarios/${id}/estado`, { activo, motivo });
  },
};
