import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/test-utils';
import { ErrorBoundary } from '../ErrorBoundary';

// Componente helper que lanza un error de manera controlada
function Bomb({ shouldThrow = true }: { shouldThrow?: boolean }) {
  if (shouldThrow) {
    throw new Error('boom');
  }
  return <div>hijo OK</div>;
}

// Nota: el setup global (src/test/setup.ts) ya envuelve console.error
// con un filtro y hace mockRestore() en afterEach. No agregamos otro spy
// para no chocar con esa restauración.

describe('ErrorBoundary', () => {

  it('happy path: renderiza el child cuando no hay error', () => {
    renderWithProviders(
      <ErrorBoundary>
        <div>contenido sano</div>
      </ErrorBoundary>
    );

    expect(screen.getByText('contenido sano')).toBeInTheDocument();
  });

  it('captura errores de los children y muestra el fallback por defecto', () => {
    renderWithProviders(
      <ErrorBoundary>
        <Bomb />
      </ErrorBoundary>
    );

    expect(screen.getByText('Algo salió mal')).toBeInTheDocument();
    // El mensaje del error se muestra en el fallback
    expect(screen.getByText('boom')).toBeInTheDocument();
    // Botones de acción
    expect(screen.getByRole('button', { name: /reintentar/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /recargar página/i })).toBeInTheDocument();
  });

  it('renderiza el fallback custom cuando se provee', () => {
    renderWithProviders(
      <ErrorBoundary fallback={<div>fallback personalizado</div>}>
        <Bomb />
      </ErrorBoundary>
    );

    expect(screen.getByText('fallback personalizado')).toBeInTheDocument();
    expect(screen.queryByText('Algo salió mal')).not.toBeInTheDocument();
  });

  it('cambiar la key remonta el boundary y resetea el estado de error', () => {
    const { rerender } = renderWithProviders(
      <ErrorBoundary key="v1">
        <Bomb />
      </ErrorBoundary>
    );

    // Inicialmente muestra el fallback porque el child lanzó
    expect(screen.getByText('Algo salió mal')).toBeInTheDocument();

    // Re-render con una key diferente y un child que NO lanza
    rerender(
      <ErrorBoundary key="v2">
        <Bomb shouldThrow={false} />
      </ErrorBoundary>
    );

    expect(screen.queryByText('Algo salió mal')).not.toBeInTheDocument();
    expect(screen.getByText('hijo OK')).toBeInTheDocument();
  });
});
