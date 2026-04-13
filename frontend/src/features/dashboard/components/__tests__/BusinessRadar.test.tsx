import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { BusinessRadar } from '../BusinessRadar';
import type { MetricasDelDiaDTO, VentaPorHoraDTO, AlertaStockDTO } from '@/types/api';

// ── Mock recharts ──────────────────────────────────────────────────────────

vi.mock('recharts', () => ({
  // Don't pass children through AreaChart to avoid jsdom SVG-casing warnings
  // from inline <defs>/<linearGradient>/<stop> elements in the component.
  AreaChart:           () => <div data-testid="area-chart" />,
  Area:                () => null,
  XAxis:               () => null,
  YAxis:               () => null,
  Tooltip:             () => null,
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  ReferenceLine:       () => null,
}));

// ── Fixtures ───────────────────────────────────────────────────────────────

const metricasBase: MetricasDelDiaDTO = {
  ventasTotales:     500_000,
  ventasAyer:        450_000,
  porcentajeCambio:  11.1,
  cantidadVentas:    20,
  productosVendidos: 40,
  clientesAtendidos: 18,
  ticketPromedio:    25_000,
  utilidadDelDia:    150_000,
  margenPromedio:    30,
};

const ventasPorHora: VentaPorHoraDTO[] = [
  { hora: 8,  total: 50_000,  cantidad: 2 },
  { hora: 9,  total: 80_000,  cantidad: 3 },
  { hora: 10, total: 120_000, cantidad: 5 },
];

const stockRisksBase: AlertaStockDTO[] = [
  {
    productoId:     'prod-001',
    nombreProducto: 'Arroz Premium',
    sucursalId:     1,
    nombreSucursal: 'Principal',
    cantidadActual: 3,
    stockMinimo:    10,
  },
  {
    productoId:     'prod-002',
    nombreProducto: 'Aceite Vegetal',
    sucursalId:     1,
    nombreSucursal: 'Principal',
    cantidadActual: 8,
    stockMinimo:    10,
  },
];

// ── Tests ──────────────────────────────────────────────────────────────────

describe('BusinessRadar', () => {
  it('renderiza el encabezado y el chip de roles', () => {
    renderWithProviders(
      <BusinessRadar metricas={metricasBase} ventasPorHora={ventasPorHora} stockRisks={[]} />
    );

    expect(screen.getByText('Radar de Negocio')).toBeInTheDocument();
    expect(screen.getByText('Supervisor / Admin')).toBeInTheDocument();
  });

  it('muestra las 4 tarjetas de métricas con sus labels', () => {
    renderWithProviders(
      <BusinessRadar metricas={metricasBase} ventasPorHora={ventasPorHora} stockRisks={[]} />
    );

    expect(screen.getByText(/Ventas del día/i)).toBeInTheDocument();
    expect(screen.getByText(/Utilidad del día/i)).toBeInTheDocument();
    expect(screen.getByText(/Margen promedio/i)).toBeInTheDocument();
    expect(screen.getByText(/Ticket promedio/i)).toBeInTheDocument();
  });

  it('muestra el valor de ventas totales formateado', () => {
    renderWithProviders(
      <BusinessRadar metricas={metricasBase} ventasPorHora={ventasPorHora} stockRisks={[]} />
    );

    // $500.000 en locale es-CO
    expect(screen.getByText(/\$500/)).toBeInTheDocument();
  });

  it('muestra el margen como porcentaje', () => {
    renderWithProviders(
      <BusinessRadar metricas={metricasBase} ventasPorHora={ventasPorHora} stockRisks={[]} />
    );

    expect(screen.getByText('30.0%')).toBeInTheDocument();
  });

  it('renderiza el gráfico de proyección intradiaria', () => {
    renderWithProviders(
      <BusinessRadar metricas={metricasBase} ventasPorHora={ventasPorHora} stockRisks={[]} />
    );

    expect(screen.getByTestId('area-chart')).toBeInTheDocument();
    expect(screen.getByText(/Ventas por hora/i)).toBeInTheDocument();
  });

  it('muestra leyendas Real y Proyectado', () => {
    renderWithProviders(
      <BusinessRadar metricas={metricasBase} ventasPorHora={ventasPorHora} stockRisks={[]} />
    );

    expect(screen.getByText('Real')).toBeInTheDocument();
    expect(screen.getByText('Proyectado')).toBeInTheDocument();
  });

  it('no muestra la sección de riesgos cuando stockRisks está vacío', () => {
    renderWithProviders(
      <BusinessRadar metricas={metricasBase} ventasPorHora={ventasPorHora} stockRisks={[]} />
    );

    expect(screen.queryByText(/riesgo de ruptura/i)).not.toBeInTheDocument();
  });

  it('muestra la sección de riesgos de stock cuando hay alertas', () => {
    renderWithProviders(
      <BusinessRadar metricas={metricasBase} ventasPorHora={ventasPorHora} stockRisks={stockRisksBase} />
    );

    expect(screen.getByText(/Productos en riesgo de ruptura \(2\)/i)).toBeInTheDocument();
    expect(screen.getByText('Arroz Premium')).toBeInTheDocument();
    expect(screen.getByText('Aceite Vegetal')).toBeInTheDocument();
  });

  it('marca como crítico el producto con menos del 50% del mínimo', () => {
    // Arroz: 3/10 = 30% → Crítico
    // Aceite: 8/10 = 80% → Bajo
    renderWithProviders(
      <BusinessRadar metricas={metricasBase} ventasPorHora={ventasPorHora} stockRisks={stockRisksBase} />
    );

    expect(screen.getByText('Crítico')).toBeInTheDocument();
    expect(screen.getByText('Bajo')).toBeInTheDocument();
  });

  it('muestra el indicador "En alza" cuando porcentajeCambio > 5', () => {
    // metricasBase.porcentajeCambio = 11.1 → trend up en Ventas
    // margenPromedio=30 > 25 → trend up en Utilidad también
    renderWithProviders(
      <BusinessRadar metricas={metricasBase} ventasPorHora={ventasPorHora} stockRisks={[]} />
    );

    const indicadores = screen.getAllByText('En alza');
    expect(indicadores.length).toBeGreaterThanOrEqual(1);
  });

  it('muestra el indicador "En baja" cuando porcentajeCambio < -5', () => {
    const metricasBaja: MetricasDelDiaDTO = { ...metricasBase, porcentajeCambio: -10 };

    renderWithProviders(
      <BusinessRadar metricas={metricasBaja} ventasPorHora={ventasPorHora} stockRisks={[]} />
    );

    expect(screen.getByText('En baja')).toBeInTheDocument();
  });

  it('muestra alerta de caída severa cuando porcentajeCambio < -20', () => {
    const metricasCaida: MetricasDelDiaDTO = { ...metricasBase, porcentajeCambio: -25 };

    renderWithProviders(
      <BusinessRadar metricas={metricasCaida} ventasPorHora={ventasPorHora} stockRisks={[]} />
    );

    expect(screen.getByText(/25%\s*vs ayer/i)).toBeInTheDocument();
  });

  it('muestra alerta "Margen bajo" cuando margenPromedio < 10', () => {
    const metricasMargenBajo: MetricasDelDiaDTO = { ...metricasBase, margenPromedio: 8 };

    renderWithProviders(
      <BusinessRadar metricas={metricasMargenBajo} ventasPorHora={ventasPorHora} stockRisks={[]} />
    );

    expect(screen.getByText('Margen bajo')).toBeInTheDocument();
  });
});
