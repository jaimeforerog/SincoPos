import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { ReporteCajaPage } from '../pages/ReporteCajaPage';

vi.mock('@/api/reportes', () => ({
  reportesApi: {
    caja: vi.fn(),
  },
}));

vi.mock('@/api/sucursales', () => ({
  sucursalesApi: {
    getAll: vi.fn().mockResolvedValue([
      { id: 1, nombre: 'Principal', activo: true },
    ]),
  },
}));

vi.mock('@/api/cajas', () => ({
  cajasApi: {
    getAll: vi.fn().mockResolvedValue([
      { id: 1, nombre: 'Caja 01', sucursalId: 1, estado: 'Abierta' },
    ]),
  },
}));

vi.mock('@/utils/exportReportes', () => ({
  exportarReporteCaja: vi.fn(),
}));

describe('ReporteCajaPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('muestra el encabezado "Reporte de Caja"', async () => {
    renderWithProviders(<ReporteCajaPage />);
    expect(await screen.findByText('Reporte de Caja')).toBeInTheDocument();
  });

  it('muestra el panel "Seleccionar Caja"', async () => {
    renderWithProviders(<ReporteCajaPage />);
    expect(await screen.findByText('Seleccionar Caja')).toBeInTheDocument();
  });

  it('muestra el selector de Sucursal', async () => {
    renderWithProviders(<ReporteCajaPage />);
    await screen.findByText('Seleccionar Caja');
    // MUI Select muestra el label — los options no están en DOM hasta abrir el dropdown
    expect(screen.getAllByRole('combobox').length).toBeGreaterThanOrEqual(1);
  });

  it('muestra el botón "Generar Reporte" deshabilitado sin caja seleccionada', async () => {
    renderWithProviders(<ReporteCajaPage />);
    const btn = await screen.findByRole('button', { name: /generar reporte/i });
    expect(btn).toBeDisabled();
  });

  it('muestra el botón "Exportar Excel" deshabilitado inicialmente', async () => {
    renderWithProviders(<ReporteCajaPage />);
    const exportBtn = await screen.findByRole('button', { name: /exportar excel/i });
    expect(exportBtn).toBeDisabled();
  });

  it('hay dos selectores (Sucursal y Caja) en el formulario', async () => {
    renderWithProviders(<ReporteCajaPage />);
    await screen.findByText('Seleccionar Caja');
    // Sucursal selector + Caja selector
    expect(screen.getAllByRole('combobox').length).toBe(2);
  });
});
