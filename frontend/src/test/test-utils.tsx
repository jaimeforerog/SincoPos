import { render, type RenderOptions } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { SnackbarProvider } from 'notistack';

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0 },
      mutations: { retry: false },
    },
  });
}

export function renderWithProviders(
  ui: React.ReactElement,
  options?: RenderOptions & { initialEntries?: string[] }
) {
  const queryClient = createTestQueryClient();
  const { initialEntries = ['/'], ...rest } = options ?? {};

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={initialEntries}>
        <SnackbarProvider maxSnack={3}>{ui}</SnackbarProvider>
      </MemoryRouter>
    </QueryClientProvider>,
    rest
  );
}

export * from '@testing-library/react';
