import { create } from 'zustand';
import type { UserInfo, SucursalResumenDTO } from '@/types/api';

interface AuthState {
  user: UserInfo | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  activeSucursalId: number | undefined;
  activeEmpresaId: number | undefined;
  /** Empresas disponibles derivadas de las sucursales asignadas */
  empresasDisponibles: { id: number; nombre: string }[];
  /** IdP-specific logout function, set by AuthProvider */
  idpLogout: (() => void) | null;
  setUser: (user: UserInfo | null) => void;
  setLoading: (loading: boolean) => void;
  setActiveSucursal: (id: number) => void;
  setActiveEmpresa: (id: number) => void;
  setIdpLogout: (fn: () => void) => void;
  logout: () => void;
}

function deriveEmpresas(sucursales: SucursalResumenDTO[]): { id: number; nombre: string }[] {
  const map = new Map<number, string>();
  for (const s of sucursales) {
    if (s.empresaId != null && !map.has(s.empresaId)) {
      map.set(s.empresaId, s.empresaNombre ?? `Empresa ${s.empresaId}`);
    }
  }
  return Array.from(map.entries()).map(([id, nombre]) => ({ id, nombre }));
}

export const useAuthStore = create<AuthState>((set, get) => ({
  user: null,
  isAuthenticated: false,
  isLoading: true,
  activeSucursalId: undefined,
  activeEmpresaId: undefined,
  empresasDisponibles: [],
  idpLogout: null,

  setUser: (user) => {
    let activeSucursalId: number | undefined = user?.sucursalId;
    let activeEmpresaId: number | undefined;

    if (user) {
      const empresas = deriveEmpresas(user.sucursalesDisponibles);

      // Restaurar empresa activa desde localStorage
      const storedEmpresa = localStorage.getItem('activeEmpresaId');
      if (storedEmpresa) {
        const storedId = parseInt(storedEmpresa, 10);
        if (empresas.some(e => e.id === storedId)) {
          activeEmpresaId = storedId;
        }
      }

      // Auto-seleccionar si solo hay una empresa
      if (activeEmpresaId === undefined && empresas.length === 1) {
        activeEmpresaId = empresas[0].id;
        localStorage.setItem('activeEmpresaId', String(activeEmpresaId));
      }

      // Restaurar sucursal activa — filtrar por empresa activa si existe
      const storedSucursal = localStorage.getItem('activeSucursalId');
      if (storedSucursal) {
        const storedId = parseInt(storedSucursal, 10);
        const sucursalesFiltradas = activeEmpresaId
          ? user.sucursalesDisponibles.filter(s => s.empresaId === activeEmpresaId || s.empresaId == null)
          : user.sucursalesDisponibles;
        if (sucursalesFiltradas.some(s => s.id === storedId)) {
          activeSucursalId = storedId;
        }
      }

      // Primera sucursal disponible como fallback
      if (activeSucursalId === undefined && user.sucursalesDisponibles?.length > 0) {
        const sucursalesFiltradas = activeEmpresaId
          ? user.sucursalesDisponibles.filter(s => s.empresaId === activeEmpresaId || s.empresaId == null)
          : user.sucursalesDisponibles;
        if (sucursalesFiltradas.length > 0) {
          activeSucursalId = sucursalesFiltradas[0].id;
          localStorage.setItem('activeSucursalId', String(activeSucursalId));
        }
      }

      set({
        user,
        isAuthenticated: true,
        isLoading: false,
        activeSucursalId,
        activeEmpresaId,
        empresasDisponibles: empresas,
      });
    } else {
      set({ user: null, isAuthenticated: false, isLoading: false, activeSucursalId: undefined, activeEmpresaId: undefined, empresasDisponibles: [] });
    }
  },

  setLoading: (loading) => set({ isLoading: loading }),

  setActiveSucursal: (id) => {
    localStorage.setItem('activeSucursalId', String(id));
    set({ activeSucursalId: id });
  },

  setActiveEmpresa: (id) => {
    const { user } = get();
    localStorage.setItem('activeEmpresaId', String(id));

    // Al cambiar empresa, resetear sucursal activa a la primera de esa empresa
    let activeSucursalId: number | undefined;
    if (user) {
      const sucursalesFiltradas = user.sucursalesDisponibles.filter(
        s => s.empresaId === id || s.empresaId == null
      );
      if (sucursalesFiltradas.length > 0) {
        activeSucursalId = sucursalesFiltradas[0].id;
        localStorage.setItem('activeSucursalId', String(activeSucursalId));
      }
    }

    set({ activeEmpresaId: id, activeSucursalId });
  },

  setIdpLogout: (fn) => set({ idpLogout: fn }),

  logout: () => {
    sessionStorage.removeItem('access_token');
    localStorage.removeItem('activeSucursalId');
    localStorage.removeItem('activeEmpresaId');
    const idpLogout = get().idpLogout;
    set({ user: null, isAuthenticated: false, activeSucursalId: undefined, activeEmpresaId: undefined, empresasDisponibles: [] });
    if (idpLogout) {
      idpLogout();
    }
  },
}));
