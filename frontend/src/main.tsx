import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { registerSW } from 'virtual:pwa-register'
import '@fontsource/inter/400.css'
import '@fontsource/inter/500.css'
import '@fontsource/inter/600.css'
import '@fontsource/inter/700.css'
import App from './App.tsx'

// Registrar Service Worker con notificación de actualización disponible
registerSW({
  onNeedRefresh() {
    // La UI mostrará el banner de actualización vía UpdatePrompt (futuro componente)
    // Por ahora lo logueamos para no romper el flujo
    console.info('[PWA] Nueva versión disponible. Recarga para actualizar.')
  },
  onOfflineReady() {
    console.info('[PWA] App lista para uso offline.')
  },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
