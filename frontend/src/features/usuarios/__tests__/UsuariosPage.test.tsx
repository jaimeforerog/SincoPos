import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { UsuariosPage } from '../pages/UsuariosPage';
import type { UsuarioDto } from '@/api/usuarios';

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({
    isAdmin: () => true,
    isSupervisor: () => true,
    hasAnyRole: () => true,
  }),
}));

vi.mock('@/stores/auth.store', () => ({
  useAuthStore: () => ({
    user: { id: '99', email: 'admin@test.com', role: 'admin' },
    setUser: vi.fn(),
  }),
}));

vi.mock('@/api/usuarios', () => ({
  usuariosApi: {
    listar: vi.fn(),
    me: vi.fn(),
    actualizarSucursal: vi.fn(),
    asignarSucursales: vi.fn(),
    cambiarEstado: vi.fn(),
  },
}));

vi.mock('@/api/sucursales', () => ({
  sucursalesApi: {
    listar: vi.fn().mockResolvedValue([
      { id: 1, nombre: 'Principal', activo: true },
    ]),
    getAll: vi.fn().mockResolvedValue([
      { id: 1, nombre: 'Principal', activo: true },
    ]),
  },
}));

const makeUsuario = (overrides: Partial<UsuarioDto> = {}): UsuarioDto => ({
  id: 1,
  externalId: 'ext-001',
  email: 'juan.perez@empresa.com',
  nombreCompleto: 'Juan Pérez',
  rol: 'cajero',
  activo: true,
  fechaCreacion: '2026-01-01T00:00:00Z',
  sucursalesAsignadas: [{ id: 1, nombre: 'Principal' }],
  ...overrides,
});

describe('UsuariosPage', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    const { usuariosApi } = await import('@/api/usuarios');
    vi.mocked(usuariosApi.listar).mockResolvedValue([makeUsuario()]);
  });

  it('muestra el encabezado "Usuarios"', async () => {
    renderWithProviders(<UsuariosPage />);
    // Aparece en breadcrumb + h5
    expect((await screen.findAllByText('Usuarios')).length).toBeGreaterThanOrEqual(1);
  });

  it('muestra el campo de búsqueda', async () => {
    renderWithProviders(<UsuariosPage />);
    expect(
      await screen.findByPlaceholderText(/buscar por nombre o email/i)
    ).toBeInTheDocument();
  });

  it('muestra los encabezados de la tabla', async () => {
    renderWithProviders(<UsuariosPage />);
    expect(await screen.findByText('Nombre')).toBeInTheDocument();
    expect(screen.getByText('Email')).toBeInTheDocument();
    // "Rol" aparece en encabezado de tabla Y en el chip del filtro de roles
    expect(screen.getAllByText('Rol').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Estado').length).toBeGreaterThanOrEqual(1);
  });

  it('muestra los usuarios en la tabla', async () => {
    renderWithProviders(<UsuariosPage />);
    expect(await screen.findByText('Juan Pérez')).toBeInTheDocument();
    expect(screen.getByText('juan.perez@empresa.com')).toBeInTheDocument();
  });

  it('muestra el chip de rol "Cajero"', async () => {
    renderWithProviders(<UsuariosPage />);
    expect(await screen.findByText('Cajero')).toBeInTheDocument();
  });

  it('muestra chip "Activo" para usuarios activos', async () => {
    renderWithProviders(<UsuariosPage />);
    expect(await screen.findByText('Activo')).toBeInTheDocument();
  });

  it('muestra el botón "Nuevo Usuario" para admin', async () => {
    renderWithProviders(<UsuariosPage />);
    expect(
      await screen.findByRole('button', { name: /nuevo usuario/i })
    ).toBeInTheDocument();
  });

  it('muestra "No se encontraron usuarios" cuando la lista está vacía', async () => {
    const { usuariosApi } = await import('@/api/usuarios');
    vi.mocked(usuariosApi.listar).mockResolvedValue([]);

    renderWithProviders(<UsuariosPage />);
    expect(await screen.findByText(/no se encontraron usuarios/i)).toBeInTheDocument();
  });
});
