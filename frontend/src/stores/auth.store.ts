import { create } from 'zustand';
import type { UserInfo } from '@/types/api';

interface AuthState {
  user: UserInfo | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  activeSucursalId: number | undefined;
  setUser: (user: UserInfo | null) => void;
  setLoading: (loading: boolean) => void;
  setActiveSucursal: (id: number) => void;
  logout: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  isAuthenticated: false,
  isLoading: true,
  activeSucursalId: undefined,
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
    }

    set({ user, isAuthenticated: user !== null, isLoading: false, activeSucursalId });
  },
  setLoading: (loading) => set({ isLoading: loading }),
  setActiveSucursal: (id) => {
    localStorage.setItem('activeSucursalId', String(id));
    set({ activeSucursalId: id });
  },
  logout: () => {
    sessionStorage.removeItem('access_token');
    localStorage.removeItem('activeSucursalId');
    set({ user: null, isAuthenticated: false, activeSucursalId: undefined });
  },
}));
