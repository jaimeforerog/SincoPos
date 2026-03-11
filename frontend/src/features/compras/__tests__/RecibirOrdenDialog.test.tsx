import { screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { RecibirOrdenDialog } from '../components/RecibirOrdenDialog';
import { renderWithProviders } from '@/test/test-utils';

describe('RecibirOrdenDialog', () => {
  const defaultProps = {
    open: true,
    orden: {
      id: 1,
      numeroOrden: 'OC-001',
      proveedorNombre: 'Proveedor Test',
      formaPago: 'Contado',
      diasPlazo: 0,
      detalles: [
        {
          productoId: 'p1',
          nombreProducto: 'Producto Test',
          cantidadSolicitada: 10,
          cantidadRecibida: 0,
          manejaLotes: false,
        }
      ]
    } as any,
    onClose: vi.fn(),
    onSuccess: vi.fn(),
  };

  it('renders correctly when open', async () => {
    renderWithProviders(<RecibirOrdenDialog {...defaultProps} />);
    
    expect(screen.getByText(/Recibir Mercancía — OC-001/i)).toBeInTheDocument();
  });
});
