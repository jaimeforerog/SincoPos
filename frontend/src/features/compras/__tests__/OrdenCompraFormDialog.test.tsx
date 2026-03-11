import { screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { OrdenCompraFormDialog } from '../components/OrdenCompraFormDialog';
import { renderWithProviders } from '@/test/test-utils';

describe('OrdenCompraFormDialog', () => {
  const defaultProps = {
    open: true,
    onClose: vi.fn(),
    onSuccess: vi.fn(),
  };

  it('renders correctly when open', async () => {
    renderWithProviders(<OrdenCompraFormDialog {...defaultProps} />);
    
    expect(screen.getByText(/Nueva Orden de Compra/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Sucursal \*/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Proveedor \*/i)).toBeInTheDocument();
  });

  it('displays validation errors when submitting empty form', async () => {
    renderWithProviders(<OrdenCompraFormDialog {...defaultProps} />);
    
    const submitBtn = screen.getByRole('button', { name: /Crear Orden/i });
    submitBtn.click();

    await waitFor(() => {
      expect(screen.getByText(/Seleccione una sucursal/i)).toBeInTheDocument();
      expect(screen.getByText(/Seleccione un proveedor/i)).toBeInTheDocument();
      expect(screen.getByText(/Debe agregar al menos un producto/i)).toBeInTheDocument();
    });
  });
});
