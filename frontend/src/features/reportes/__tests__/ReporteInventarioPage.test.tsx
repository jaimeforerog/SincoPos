import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { ReporteInventarioPage } from '../pages/ReporteInventarioPage';

vi.mock('@/api/reportes', () => ({
  reportesApi: {
    inventarioValorizado: vi.fn(),
  },
}));

vi.mock('@/api/sucursales', () => ({
  sucursalesApi: {
    getAll: vi.fn().mockResolvedValue([
      { id: 1, nombre: 'Principal', activo: true },
    ]),
  },
}));

vi.mock('@/api/categorias', () => ({
  categoriasApi: {
    getAll: vi.fn().mockResolvedValue([
      { id: 1, nombre: 'Bebidas', activo: true },
      { id: 2, nombre: 'Snacks', activo: true },
    ]),
  },
}));

vi.mock('@/utils/exportReportes', () => ({
  exportarReporteInventario: vi.fn(),
}));

const makeReporteInventario = () => ({
  totalCosto: 5_000_000,
  totalVenta: 8_000_000,
  utilidadPotencial: 3_000_000,
  totalProductos: 2,
  totalUnidades: 50,
  productos: [
    {
      productoId: 'p1',
      codigoBarras: '7701234',
      nombre: 'Coca-Cola 350ml',
      categoria: 'Bebidas',
      sucursalId: 1,
      nombreSucursal: 'Principal',
      cantidad: 30,
      costoPromedio: 2_000,
      costoTotal: 60_000,
      precioVenta: 3_500,
      valorVenta: 105_000,
      utilidadPotencial: 45_000,
      margenPorcentaje: 42.86,
    },
    {
      productoId: 'p2',
      codigoBarras: '7705678',
      nombre: 'Papas Fritas 100g',
      categoria: 'Snacks',
      sucursalId: 1,
      nombreSucursal: 'Principal',
      cantidad: 20,
      costoPromedio: 1_500,
      costoTotal: 30_000,
      precioVenta: 2_500,
      valorVenta: 50_000,
      utilidadPotencial: 20_000,
      margenPorcentaje: 40.0,
    },
  ],
});

describe('ReporteInventarioPage', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    const { reportesApi } = await import('@/api/reportes');
    vi.mocked(reportesApi.inventarioValorizado).mockResolvedValue(makeReporteInventario());
  });

  it('muestra el encabezado "Inventario Valorizado"', async () => {
    renderWithProviders(<ReporteInventarioPage />);
    // Aparece en el header de página y en la miga de pan
    expect((await screen.findAllByText('Inventario Valorizado')).length).toBeGreaterThanOrEqual(1);
  });

  it('muestra el panel de filtros', async () => {
    renderWithProviders(<ReporteInventarioPage />);
    expect(await screen.findByText('Filtros de Búsqueda')).toBeInTheDocument();
  });

  it('muestra el switch "Solo con stock"', async () => {
    renderWithProviders(<ReporteInventarioPage />);
    expect(await screen.findByText(/solo con stock/i)).toBeInTheDocument();
  });

  it('muestra las métricas del inventario', async () => {
    renderWithProviders(<ReporteInventarioPage />);
    expect(await screen.findByText('Total Productos')).toBeInTheDocument();
    // "Costo Total" aparece en la tarjeta de métrica y en la cabecera de la tabla
    expect((await screen.findAllByText('Costo Total')).length).toBeGreaterThanOrEqual(1);
    expect((await screen.findAllByText('Valor Venta')).length).toBeGreaterThanOrEqual(1);
    // "Utilidad Potencial" aparece en métrica y en la tabla
    expect((await screen.findAllByText('Utilidad Potencial')).length).toBeGreaterThanOrEqual(1);
  });

  it('muestra las filas de productos en la tabla', async () => {
    renderWithProviders(<ReporteInventarioPage />);
    expect(await screen.findByText('Coca-Cola 350ml')).toBeInTheDocument();
    expect(await screen.findByText('Papas Fritas 100g')).toBeInTheDocument();
  });

  it('muestra encabezados de la tabla de productos', async () => {
    renderWithProviders(<ReporteInventarioPage />);
    expect(await screen.findByText('Producto')).toBeInTheDocument();
    expect(screen.getByText('Stock')).toBeInTheDocument();
    expect(screen.getByText('Precio Venta')).toBeInTheDocument();
  });

  it('muestra el botón "Generar Reporte"', async () => {
    renderWithProviders(<ReporteInventarioPage />);
    expect(await screen.findByRole('button', { name: /generar reporte/i })).toBeInTheDocument();
  });

  it('muestra el botón "Exportar Excel" habilitado cuando hay datos', async () => {
    renderWithProviders(<ReporteInventarioPage />);
    const exportBtn = await screen.findByRole('button', { name: /exportar excel/i });
    expect(exportBtn).not.toBeDisabled();
  });
});
