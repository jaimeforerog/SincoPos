import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, fireEvent, waitFor } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { ReglasEticasPage } from '../pages/ReglasEticasPage';
import type { ReglaEticaDto } from '@/api/eticas';

// ── Mocks ──────────────────────────────────────────────────────────────────

const mockGetAll          = vi.fn();
const mockCreate          = vi.fn();
const mockUpdate          = vi.fn();
const mockDelete          = vi.fn();
const mockGetActivaciones = vi.fn();

vi.mock('@/api/eticas', () => ({
  eticasApi: {
    getAll:          (...a: unknown[]) => mockGetAll(...a),
    create:          (...a: unknown[]) => mockCreate(...a),
    update:          (...a: unknown[]) => mockUpdate(...a),
    delete:          (...a: unknown[]) => mockDelete(...a),
    getActivaciones: (...a: unknown[]) => mockGetActivaciones(...a),
  },
}));

// ── Fixtures ───────────────────────────────────────────────────────────────

const regla1: ReglaEticaDto = {
  id: 1,
  empresaId: null,
  nombre: 'Descuento máximo 20%',
  contexto: 'Venta',
  condicion: 'DescuentoMaximoPorcentaje',
  valorLimite: 20,
  accion: 'Bloquear',
  mensaje: 'Descuento excesivo detectado',
  activo: true,
  fechaCreacion: '2026-03-01T00:00:00Z',
};

const regla2: ReglaEticaDto = {
  id: 2,
  empresaId: null,
  nombre: 'Monto máximo por transacción',
  contexto: 'Venta',
  condicion: 'MontoMaximoTransaccion',
  valorLimite: 5000000,
  accion: 'Alertar',
  mensaje: null,
  activo: true,
  fechaCreacion: '2026-03-02T00:00:00Z',
};

// ── Tests ──────────────────────────────────────────────────────────────────

describe('ReglasEticasPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAll.mockResolvedValue([regla1, regla2]);
    mockGetActivaciones.mockResolvedValue([]);
  });

  it('muestra el título "Supervisión Ética"', async () => {
    renderWithProviders(<ReglasEticasPage />);
    expect(screen.getByText('Supervisión Ética')).toBeInTheDocument();
  });

  it('muestra las reglas retornadas por la API', async () => {
    renderWithProviders(<ReglasEticasPage />);
    await screen.findByText('Descuento máximo 20%');
    expect(screen.getAllByText('Monto máximo por transacción').length).toBeGreaterThan(0);
  });

  it('muestra el chip "Bloquear" en color error', async () => {
    renderWithProviders(<ReglasEticasPage />);
    await screen.findByText('Descuento máximo 20%');
    const chips = screen.getAllByText('Bloquear');
    expect(chips.length).toBeGreaterThan(0);
  });

  it('muestra el chip "Alertar" para accion alertar', async () => {
    renderWithProviders(<ReglasEticasPage />);
    await screen.findByText('Descuento máximo 20%');
    expect(screen.getByText('Alertar')).toBeInTheDocument();
  });

  it('muestra el mensaje de la regla', async () => {
    renderWithProviders(<ReglasEticasPage />);
    await screen.findByText('Descuento excesivo detectado');
  });

  it('muestra "No hay reglas éticas" cuando la lista está vacía', async () => {
    mockGetAll.mockResolvedValue([]);
    renderWithProviders(<ReglasEticasPage />);
    await screen.findByText(/no hay reglas éticas configuradas/i);
  });

  it('abre el diálogo "Nueva Regla" al hacer clic en el botón', async () => {
    renderWithProviders(<ReglasEticasPage />);
    await screen.findByText('Descuento máximo 20%');
    fireEvent.click(screen.getByRole('button', { name: /nueva regla/i }));
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText('Nueva Regla Ética')).toBeInTheDocument();
  });

  it('el botón "Crear" queda deshabilitado si el nombre está vacío', async () => {
    renderWithProviders(<ReglasEticasPage />);
    await screen.findByText('Descuento máximo 20%');
    fireEvent.click(screen.getByRole('button', { name: /nueva regla/i }));
    const crearBtn = screen.getByRole('button', { name: /^Crear$/i });
    // El nombre por defecto está vacío en el form reseteado
    const nombreInput = screen.getByLabelText(/nombre/i);
    fireEvent.change(nombreInput, { target: { value: '' } });
    expect(crearBtn).toBeDisabled();
  });

  it('llama a create al confirmar el formulario con datos válidos', async () => {
    mockCreate.mockResolvedValue({ ...regla1, id: 3 });
    renderWithProviders(<ReglasEticasPage />);
    await screen.findByText('Descuento máximo 20%');

    fireEvent.click(screen.getByRole('button', { name: /nueva regla/i }));
    fireEvent.change(screen.getByLabelText(/nombre/i), { target: { value: 'Nueva regla test' } });
    fireEvent.click(screen.getByRole('button', { name: /^Crear$/i }));

    await waitFor(() => {
      expect(mockCreate).toHaveBeenCalledWith(
        expect.objectContaining({ nombre: 'Nueva regla test' })
      );
    });
  });

  it('muestra la pestaña Historial de Activaciones', async () => {
    renderWithProviders(<ReglasEticasPage />);
    expect(screen.getByText(/historial de activaciones/i)).toBeInTheDocument();
  });

  it('muestra "No hay activaciones" en la pestaña de historial cuando está vacío', async () => {
    renderWithProviders(<ReglasEticasPage />);
    fireEvent.click(screen.getByText(/historial de activaciones/i));
    await screen.findByText(/no hay activaciones registradas/i);
  });
});
