import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'
import path from 'path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['vite.svg', 'icons/*.png'],
      manifest: {
        name: 'SincoPos',
        short_name: 'SincoPos',
        description: 'Punto de Venta SincoPos',
        theme_color: '#1565c0',
        background_color: '#ffffff',
        display: 'standalone',
        scope: '/',
        start_url: '/pos',
        icons: [
          { src: '/icons/icon-192x192.png', sizes: '192x192', type: 'image/png' },
          {
            src: '/icons/icon-512x512.png',
            sizes: '512x512',
            type: 'image/png',
            purpose: 'any maskable',
          },
        ],
      },
      workbox: {
        globPatterns: ['**/*.{js,css,html,ico,png,svg,woff2}'],
        // No pre-cachear el SW ni los chunks de workbox para evitar loops
        globIgnores: ['**/sw.js', '**/workbox-*.js'],
        runtimeCaching: [
          {
            // Productos: sirve cache inmediatamente, actualiza en background
            urlPattern: /\/api\/v1\/productos/,
            handler: 'StaleWhileRevalidate',
            options: {
              cacheName: 'api-productos',
              expiration: { maxEntries: 500, maxAgeSeconds: 3600 },
              cacheableResponse: { statuses: [200] },
            },
          },
          {
            // Precios: StaleWhileRevalidate (cambian poco)
            urlPattern: /\/api\/v1\/precios/,
            handler: 'StaleWhileRevalidate',
            options: {
              cacheName: 'api-precios',
              expiration: { maxEntries: 100, maxAgeSeconds: 1800 },
              cacheableResponse: { statuses: [200] },
            },
          },
          {
            // Inventario/stock: NetworkFirst con fallback a cache (stock aproximado offline)
            urlPattern: /\/api\/v1\/inventario/,
            handler: 'NetworkFirst',
            options: {
              cacheName: 'api-inventario',
              expiration: { maxEntries: 200, maxAgeSeconds: 300 },
              networkTimeoutSeconds: 3,
              cacheableResponse: { statuses: [200] },
            },
          },
          {
            // Cajas: NetworkFirst (necesita estado actualizado)
            urlPattern: /\/api\/v1\/cajas/,
            handler: 'NetworkFirst',
            options: {
              cacheName: 'api-cajas',
              expiration: { maxEntries: 20, maxAgeSeconds: 60 },
              networkTimeoutSeconds: 3,
              cacheableResponse: { statuses: [200] },
            },
          },
          {
            // Categorías: StaleWhileRevalidate (cambian muy poco)
            urlPattern: /\/api\/v1\/categorias/,
            handler: 'StaleWhileRevalidate',
            options: {
              cacheName: 'api-categorias',
              expiration: { maxEntries: 50, maxAgeSeconds: 7200 },
              cacheableResponse: { statuses: [200] },
            },
          },
        ],
      },
    }),
  ],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 5173,
    hmr: {
      protocol: 'ws',
      host: 'localhost',
      port: 5173,
    },
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
