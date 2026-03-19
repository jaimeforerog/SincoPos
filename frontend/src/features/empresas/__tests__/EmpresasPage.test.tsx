import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { EmpresasPage } from '../pages/EmpresasPage';
import type { EmpresaDTO } from '@/types/api';

vi.mock('@/api/empresas', () => ({
  empresasApi: {
    getAll: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    getById: vi.fn(),
  },
}));

const makeEmpresa = (overrides: Partial<EmpresaDTO> = {}): EmpresaDTO => ({
  id: 1,
  nombre: 'Distribuidora Central S.A.S',
  nit: '900123456-7',
  razonSocial: 'Distribuidora Central S.A.S Razón',
  activo: true,
  ...overrides,
});

describe('EmpresasPage', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    const { empresasApi } = await import('@/api/empresas');
    vi.mocked(empresasApi.getAll).mockResolvedValue([makeEmpresa()]);
  });

  it('muestra el encabezado "Empresas"', async () => {
    renderWithProviders(<EmpresasPage />);
    expect(await screen.findByText('Empresas')).toBeInTheDocument();
  });

  it('muestra el subtítulo descriptivo', async () => {
    renderWithProviders(<EmpresasPage />);
    expect(await screen.findByText(/gestión de empresas/i)).toBeInTheDocument();
  });

  it('muestra el botón "Nueva empresa"', async () => {
    renderWithProviders(<EmpresasPage />);
    expect(await screen.findByRole('button', { name: /nueva empresa/i })).toBeInTheDocument();
  });

  it('muestra los encabezados de la tabla', async () => {
    renderWithProviders(<EmpresasPage />);
    expect(await screen.findByText('Nombre')).toBeInTheDocument();
    expect(screen.getByText('NIT')).toBeInTheDocument();
    expect(screen.getByText('Razón social')).toBeInTheDocument();
  });

  it('muestra las empresas en la tabla', async () => {
    renderWithProviders(<EmpresasPage />);
    expect(await screen.findByText('Distribuidora Central S.A.S')).toBeInTheDocument();
    expect(screen.getByText('900123456-7')).toBeInTheDocument();
  });

  it('muestra chip "Activa" para empresas activas', async () => {
    renderWithProviders(<EmpresasPage />);
    expect(await screen.findByText('Activa')).toBeInTheDocument();
  });

  it('muestra "Sin empresas registradas" cuando la lista está vacía', async () => {
    const { empresasApi } = await import('@/api/empresas');
    vi.mocked(empresasApi.getAll).mockResolvedValue([]);
    renderWithProviders(<EmpresasPage />);
    expect(await screen.findByText(/no hay empresas registradas/i)).toBeInTheDocument();
  });
});
