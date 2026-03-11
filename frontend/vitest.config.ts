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
    env: {
      VITE_API_URL: '',
      VITE_API_VERSION: 'v1',
    },
  },
  resolve: {
    alias: { '@': path.resolve(__dirname, './src') },
  },
});
