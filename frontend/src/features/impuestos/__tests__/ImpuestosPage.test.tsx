import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/test/test-utils';
import ImpuestosPage from '../pages/ImpuestosPage';
import type { ImpuestoDTO, RetencionReglaDTO, ConceptoRetencionDTO } from '@/types/api';

vi.mock('@/api/impuestos', () => ({
  impuestosApi: {
    getAll: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    deactivate: vi.fn(),
  },
  retencionesApi: {
    getAll: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    deactivate: vi.fn(),
  },
  conceptosRetencionApi: {
    getAll: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    deactivate: vi.fn(),
  },
}));

const makeImpuesto = (overrides: Partial<ImpuestoDTO> = {}): ImpuestoDTO => ({
  id: 1,
  nombre: 'IVA General',
  tipo: 'IVA',
  porcentaje: 0.19,
  valorFijo: undefined,
  aplicaSobreBase: true,
  codigoCuentaContable: '2408',
  descripcion: 'IVA 19%',
  codigoPais: 'CO',
  ...overrides,
});

const makeRetencion = (overrides: Partial<RetencionReglaDTO> = {}): RetencionReglaDTO => ({
  id: 1,
  nombre: 'ReteFuente Servicios',
  tipo: 'ReteFuente',
  porcentaje: 0.04,
  baseMinUVT: 4,
  perfilVendedor: 'REGIMEN_ORDINARIO',
  perfilComprador: 'GRAN_CONTRIBUYENTE',
  activo: true,
  ...overrides,
});

const makeConcepto = (overrides: Partial<ConceptoRetencionDTO> = {}): ConceptoRetencionDTO => ({
  id: 1,
  nombre: 'Servicios Generales',
  codigoDian: '1001',
  porcentajeSugerido: 0.04,
  activo: true,
  ...overrides,
});

describe('ImpuestosPage', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    const { impuestosApi, retencionesApi, conceptosRetencionApi } = await import('@/api/impuestos');
    vi.mocked(impuestosApi.getAll).mockResolvedValue([makeImpuesto()]);
    vi.mocked(retencionesApi.getAll).mockResolvedValue([makeRetencion()]);
    vi.mocked(conceptosRetencionApi.getAll).mockResolvedValue([makeConcepto()]);
  });

  it('muestra el encabezado "Motor de Impuestos"', async () => {
    renderWithProviders(<ImpuestosPage />);
    expect((await screen.findAllByText('Motor de Impuestos')).length).toBeGreaterThanOrEqual(1);
  });

  it('muestra las pestañas de Impuestos, Retenciones y Conceptos', async () => {
    renderWithProviders(<ImpuestosPage />);
    expect(await screen.findByRole('button', { name: /impuestos \(iva, inc/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /retenciones/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /conceptos retencion dian/i })).toBeInTheDocument();
  });

  it('muestra la pestaña de Impuestos activa por defecto', async () => {
    renderWithProviders(<ImpuestosPage />);
    expect(await screen.findByRole('button', { name: /nuevo impuesto/i })).toBeInTheDocument();
  });

  it('muestra los impuestos en la tabla', async () => {
    renderWithProviders(<ImpuestosPage />);
    expect(await screen.findByText('IVA General')).toBeInTheDocument();
  });

  it('muestra los encabezados de la tabla de impuestos', async () => {
    renderWithProviders(<ImpuestosPage />);
    expect(await screen.findByText('Nombre')).toBeInTheDocument();
    expect(screen.getByText('Tipo')).toBeInTheDocument();
    expect(screen.getByText('Tarifa')).toBeInTheDocument();
  });

  it('cambia a la pestaña de Retenciones al hacer clic', async () => {
    renderWithProviders(<ImpuestosPage />);
    await screen.findByRole('button', { name: /impuestos \(iva, inc/i });
    await userEvent.click(screen.getByRole('button', { name: /^retenciones$/i }));
    expect(await screen.findByRole('button', { name: /nueva retencion/i })).toBeInTheDocument();
    expect(await screen.findByText('ReteFuente Servicios')).toBeInTheDocument();
  });

  it('cambia a la pestaña de Conceptos al hacer clic', async () => {
    renderWithProviders(<ImpuestosPage />);
    await screen.findByRole('button', { name: /impuestos \(iva, inc/i });
    await userEvent.click(screen.getByRole('button', { name: /conceptos retencion dian/i }));
    expect(await screen.findByRole('button', { name: /nuevo concepto/i })).toBeInTheDocument();
    expect(await screen.findByText('Servicios Generales')).toBeInTheDocument();
  });
});
