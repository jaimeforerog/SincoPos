import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, fireEvent, waitFor } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { InteligenciaColectivaPage } from '../pages/InteligenciaColectivaPage';
import type { ComboProductoDto, PatronComparativoDto, EstadoGlobalDto } from '@/api/colectiva';

// ── Mocks ──────────────────────────────────────────────────────────────────

const mockGetCombos          = vi.fn();
const mockCompararSucursales = vi.fn();
const mockGetEstadoGlobal    = vi.fn();

vi.mock('@/api/colectiva', () => ({
  colectivaApi: {
    getCombos:          (...a: unknown[]) => mockGetCombos(...a),
    compararSucursales: (...a: unknown[]) => mockCompararSucursales(...a),
    getEstadoGlobal:    (...a: unknown[]) => mockGetEstadoGlobal(...a),
  },
}));

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({
    activeSucursalId: 1,
    activeEmpresaId: 1,
    hasAnyRole: () => true,
  }),
}));

// ── Fixtures ───────────────────────────────────────────────────────────────

const combos: ComboProductoDto[] = [
  {
    productoAId: 'p1', productoANombre: 'Arroz Premium',
    productoBId: 'p2', productoBNombre: 'Aceite Vegetal',
    vecesJuntos: 42, frecuencia: 0.35,
  },
  {
    productoAId: 'p1', productoANombre: 'Arroz Premium',
    productoBId: 'p3', productoBNombre: 'Sal Refinada',
    vecesJuntos: 28, frecuencia: 0.23,
  },
];

const comparativo: PatronComparativoDto = {
  sucursales: ['Norte', 'Sur'],
  items: [
    {
      productoId: 'p1',
      nombreProducto: 'Arroz Premium',
      velocidadPorSucursal: { Norte: 150, Sur: 80 },
    },
  ],
};

const estadoGlobal: EstadoGlobalDto = {
  servicioCentralDisponible: false,
  mensaje: 'Modo local activo. El servicio central Sinco no está disponible.',
  ultimaActualizacionGlobal: null,
};

// ── Tests ──────────────────────────────────────────────────────────────────

describe('InteligenciaColectivaPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetCombos.mockResolvedValue(combos);
    mockCompararSucursales.mockResolvedValue(comparativo);
    mockGetEstadoGlobal.mockResolvedValue(estadoGlobal);
  });

  it('muestra el título "Inteligencia Colectiva"', () => {
    renderWithProviders(<InteligenciaColectivaPage />);
    expect(screen.getByText('Inteligencia Colectiva')).toBeInTheDocument();
  });

  it('muestra las tres pestañas', () => {
    renderWithProviders(<InteligenciaColectivaPage />);
    expect(screen.getByText('Combos de Productos')).toBeInTheDocument();
    expect(screen.getByText('Comparación Sucursales')).toBeInTheDocument();
    expect(screen.getByText('Estado Global')).toBeInTheDocument();
  });

  it('muestra los combos de productos en la primera pestaña', async () => {
    renderWithProviders(<InteligenciaColectivaPage />);
    const arroz = await screen.findAllByText('Arroz Premium');
    expect(arroz.length).toBeGreaterThan(0);
    expect(screen.getByText('Aceite Vegetal')).toBeInTheDocument();
  });

  it('muestra el número de veces juntos', async () => {
    renderWithProviders(<InteligenciaColectivaPage />);
    await screen.findAllByText('Arroz Premium');
    expect(screen.getByText('42')).toBeInTheDocument();
    expect(screen.getByText('28')).toBeInTheDocument();
  });

  it('muestra "Sin datos de combos" cuando la lista está vacía', async () => {
    mockGetCombos.mockResolvedValue([]);
    renderWithProviders(<InteligenciaColectivaPage />);
    await screen.findByText(/sin datos de combos/i);
  });

  it('llama a getCombos con el sucursalId activo', async () => {
    renderWithProviders(<InteligenciaColectivaPage />);
    await waitFor(() => {
      expect(mockGetCombos).toHaveBeenCalledWith(1, 15);
    });
  });

  it('cambia a la pestaña de comparación y muestra sucursales', async () => {
    renderWithProviders(<InteligenciaColectivaPage />);
    fireEvent.click(screen.getByText('Comparación Sucursales'));
    await screen.findByText('Norte');
    expect(screen.getByText('Sur')).toBeInTheDocument();
  });

  it('la pestaña comparación muestra el producto top', async () => {
    renderWithProviders(<InteligenciaColectivaPage />);
    fireEvent.click(screen.getByText('Comparación Sucursales'));
    await waitFor(() => {
      expect(mockCompararSucursales).toHaveBeenCalledWith(1);
    });
    await screen.findByText('Arroz Premium');
  });

  it('cambia a la pestaña Estado Global y muestra el mensaje', async () => {
    renderWithProviders(<InteligenciaColectivaPage />);
    fireEvent.click(screen.getByText('Estado Global'));
    await screen.findByText('Servicio Central Sinco — No disponible (modo local)');
    await screen.findByText(/modo local activo/i);
  });

  it('muestra el estado ✅ Activo para patrones locales', async () => {
    renderWithProviders(<InteligenciaColectivaPage />);
    fireEvent.click(screen.getByText('Estado Global'));
    await screen.findByText('Servicio Central Sinco — No disponible (modo local)');
    const chips = screen.getAllByText('✅ Activo');
    expect(chips.length).toBeGreaterThanOrEqual(3);
  });

  it('muestra el estado 🔮 Futuro para propagación global', async () => {
    renderWithProviders(<InteligenciaColectivaPage />);
    fireEvent.click(screen.getByText('Estado Global'));
    await screen.findByText('Servicio Central Sinco — No disponible (modo local)');
    const futuro = screen.getAllByText('🔮 Futuro');
    expect(futuro.length).toBeGreaterThanOrEqual(3);
  });
});
