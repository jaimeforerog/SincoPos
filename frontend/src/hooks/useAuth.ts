import { useAuthStore } from '@/stores/auth.store';

export const useAuth = () => {
  const { user, isAuthenticated, isLoading } = useAuthStore();

  const hasRole = (role: string): boolean => {
    return user?.roles?.some(r => r.toLowerCase() === role.toLowerCase()) ?? false;
  };

  const hasAnyRole = (roles: string[]): boolean => {
    return roles.some((role) => hasRole(role));
  };

  const isAdmin = (): boolean => {
    return hasRole('Admin');
  };

  const isSupervisor = (): boolean => {
    return hasRole('Supervisor') || hasRole('Admin');
  };

  const isCajero = (): boolean => {
    return hasRole('Cajero') || hasRole('Supervisor') || hasRole('Admin');
  };

  return {
    user,
    isAuthenticated,
    isLoading,
    hasRole,
    hasAnyRole,
    isAdmin,
    isSupervisor,
    isCajero,
  };
};
