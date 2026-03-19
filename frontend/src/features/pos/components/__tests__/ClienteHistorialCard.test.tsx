import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { ClienteHistorialCard } from '../ClienteHistorialCard';
import type { ClienteHistorialDTO } from '@/types/api';

// ── Mock de tercerosApi ────────────────────────────────────────────────────

const mockGetHistorial = vi.fn();

vi.mock('@/api/terceros', () => ({
  tercerosApi: {
    getHistorial: (...args: unknown[]) => mockGetHistorial(...args),
  },
}));

// ── Fixtures ───────────────────────────────────────────────────────────────

const historialConCompras: ClienteHistorialDTO = {
  clienteId:          42,
  totalCompras:       15,
  totalGastado:       750_000,
  gastoPromedio:      50_000,
  primeraVisita:      '2025-01-10T00:00:00Z',
  ultimaVisita:       '2026-03-15T00:00:00Z',
  topProductos: [
    { productoId: 'p1', nombreProducto: 'Arroz Premium',  cantidadTotal: 12 },
    { productoId: 'p2', nombreProducto: 'Aceite Vegetal', cantidadTotal: 8  },
    { productoId: 'p3', nombreProducto: 'Sal Refinada',   cantidadTotal: 5  },
  ],
  visitasPorDiaSemana: { Lunes: 3, Martes: 2 },
  visitasPorHora:      { '10': 5, '14': 3 },
};

const historialSinCompras: ClienteHistorialDTO = {
  clienteId:          99,
  totalCompras:       0,
  totalGastado:       0,
  gastoPromedio:      0,
  topProductos:       [],
  visitasPorDiaSemana:{},
  visitasPorHora:     {},
};

// ── Tests ──────────────────────────────────────────────────────────────────

describe('ClienteHistorialCard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('no hace la query cuando clienteId <= 0', () => {
    renderWithProviders(<ClienteHistorialCard clienteId={0} />);
    expect(mockGetHistorial).not.toHaveBeenCalled();
  });

  it('muestra skeleton mientras carga', () => {
    // La query nunca resuelve → estado isLoading
    mockGetHistorial.mockReturnValue(new Promise(() => {}));

    renderWithProviders(<ClienteHistorialCard clienteId={42} />);

    // MUI Skeleton renderiza con role="progressbar" o simplemente el componente
    // Buscamos el skeleton por su clase
    const skeleton = document.querySelector('.MuiSkeleton-root');
    expect(skeleton).toBeInTheDocument();
  });

  it('retorna null cuando totalCompras === 0', async () => {
    mockGetHistorial.mockResolvedValue(historialSinCompras);

    const { container } = renderWithProviders(<ClienteHistorialCard clienteId={99} />);

    await waitFor(() => {
      expect(mockGetHistorial).toHaveBeenCalledWith(99);
    });

    // Después de resolver, no debe renderizar nada
    await waitFor(() => {
      expect(container).toBeEmptyDOMElement();
    });
  });

  it('muestra el historial del cliente con compras', async () => {
    mockGetHistorial.mockResolvedValue(historialConCompras);

    renderWithProviders(<ClienteHistorialCard clienteId={42} />);

    await waitFor(() => {
      expect(screen.getByText('Historial del cliente')).toBeInTheDocument();
    });
  });

  it('muestra el chip con el total de compras', async () => {
    mockGetHistorial.mockResolvedValue(historialConCompras);

    renderWithProviders(<ClienteHistorialCard clienteId={42} />);

    await screen.findByText('15 compras');
  });

  it('muestra el chip con el gasto promedio formateado en COP', async () => {
    mockGetHistorial.mockResolvedValue(historialConCompras);

    renderWithProviders(<ClienteHistorialCard clienteId={42} />);

    await waitFor(() => {
      // gastoPromedio = 50_000 → "Prom. $ 50.000" (locale es-CO)
      expect(screen.getByText(/Prom\./i)).toBeInTheDocument();
      expect(screen.getByText(/50/)).toBeInTheDocument();
    });
  });

  it('muestra los top productos', async () => {
    mockGetHistorial.mockResolvedValue(historialConCompras);

    renderWithProviders(<ClienteHistorialCard clienteId={42} />);

    await screen.findByText('Arroz Premium');
    expect(screen.getByText('Aceite Vegetal')).toBeInTheDocument();
    expect(screen.getByText('Sal Refinada')).toBeInTheDocument();
  });

  it('muestra el label "Suele comprar:"', async () => {
    mockGetHistorial.mockResolvedValue(historialConCompras);

    renderWithProviders(<ClienteHistorialCard clienteId={42} />);

    await screen.findByText(/Suele comprar:/i);
  });

  it('llama a getHistorial con el clienteId correcto', async () => {
    mockGetHistorial.mockResolvedValue(historialConCompras);

    renderWithProviders(<ClienteHistorialCard clienteId={42} />);

    await waitFor(() => {
      expect(mockGetHistorial).toHaveBeenCalledWith(42);
    });
  });

  it('no muestra la sección de top productos cuando la lista está vacía', async () => {
    const historialSinTop: ClienteHistorialDTO = {
      ...historialConCompras,
      topProductos: [],
    };
    mockGetHistorial.mockResolvedValue(historialSinTop);

    renderWithProviders(<ClienteHistorialCard clienteId={42} />);

    await screen.findByText('Historial del cliente');
    expect(screen.queryByText(/Suele comprar:/i)).not.toBeInTheDocument();
  });

  it('muestra máximo 4 top productos', async () => {
    const historialCinco: ClienteHistorialDTO = {
      ...historialConCompras,
      topProductos: [
        { productoId: 'p1', nombreProducto: 'Prod A', cantidadTotal: 10 },
        { productoId: 'p2', nombreProducto: 'Prod B', cantidadTotal: 9  },
        { productoId: 'p3', nombreProducto: 'Prod C', cantidadTotal: 8  },
        { productoId: 'p4', nombreProducto: 'Prod D', cantidadTotal: 7  },
        { productoId: 'p5', nombreProducto: 'Prod E', cantidadTotal: 6  },
      ],
    };
    mockGetHistorial.mockResolvedValue(historialCinco);

    renderWithProviders(<ClienteHistorialCard clienteId={42} />);

    await screen.findByText('Prod A');
    expect(screen.getByText('Prod D')).toBeInTheDocument();
    // El 5to producto no debe aparecer
    expect(screen.queryByText('Prod E')).not.toBeInTheDocument();
  });
});
