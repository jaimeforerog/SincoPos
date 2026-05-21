import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig({
  plugins: [react()],
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
    // userEvent.type/click se vuelven lentos cuando la suite corre en paralelo
    // (jsdom + MUI + React Query). 15s da margen sin enmascarar bugs reales.
    testTimeout: 15000,
    env: {
      VITE_API_URL: '',
      VITE_API_VERSION: 'v1',
    },
  },
  resolve: {
    alias: { '@': path.resolve(__dirname, './src') },
  },
});
