import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5086',
        changeOrigin: true,
      },
      '/hubs': {
        target: 'http://localhost:5086',
        changeOrigin: true,
        ws: true,
      },
    },
  },
  build: {
    rollupOptions: {
      output: {
        manualChunks: {
          // React runtime
          'vendor-react': ['react', 'react-dom', 'react-router-dom'],
          // MUI core — sin @mui/icons-material para que Rollup tree-shake solo los íconos usados
          'vendor-mui': [
            '@mui/material',
            '@emotion/react',
            '@emotion/styled',
          ],
          // Auth libraries
          'vendor-auth': [
            '@azure/msal-browser',
            '@azure/msal-react',
            'react-oidc-context',
            'oidc-client-ts',
          ],
          // Data fetching + state
          'vendor-query': ['@tanstack/react-query', 'zustand', 'axios'],
          // Real-time
          'vendor-signalr': ['@microsoft/signalr'],
          // Exportación Excel — solo se usa en reportes
          'vendor-xlsx': ['xlsx'],
          // Charts — solo se usa en dashboard/reportes
          'vendor-charts': ['recharts'],
        },
      },
    },
    chunkSizeWarningLimit: 600,
  },
})
