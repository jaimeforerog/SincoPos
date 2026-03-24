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
  setActiveEmpresa: (id: number, nombre?: string) => void;
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
      // Preferir la lista explícita del backend (incluye empresas sin sucursales).
      // Fallback a derivación desde sucursales para compatibilidad.
      const empresas = (user.empresasDisponibles && user.empresasDisponibles.length > 0)
        ? user.empresasDisponibles
        : deriveEmpresas(user.sucursalesDisponibles);

      // Limpiar valor legacy de localStorage si existe (migración)
      localStorage.removeItem('activeEmpresaId');

      // Restaurar empresa activa desde sessionStorage (no localStorage).
      // sessionStorage se limpia al cerrar el navegador / iniciar nueva sesión,
      // lo que garantiza que el diálogo de selección siempre aparezca en un login nuevo.
      // Con localStorage la empresa quedaba "pegada" indefinidamente y el diálogo nunca se mostraba.
      const storedEmpresa = sessionStorage.getItem('activeEmpresaId');
      if (storedEmpresa) {
        const storedId = parseInt(storedEmpresa, 10);
        if (empresas.some(e => e.id === storedId)) {
          activeEmpresaId = storedId;
        }
      }

      // Restaurar sucursal activa desde localStorage — filtrar por empresa activa si existe
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

      // Primera sucursal disponible como fallback (solo si ya hay empresa seleccionada)
      if (activeSucursalId === undefined && activeEmpresaId !== undefined && user.sucursalesDisponibles?.length > 0) {
        const sucursalesFiltradas = user.sucursalesDisponibles.filter(
          s => s.empresaId === activeEmpresaId || s.empresaId == null
        );
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

  setActiveEmpresa: (id, nombre) => {
    const { user, empresasDisponibles } = get();
    // Guardar en sessionStorage (no localStorage) para que se limpie en nuevo login
    sessionStorage.setItem('activeEmpresaId', String(id));
    // Limpiar valor legacy de localStorage si existía
    localStorage.removeItem('activeEmpresaId');

    // Si la empresa no está en el store (p.ej. cuando /me falló), agregarla
    let nuevasEmpresas = empresasDisponibles;
    if (nombre && !empresasDisponibles.some(e => e.id === id)) {
      nuevasEmpresas = [...empresasDisponibles, { id, nombre }];
    }

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

    set({ activeEmpresaId: id, activeSucursalId, empresasDisponibles: nuevasEmpresas });
  },

  setIdpLogout: (fn) => set({ idpLogout: fn }),

  logout: () => {
    sessionStorage.removeItem('access_token');
    sessionStorage.removeItem('activeEmpresaId');
    localStorage.removeItem('activeSucursalId');
    localStorage.removeItem('activeEmpresaId'); // limpiar valor legacy
    localStorage.removeItem('pos-cart');
    const idpLogout = get().idpLogout;
    set({ user: null, isAuthenticated: false, activeSucursalId: undefined, activeEmpresaId: undefined, empresasDisponibles: [] });
    if (idpLogout) {
      idpLogout();
    }
  },
}));
