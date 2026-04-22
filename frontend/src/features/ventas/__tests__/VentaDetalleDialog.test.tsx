import { screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { VentaDetalleDialog } from '../components/VentaDetalleDialog';
import { renderWithProviders } from '@/test/test-utils';
import type { VentaDTO } from '@/types/api';

const baseVenta: VentaDTO = {
  id: 1,
  numeroVenta: 'V-000001',
  sucursalId: 1,
  nombreSucursal: 'Sucursal Central',
  cajaId: 1,
  nombreCaja: 'Caja 1',
  subtotal: 10000,
  descuento: 0,
  impuestos: 1900,
  total: 11900,
  estado: 'Completada',
  metodoPago: 'Efectivo',
  montoPagado: 11900,
  cambio: 0,
  fechaVenta: '2026-03-11T12:00:00Z',
  requiereFacturaElectronica: false,
  sincronizadoErp: false,
  detalles: [
    {
      id: 1,
      productoId: 'prod-uuid-1',
      nombreProducto: 'Producto Test',
      cantidad: 2,
      precioUnitario: 5000,
      costoUnitario: 2500,
      descuento: 0,
      porcentajeImpuesto: 0.19,
      montoImpuesto: 1900,
      subtotal: 10000,
      margenGanancia: 50,
      lotes: [],
    },
  ],
};

describe('VentaDetalleDialog — ERP badge', () => {
  it('muestra "Pendiente ERP" cuando sincronizadoErp=false y sin error', () => {
    renderWithProviders(
      <VentaDetalleDialog open venta={baseVenta} onClose={vi.fn()} />
    );
    expect(screen.getByText('Pendiente ERP')).toBeInTheDocument();
  });

  it('muestra "Sincronizado" cuando sincronizadoErp=true', () => {
    const venta: VentaDTO = {
      ...baseVenta,
      sincronizadoErp: true,
      erpReferencia: 'MOCK-VTA-1001',
      fechaSincronizacionErp: '2026-03-11T13:00:00Z',
    };
    renderWithProviders(
      <VentaDetalleDialog open venta={venta} onClose={vi.fn()} />
    );
    expect(screen.getByText('Sincronizado')).toBeInTheDocument();
  });

  it('muestra "Error ERP" cuando sincronizadoErp=false y hay errorSincronizacion', () => {
    const venta: VentaDTO = {
      ...baseVenta,
      sincronizadoErp: false,
      errorSincronizacion: 'Timeout conectando con el ERP',
    };
    renderWithProviders(
      <VentaDetalleDialog open venta={venta} onClose={vi.fn()} />
    );
    expect(screen.getByText('Error ERP')).toBeInTheDocument();
  });

  it('muestra datos de la venta (número, cliente, método de pago)', () => {
    const venta: VentaDTO = { ...baseVenta, nombreCliente: 'Cliente Test' };
    renderWithProviders(
      <VentaDetalleDialog open venta={venta} onClose={vi.fn()} />
    );
    expect(screen.getByText('V-000001')).toBeInTheDocument();
    expect(screen.getByText('Cliente Test')).toBeInTheDocument();
    expect(screen.getByText('Efectivo')).toBeInTheDocument();
  });

  it('no renderiza nada si venta=null', () => {
    const { container } = renderWithProviders(
      <VentaDetalleDialog open venta={null} onClose={vi.fn()} />
    );
    expect(container).toBeEmptyDOMElement();
  });
});
