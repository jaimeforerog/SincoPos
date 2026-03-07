import { create } from 'zustand';
import type { UserInfo } from '@/types/api';

interface AuthState {
  user: UserInfo | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  activeSucursalId: number | undefined;
  /** IdP-specific logout function, set by AuthProvider */
  idpLogout: (() => void) | null;
  setUser: (user: UserInfo | null) => void;
  setLoading: (loading: boolean) => void;
  setActiveSucursal: (id: number) => void;
  setIdpLogout: (fn: () => void) => void;
  logout: () => void;
}

export const useAuthStore = create<AuthState>((set, get) => ({
  user: null,
  isAuthenticated: false,
  isLoading: true,
  activeSucursalId: undefined,
  idpLogout: null,
  setUser: (user) => {
    let activeSucursalId: number | undefined = user?.sucursalId;

    if (user) {
      // Restaurar sucursal activa desde localStorage si existe y es válida
      const stored = localStorage.getItem('activeSucursalId');
      if (stored) {
        const storedId = parseInt(stored, 10);
        const isValid = user.sucursalesDisponibles.some(s => s.id === storedId);
        if (isValid) activeSucursalId = storedId;
      }

      // Si aún no hay sucursal activa, usar la primera disponible
      if (activeSucursalId === undefined && user.sucursalesDisponibles?.length > 0) {
        activeSucursalId = user.sucursalesDisponibles[0].id;
        localStorage.setItem('activeSucursalId', String(activeSucursalId));
      }
    }

    set({ user, isAuthenticated: user !== null, isLoading: false, activeSucursalId });
  },
  setLoading: (loading) => set({ isLoading: loading }),
  setActiveSucursal: (id) => {
    localStorage.setItem('activeSucursalId', String(id));
    set({ activeSucursalId: id });
  },
  setIdpLogout: (fn) => set({ idpLogout: fn }),
  logout: () => {
    sessionStorage.removeItem('access_token');
    localStorage.removeItem('activeSucursalId');
    const idpLogout = get().idpLogout;
    set({ user: null, isAuthenticated: false, activeSucursalId: undefined });
    if (idpLogout) {
      idpLogout();
    }
  },
}));
