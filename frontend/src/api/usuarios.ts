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

// ─── DTOs para crear / actualizar usuarios ───────────────────────────────────

export interface CrearUsuarioRequest {
  email: string;
  nombreCompleto: string;
  telefono?: string;
  rol: string;
  sucursalDefaultId?: number;
  sucursalIds?: number[];
}

export interface CrearUsuarioResult {
  id: number;
  email: string;
  nombreCompleto: string;
  rol: string;
  passwordTemporal?: string;
}

export interface ActualizarUsuarioRequest {
  nombreCompleto?: string;
  telefono?: string;
  rol?: string;
  sucursalDefaultId?: number;
  sucursalIds?: number[];
}

export interface CambiarRolRequest {
  rol: string;
}

export interface ResetPasswordResult {
  passwordTemporal: string;
}

export const usuariosApi = {
  me: async (): Promise<UserInfo> => {
    const response = await apiClient.get<PerfilUsuarioBackend>('/usuarios/me');
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
    const response = await apiClient.get<UsuarioDto[]>('/usuarios', {
      params: filtros,
    });
    return response.data;
  },

  actualizarSucursal: async (id: number, sucursalId: number): Promise<void> => {
    await apiClient.put(`/usuarios/${id}/sucursal`, { sucursalId });
  },

  actualizarMiSucursal: async (sucursalId: number): Promise<void> => {
    await apiClient.put('/usuarios/me/sucursal', { sucursalId });
  },

  asignarSucursales: async (id: number, sucursalIds: number[]): Promise<void> => {
    await apiClient.put(`/usuarios/${id}/sucursales`, { sucursalIds });
  },

  cambiarEstado: async (id: number, activo: boolean, motivo?: string): Promise<void> => {
    await apiClient.put(`/usuarios/${id}/estado`, { activo, motivo });
  },

  crear: async (data: CrearUsuarioRequest): Promise<CrearUsuarioResult> => {
    const response = await apiClient.post<CrearUsuarioResult>('/usuarios', data);
    return response.data;
  },

  actualizar: async (id: number, data: ActualizarUsuarioRequest): Promise<void> => {
    await apiClient.put(`/usuarios/${id}`, data);
  },

  cambiarRol: async (id: number, rol: string): Promise<void> => {
    await apiClient.put(`/usuarios/${id}/rol`, { rol });
  },

  resetPassword: async (id: number): Promise<ResetPasswordResult> => {
    const response = await apiClient.post<ResetPasswordResult>(`/usuarios/${id}/reset-password`);
    return response.data;
  },
};
