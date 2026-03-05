import { useEffect } from 'react';
import type { ReactNode } from 'react';
import type { UserInfo } from '@/types/api';
import { useAuthStore } from '@/stores/auth.store';

// Usuario de desarrollo mock
const DEV_USER: UserInfo = {
  id: 'dev-user-1',
  username: 'developer',
  email: 'dev@sincopos.com',
  nombre: 'Usuario Desarrollo',
  roles: ['Admin', 'Supervisor', 'Cajero'],
  sucursalId: 152, // Suc PromedioPonderado en base de datos sincopos
  sucursalNombre: 'Suc PromedioPonderado',
};

export function DevAuthProvider({ children }: { children: ReactNode }) {
  const { setUser } = useAuthStore();

  useEffect(() => {
    // Simular login automático en desarrollo
    setUser(DEV_USER);

    // Mock token para axios interceptor
    sessionStorage.setItem('access_token', 'dev-token-mock');
  }, [setUser]);

  return <>{children}</>;
}
