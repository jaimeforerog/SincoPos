import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { PipelineMonitorPage } from '../pages/PipelineMonitorPage';
import type { PipelineMetricsSummary, EjecucionResumenDto } from '@/api/orquestador';

// ── Mocks ──────────────────────────────────────────────────────────────────

const mockGetMetricas    = vi.fn();
const mockGetEjecuciones = vi.fn();

vi.mock('@/api/orquestador', () => ({
  orquestadorApi: {
    getMetricas:    (...a: unknown[]) => mockGetMetricas(...a),
    getEjecuciones: (...a: unknown[]) => mockGetEjecuciones(...a),
  },
}));

// recharts usa ResizeObserver que no existe en jsdom
vi.mock('recharts', () => ({
  BarChart: ({ children }: any) => <div data-testid="bar-chart">{children}</div>,
  Bar: () => null,
  Cell: () => null,
  XAxis: () => null,
  YAxis: () => null,
  CartesianGrid: () => null,
  Tooltip: () => null,
  ResponsiveContainer: ({ children }: any) => <div>{children}</div>,
}));

// ── Fixtures ───────────────────────────────────────────────────────────────

const metricasVacias: PipelineMetricsSummary = {
  totalEjecuciones: 0, exitosas: 0, fallidas: 0,
  tasaExitoPorc: 0, latenciaPromedioMs: 0,
  latenciaMaximaMs: 0, latenciaMinimaMs: 0, pasos: [],
};

const metricasConDatos: PipelineMetricsSummary = {
  totalEjecuciones: 10,
  exitosas: 9,
  fallidas: 1,
  tasaExitoPorc: 90,
  latenciaPromedioMs: 120,
  latenciaMaximaMs: 350,
  latenciaMinimaMs: 45,
  pasos: [
    { nombre: 'PreValidacion', promedioMs: 5, maxMs: 12, tasaExitoPorc: 100 },
    { nombre: 'ProcesarVenta', promedioMs: 115, maxMs: 340, tasaExitoPorc: 90 },
  ],
};

const ejecucionExitosa: EjecucionResumenDto = {
  timestamp: '2026-03-19T10:30:00Z',
  totalMs: 120,
  exitoso: true,
  error: null,
  pasos: [
    { nombre: 'PreValidacion', ms: 5, exitoso: true },
    { nombre: 'ProcesarVenta', ms: 115, exitoso: true },
  ],
};

const ejecucionFallida: EjecucionResumenDto = {
  timestamp: '2026-03-19T10:31:00Z',
  totalMs: 8,
  exitoso: false,
  error: 'Stock insuficiente para Arroz Premium.',
  pasos: [
    { nombre: 'PreValidacion', ms: 5, exitoso: true },
    { nombre: 'ProcesarVenta', ms: 3, exitoso: false, error: 'Stock insuficiente para Arroz Premium.' },
  ],
};

// ── Tests ──────────────────────────────────────────────────────────────────

describe('PipelineMonitorPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetMetricas.mockResolvedValue(metricasVacias);
    mockGetEjecuciones.mockResolvedValue([]);
  });

  it('muestra el título "Monitor de Pipeline"', () => {
    renderWithProviders(<PipelineMonitorPage />);
    expect(screen.getByText('Monitor de Pipeline')).toBeInTheDocument();
  });

  it('muestra la descripción "Capa 15"', () => {
    renderWithProviders(<PipelineMonitorPage />);
    expect(screen.getByText(/Capa 15 del Blueprint/i)).toBeInTheDocument();
  });

  it('muestra el alert de "Sin ejecuciones" cuando no hay datos', async () => {
    renderWithProviders(<PipelineMonitorPage />);
    await screen.findByText(/Sin ejecuciones registradas aún/i);
  });

  it('muestra KPIs cuando hay datos', async () => {
    mockGetMetricas.mockResolvedValue(metricasConDatos);
    mockGetEjecuciones.mockResolvedValue([ejecucionExitosa]);

    renderWithProviders(<PipelineMonitorPage />);

    await screen.findByText('10');  // total ejecuciones
    expect(screen.getAllByText('90.0%').length).toBeGreaterThan(0);
    expect(screen.getAllByText('120 ms').length).toBeGreaterThan(0);
  });

  it('muestra la tabla de ejecuciones recientes', async () => {
    mockGetMetricas.mockResolvedValue(metricasConDatos);
    mockGetEjecuciones.mockResolvedValue([ejecucionExitosa, ejecucionFallida]);

    renderWithProviders(<PipelineMonitorPage />);

    await screen.findByText(/Ejecuciones recientes/i);
  });

  it('muestra chip "Exitoso" para ejecuciones exitosas', async () => {
    mockGetMetricas.mockResolvedValue(metricasConDatos);
    mockGetEjecuciones.mockResolvedValue([ejecucionExitosa]);

    renderWithProviders(<PipelineMonitorPage />);

    await screen.findByText('Exitoso');
  });

  it('muestra chip "Fallido" para ejecuciones fallidas', async () => {
    mockGetMetricas.mockResolvedValue(metricasConDatos);
    mockGetEjecuciones.mockResolvedValue([ejecucionFallida]);

    renderWithProviders(<PipelineMonitorPage />);

    await screen.findByText('Fallido');
  });

  it('muestra el SLA ✅ OK cuando latencia promedio <= 500ms', async () => {
    mockGetMetricas.mockResolvedValue(metricasConDatos);
    mockGetEjecuciones.mockResolvedValue([]);

    renderWithProviders(<PipelineMonitorPage />);
    await screen.findByText('✅ OK');
  });

  it('muestra pasos del pipeline en la sección de tasa de éxito', async () => {
    mockGetMetricas.mockResolvedValue(metricasConDatos);
    mockGetEjecuciones.mockResolvedValue([ejecucionExitosa]);

    renderWithProviders(<PipelineMonitorPage />);

    // Los nombres de paso aparecen en la sección "Tasa de éxito por paso"
    await screen.findByText('PreValidacion');
    expect(screen.getByText('ProcesarVenta')).toBeInTheDocument();
  });

  it('llama a getMetricas y getEjecuciones al montar', async () => {
    renderWithProviders(<PipelineMonitorPage />);

    await waitFor(() => {
      expect(mockGetMetricas).toHaveBeenCalled();
      expect(mockGetEjecuciones).toHaveBeenCalledWith(30);
    });
  });

  it('muestra "Sin ejecuciones registradas" en la tabla cuando está vacía', async () => {
    mockGetMetricas.mockResolvedValue(metricasConDatos);
    mockGetEjecuciones.mockResolvedValue([]);

    renderWithProviders(<PipelineMonitorPage />);

    await screen.findByText('10');  // espera a que carguen las métricas
    expect(screen.getByText(/Sin ejecuciones registradas en esta sesión/i)).toBeInTheDocument();
  });
});
