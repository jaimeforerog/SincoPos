import { describe, it, expect, beforeEach } from 'vitest';
import { useAuthStore } from '../auth.store';
import type { UserInfo } from '@/types/api';

const makeUser = (overrides: Partial<UserInfo> = {}): UserInfo => ({
  id: 'u1',
  username: 'testuser',
  email: 'test@sincopos.com',
  nombre: 'Test User',
  roles: ['cajero'],
  sucursalId: 1,
  sucursalNombre: 'Principal',
  sucursalesDisponibles: [{ id: 1, nombre: 'Principal', empresaId: 1 }, { id: 2, nombre: 'Norte', empresaId: 1 }],
  ...overrides,
});

beforeEach(() => {
  localStorage.clear();
  sessionStorage.clear();
  useAuthStore.setState({
    user: null,
    isAuthenticated: false,
    isLoading: true,
    activeSucursalId: undefined,
  });
});

describe('auth.store — setUser', () => {
  it('guarda usuario y activa isAuthenticated', () => {
    useAuthStore.getState().setUser(makeUser());

    const { user, isAuthenticated, isLoading } = useAuthStore.getState();
    expect(user?.email).toBe('test@sincopos.com');
    expect(isAuthenticated).toBe(true);
    expect(isLoading).toBe(false);
  });

  it('usa sucursalId del usuario como activeSucursalId por defecto', () => {
    useAuthStore.getState().setUser(makeUser({ sucursalId: 1 }));

    expect(useAuthStore.getState().activeSucursalId).toBe(1);
  });

  it('restaura activeSucursalId desde localStorage si es válido', () => {
    localStorage.setItem('activeSucursalId', '2');
    useAuthStore.getState().setUser(makeUser());

    expect(useAuthStore.getState().activeSucursalId).toBe(2);
  });

  it('ignora activeSucursalId de localStorage si la sucursal no pertenece al usuario', () => {
    localStorage.setItem('activeSucursalId', '99');
    useAuthStore.getState().setUser(makeUser());

    // Cae al sucursalId por defecto del usuario
    expect(useAuthStore.getState().activeSucursalId).toBe(1);
  });

  it('setUser(null) limpia autenticación', () => {
    useAuthStore.getState().setUser(makeUser());
    useAuthStore.getState().setUser(null);

    expect(useAuthStore.getState().user).toBeNull();
    expect(useAuthStore.getState().isAuthenticated).toBe(false);
  });
});

describe('auth.store — setActiveSucursal', () => {
  it('actualiza activeSucursalId y persiste en localStorage', () => {
    useAuthStore.getState().setActiveSucursal(2);

    expect(useAuthStore.getState().activeSucursalId).toBe(2);
    expect(localStorage.getItem('activeSucursalId')).toBe('2');
  });
});

describe('auth.store — logout', () => {
  it('limpia usuario, activeSucursalId, sessionStorage y localStorage', () => {
    sessionStorage.setItem('access_token', 'token-abc');
    localStorage.setItem('activeSucursalId', '1');
    useAuthStore.getState().setUser(makeUser());

    useAuthStore.getState().logout();

    const { user, isAuthenticated, activeSucursalId } = useAuthStore.getState();
    expect(user).toBeNull();
    expect(isAuthenticated).toBe(false);
    expect(activeSucursalId).toBeUndefined();
    expect(sessionStorage.getItem('access_token')).toBeNull();
    expect(localStorage.getItem('activeSucursalId')).toBeNull();
  });
});
