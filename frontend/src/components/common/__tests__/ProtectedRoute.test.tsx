import { describe, it, expect, beforeEach } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { ProtectedRoute } from '../ProtectedRoute';
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
  sucursalesDisponibles: [{ id: 1, nombre: 'Principal' }],
});

beforeEach(() => {
  useAuthStore.setState({
    user: null,
    isAuthenticated: false,
    isLoading: false,
    activeSucursalId: undefined,
  });
});

describe('ProtectedRoute', () => {
  it('muestra CircularProgress mientras isLoading es true', () => {
    useAuthStore.setState({ isLoading: true });
    renderWithProviders(
      <ProtectedRoute>
        <div>Contenido protegido</div>
      </ProtectedRoute>
    );

    // MUI CircularProgress usa role="progressbar"
    expect(screen.getByRole('progressbar')).toBeInTheDocument();
    expect(screen.queryByText('Contenido protegido')).not.toBeInTheDocument();
  });

  it('redirige a /login cuando no está autenticado', () => {
    renderWithProviders(
      <ProtectedRoute>
        <div>Contenido protegido</div>
      </ProtectedRoute>,
      { initialEntries: ['/dashboard'] }
    );

    // El contenido no debe mostrarse
    expect(screen.queryByText('Contenido protegido')).not.toBeInTheDocument();
  });

  it('renderiza children cuando está autenticado sin roles requeridos', () => {
    useAuthStore.setState({ user: makeUser(['cajero']), isAuthenticated: true, isLoading: false });

    renderWithProviders(
      <ProtectedRoute>
        <div>Contenido protegido</div>
      </ProtectedRoute>
    );

    expect(screen.getByText('Contenido protegido')).toBeInTheDocument();
  });

  it('renderiza children cuando el usuario tiene el rol requerido', () => {
    useAuthStore.setState({ user: makeUser(['admin']), isAuthenticated: true, isLoading: false });

    renderWithProviders(
      <ProtectedRoute requiredRoles={['admin']}>
        <div>Panel Admin</div>
      </ProtectedRoute>
    );

    expect(screen.getByText('Panel Admin')).toBeInTheDocument();
  });

  it('redirige a /unauthorized cuando el rol es insuficiente', () => {
    useAuthStore.setState({ user: makeUser(['cajero']), isAuthenticated: true, isLoading: false });

    renderWithProviders(
      <ProtectedRoute requiredRoles={['admin']}>
        <div>Solo Admin</div>
      </ProtectedRoute>
    );

    expect(screen.queryByText('Solo Admin')).not.toBeInTheDocument();
  });

  it('acepta cualquiera de los roles requeridos', () => {
    useAuthStore.setState({ user: makeUser(['supervisor']), isAuthenticated: true, isLoading: false });

    renderWithProviders(
      <ProtectedRoute requiredRoles={['admin', 'supervisor']}>
        <div>Panel Supervisor</div>
      </ProtectedRoute>
    );

    expect(screen.getByText('Panel Supervisor')).toBeInTheDocument();
  });
});
