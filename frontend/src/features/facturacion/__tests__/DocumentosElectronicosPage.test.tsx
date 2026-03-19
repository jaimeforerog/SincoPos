import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { DocumentosElectronicosPage } from '../pages/DocumentosElectronicosPage';
import { useAuthStore } from '@/stores/auth.store';
import type { DocumentoElectronicoDTO } from '@/types/api';

vi.mock('@/api/facturacion', () => ({
  facturacionApi: {
    listarDocumentos: vi.fn(),
    reintentar: vi.fn(),
    getDocumento: vi.fn(),
    descargarXml: vi.fn(() => 'http://localhost/xml/1'),
    consultarEstadoDian: vi.fn(),
    emitirFacturaManual: vi.fn(),
    getConfiguracion: vi.fn(),
    actualizarConfiguracion: vi.fn(),
  },
}));

const makeDocumento = (overrides: Partial<DocumentoElectronicoDTO> = {}): DocumentoElectronicoDTO => ({
  id: 1,
  ventaId: 10,
  sucursalId: 1,
  nombreSucursal: 'Principal Bogotá',
  tipoDocumento: 'FV',
  prefijo: 'SETP',
  numero: 1001,
  numeroCompleto: 'SETP-1001',
  cufe: 'abc123cufe',
  fechaEmision: '2026-03-19T10:00:00Z',
  estado: 'Aceptado',
  codigoEstado: 4,
  fechaEnvioDian: '2026-03-19T10:05:00Z',
  codigoRespuestaDian: '00',
  mensajeRespuestaDian: 'Documento aceptado',
  intentos: 1,
  fechaCreacion: '2026-03-19T10:00:00Z',
  ...overrides,
});

const makePaginatedResult = (items: DocumentoElectronicoDTO[]) => ({
  items,
  totalCount: items.length,
  pageNumber: 1,
  pageSize: 20,
  totalPages: 1,
});

const makeAdminUser = () => ({
  id: 'admin-1',
  email: 'admin@test.com',
  nombre: 'Admin',
  roles: ['admin'],
  rol: 'admin' as const,
});

describe('DocumentosElectronicosPage', () => {
  beforeEach(async () => {
    vi.clearAllMocks();

    useAuthStore.setState({
      user: makeAdminUser(),
      isAuthenticated: true,
      isLoading: false,
      activeSucursalId: 1,
    });

    const { facturacionApi } = await import('@/api/facturacion');
    vi.mocked(facturacionApi.listarDocumentos).mockResolvedValue(
      makePaginatedResult([makeDocumento()])
    );
  });

  it('muestra el encabezado "Documentos Electrónicos DIAN"', async () => {
    renderWithProviders(<DocumentosElectronicosPage />);
    expect(await screen.findByText('Documentos Electrónicos DIAN')).toBeInTheDocument();
  });

  it('muestra los filtros de fecha, tipo y estado', async () => {
    renderWithProviders(<DocumentosElectronicosPage />);
    expect(await screen.findByLabelText(/fecha desde/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/fecha hasta/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/tipo/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/estado/i)).toBeInTheDocument();
  });

  it('muestra documentos en la tabla', async () => {
    renderWithProviders(<DocumentosElectronicosPage />);
    expect(await screen.findByText('SETP-1001')).toBeInTheDocument();
    expect(screen.getByText('Principal Bogotá')).toBeInTheDocument();
  });

  it('muestra el tipo de documento como chip', async () => {
    renderWithProviders(<DocumentosElectronicosPage />);
    expect(await screen.findByText('Factura Venta')).toBeInTheDocument();
  });

  it('muestra el estado Aceptado con chip', async () => {
    renderWithProviders(<DocumentosElectronicosPage />);
    expect(await screen.findByText('Aceptado')).toBeInTheDocument();
  });

  it('muestra el contador de documentos', async () => {
    renderWithProviders(<DocumentosElectronicosPage />);
    expect(await screen.findByText(/1 documento/i)).toBeInTheDocument();
  });

  it('muestra botón de reintentar para documentos rechazados (admin)', async () => {
    const { facturacionApi } = await import('@/api/facturacion');
    vi.mocked(facturacionApi.listarDocumentos).mockResolvedValue(
      makePaginatedResult([makeDocumento({ codigoEstado: 5, estado: 'Rechazado' })])
    );
    renderWithProviders(<DocumentosElectronicosPage />);
    expect(await screen.findByText('Rechazado')).toBeInTheDocument();
    // Para admin + doc rechazado: 2 botones en la fila (descarga + reintentar)
    const buttons = screen.getAllByRole('button');
    expect(buttons.length).toBeGreaterThanOrEqual(2);
  });

  it('muestra mensaje de vacío cuando no hay documentos', async () => {
    const { facturacionApi } = await import('@/api/facturacion');
    vi.mocked(facturacionApi.listarDocumentos).mockResolvedValue(makePaginatedResult([]));
    renderWithProviders(<DocumentosElectronicosPage />);
    expect(
      await screen.findByText(/no se encontraron documentos/i)
    ).toBeInTheDocument();
  });
});
