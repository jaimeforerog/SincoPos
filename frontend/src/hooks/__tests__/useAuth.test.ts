import { describe, it, expect, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useAuth } from '../useAuth';
import { useAuthStore } from '@/stores/auth.store';
import type { UserInfo } from '@/types/api';

const makeUser = (roles: string[]): UserInfo => ({
  id: 'u1',
  username: 'testuser',
  email: 'test@sincopos.com',
  nombre: 'Test User',
  roles,
  sucursalId: 1,
  sucursalNombre: 'Principal',
  sucursalesDisponibles: [{ id: 1, nombre: 'Principal', empresaId: 1 }],
});

beforeEach(() => {
  useAuthStore.setState({
    user: null,
    isAuthenticated: false,
    isLoading: false,
    activeSucursalId: undefined,
  });
});

describe('useAuth — hasRole', () => {
  it('retorna true cuando el usuario tiene el rol', () => {
    useAuthStore.setState({ user: makeUser(['admin']), isAuthenticated: true });
    const { result } = renderHook(() => useAuth());

    expect(result.current.hasRole('admin')).toBe(true);
  });

  it('es case-insensitive', () => {
    useAuthStore.setState({ user: makeUser(['Admin']), isAuthenticated: true });
    const { result } = renderHook(() => useAuth());

    expect(result.current.hasRole('admin')).toBe(true);
    expect(result.current.hasRole('ADMIN')).toBe(true);
  });

  it('retorna false si el usuario no tiene el rol', () => {
    useAuthStore.setState({ user: makeUser(['cajero']), isAuthenticated: true });
    const { result } = renderHook(() => useAuth());

    expect(result.current.hasRole('admin')).toBe(false);
  });

  it('retorna false si no hay usuario', () => {
    const { result } = renderHook(() => useAuth());

    expect(result.current.hasRole('admin')).toBe(false);
  });
});

describe('useAuth — hasAnyRole', () => {
  it('retorna true si algún rol coincide', () => {
    useAuthStore.setState({ user: makeUser(['supervisor']), isAuthenticated: true });
    const { result } = renderHook(() => useAuth());

    expect(result.current.hasAnyRole(['admin', 'supervisor'])).toBe(true);
  });

  it('retorna false con lista vacía', () => {
    useAuthStore.setState({ user: makeUser(['admin']), isAuthenticated: true });
    const { result } = renderHook(() => useAuth());

    expect(result.current.hasAnyRole([])).toBe(false);
  });

  it('retorna false si ningún rol coincide', () => {
    useAuthStore.setState({ user: makeUser(['cajero']), isAuthenticated: true });
    const { result } = renderHook(() => useAuth());

    expect(result.current.hasAnyRole(['admin', 'supervisor'])).toBe(false);
  });
});

describe('useAuth — isAdmin', () => {
  it('retorna true solo con rol admin', () => {
    useAuthStore.setState({ user: makeUser(['admin']), isAuthenticated: true });
    const { result } = renderHook(() => useAuth());

    expect(result.current.isAdmin()).toBe(true);
  });

  it('retorna false con otros roles', () => {
    useAuthStore.setState({ user: makeUser(['supervisor']), isAuthenticated: true });
    const { result } = renderHook(() => useAuth());

    expect(result.current.isAdmin()).toBe(false);
  });
});

describe('useAuth — isSupervisor', () => {
  it('retorna true con rol supervisor', () => {
    useAuthStore.setState({ user: makeUser(['supervisor']), isAuthenticated: true });
    const { result } = renderHook(() => useAuth());

    expect(result.current.isSupervisor()).toBe(true);
  });

  it('retorna true con rol admin (admin hereda supervisor)', () => {
    useAuthStore.setState({ user: makeUser(['admin']), isAuthenticated: true });
    const { result } = renderHook(() => useAuth());

    expect(result.current.isSupervisor()).toBe(true);
  });

  it('retorna false con rol cajero', () => {
    useAuthStore.setState({ user: makeUser(['cajero']), isAuthenticated: true });
    const { result } = renderHook(() => useAuth());

    expect(result.current.isSupervisor()).toBe(false);
  });
});

describe('useAuth — isCajero', () => {
  it('retorna true con rol cajero', () => {
    useAuthStore.setState({ user: makeUser(['cajero']), isAuthenticated: true });
    const { result } = renderHook(() => useAuth());

    expect(result.current.isCajero()).toBe(true);
  });

  it('retorna true con rol supervisor', () => {
    useAuthStore.setState({ user: makeUser(['supervisor']), isAuthenticated: true });
    const { result } = renderHook(() => useAuth());

    expect(result.current.isCajero()).toBe(true);
  });

  it('retorna true con rol admin', () => {
    useAuthStore.setState({ user: makeUser(['admin']), isAuthenticated: true });
    const { result } = renderHook(() => useAuth());

    expect(result.current.isCajero()).toBe(true);
  });

  it('retorna false sin usuario', () => {
    const { result } = renderHook(() => useAuth());

    expect(result.current.isCajero()).toBe(false);
  });
});
