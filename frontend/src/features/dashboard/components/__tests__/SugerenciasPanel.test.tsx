import { describe, it, expect } from 'vitest';
import { screen, fireEvent } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { SugerenciasPanel } from '../SugerenciasPanel';
import type { AutomaticActionDTO } from '@/types/api';

// ── Fixtures ───────────────────────────────────────────────────────────────

const makeSugerencia = (overrides: Partial<AutomaticActionDTO> = {}): AutomaticActionDTO => ({
  tipoAccion:     'ReponerStock',
  productoId:     'prod-001',
  nombreProducto: 'Arroz Premium',
  description:    'Reponer Arroz Premium',
  reason:         'Tendencia de ventas indica agotamiento en 5 días',
  dataSource:     'Historial 30 días',
  confidence:     0.85,
  canOverride:    false,
  diasRestantes:  5,
  ...overrides,
});

const sugerenciasMultiples: AutomaticActionDTO[] = [
  makeSugerencia({ productoId: 'prod-001', description: 'Reponer Arroz Premium', confidence: 0.85, diasRestantes: 5 }),
  makeSugerencia({ productoId: 'prod-002', nombreProducto: 'Aceite Vegetal', description: 'Reponer Aceite Vegetal', confidence: 0.55, diasRestantes: 7 }),
  makeSugerencia({ productoId: 'prod-003', nombreProducto: 'Sal Refinada', description: 'Reponer Sal Refinada', confidence: 0.30, diasRestantes: 2 }),
];

// ── Tests ──────────────────────────────────────────────────────────────────

describe('SugerenciasPanel', () => {
  it('retorna null cuando la lista está vacía', () => {
    const { container } = renderWithProviders(<SugerenciasPanel sugerencias={[]} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('renderiza el encabezado con el conteo de pendientes', () => {
    renderWithProviders(<SugerenciasPanel sugerencias={sugerenciasMultiples} />);

    expect(screen.getByText('Sugerencias inteligentes')).toBeInTheDocument();
    expect(screen.getByText('3 pendientes')).toBeInTheDocument();
  });

  it('muestra el chip singular "1 pendiente" con una sola sugerencia', () => {
    renderWithProviders(<SugerenciasPanel sugerencias={[makeSugerencia()]} />);
    expect(screen.getByText('1 pendiente')).toBeInTheDocument();
  });

  it('muestra la descripción, razón y fuente de datos de cada sugerencia', () => {
    renderWithProviders(<SugerenciasPanel sugerencias={[makeSugerencia()]} />);

    expect(screen.getByText('Reponer Arroz Premium')).toBeInTheDocument();
    expect(screen.getByText(/Tendencia de ventas indica agotamiento/i)).toBeInTheDocument();
    expect(screen.getByText('Historial 30 días')).toBeInTheDocument();
  });

  it('muestra el chip de días restantes', () => {
    renderWithProviders(<SugerenciasPanel sugerencias={[makeSugerencia({ diasRestantes: 5 })]} />);
    expect(screen.getByText('5 días de stock')).toBeInTheDocument();
  });

  it('muestra el porcentaje de confianza', () => {
    renderWithProviders(<SugerenciasPanel sugerencias={[makeSugerencia({ confidence: 0.85 })]} />);
    expect(screen.getByText(/Confianza 85%/i)).toBeInTheDocument();
  });

  it('descarta una sugerencia al hacer clic en el botón Descartar', () => {
    renderWithProviders(<SugerenciasPanel sugerencias={[makeSugerencia()]} />);

    expect(screen.getByText('Reponer Arroz Premium')).toBeInTheDocument();

    const btnDescartar = screen.getByRole('button', { name: /descartar/i });
    fireEvent.click(btnDescartar);

    // Después de descartar la única sugerencia, el panel desaparece
    expect(screen.queryByText('Reponer Arroz Premium')).not.toBeInTheDocument();
  });

  it('retorna null cuando todas las sugerencias son descartadas', () => {
    const { container } = renderWithProviders(
      <SugerenciasPanel sugerencias={[makeSugerencia()]} />
    );

    fireEvent.click(screen.getByRole('button', { name: /descartar/i }));
    expect(container).toBeEmptyDOMElement();
  });

  it('solo descarta la sugerencia clickeada, no las demás', () => {
    renderWithProviders(<SugerenciasPanel sugerencias={sugerenciasMultiples} />);

    // Hay 3 botones de descartar
    const botonesDescartar = screen.getAllByRole('button', { name: /descartar/i });
    expect(botonesDescartar).toHaveLength(3);

    // Descartamos la primera
    fireEvent.click(botonesDescartar[0]);

    expect(screen.queryByText('Reponer Arroz Premium')).not.toBeInTheDocument();
    expect(screen.getByText('Reponer Aceite Vegetal')).toBeInTheDocument();
    expect(screen.getByText('Reponer Sal Refinada')).toBeInTheDocument();
    expect(screen.getByText('2 pendientes')).toBeInTheDocument();
  });

  it('NO muestra el botón "Crear orden de compra" cuando canOverride = false', () => {
    renderWithProviders(<SugerenciasPanel sugerencias={[makeSugerencia({ canOverride: false })]} />);
    expect(screen.queryByText(/Crear orden de compra/i)).not.toBeInTheDocument();
  });

  it('muestra el botón "Crear orden de compra" cuando canOverride = true', () => {
    renderWithProviders(<SugerenciasPanel sugerencias={[makeSugerencia({ canOverride: true })]} />);
    expect(screen.getByText(/Crear orden de compra/i)).toBeInTheDocument();
  });

  it('usa productoId como clave de dismiss cuando está disponible', () => {
    // Dos sugerencias con el mismo description pero distinto productoId
    const s1 = makeSugerencia({ productoId: 'prod-A', description: 'Descripción igual' });
    const s2 = makeSugerencia({ productoId: 'prod-B', description: 'Descripción igual' });

    renderWithProviders(<SugerenciasPanel sugerencias={[s1, s2]} />);

    const botonesDescartar = screen.getAllByRole('button', { name: /descartar/i });
    fireEvent.click(botonesDescartar[0]);

    // Solo queda una
    expect(screen.getAllByRole('button', { name: /descartar/i })).toHaveLength(1);
  });

  it('dias <= 3 aparece y dias <= 7 aparece (ambos chips visibles)', () => {
    renderWithProviders(<SugerenciasPanel sugerencias={sugerenciasMultiples} />);

    expect(screen.getByText('5 días de stock')).toBeInTheDocument();  // prod-001 → amarillo
    expect(screen.getByText('7 días de stock')).toBeInTheDocument();  // prod-002 → amarillo
    expect(screen.getByText('2 días de stock')).toBeInTheDocument();  // prod-003 → rojo
  });
});
