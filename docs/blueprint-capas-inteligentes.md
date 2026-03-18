# Blueprint de Capas Inteligentes — SincoPos

> **Iniciativa Cosmos · Fase Cero del nuevo Sinco ERP — Octubre 2025**
> Un marco estratégico de 15 capas que transforma procesos en inteligencia, datos en decisiones y experiencia en valor.

**Stack técnico:**
- Backend: C# · ASP.NET Core 9 · PostgreSQL 16 · Marten 8 · SignalR
- Frontend: React **19** · TypeScript · Material UI **v7** · Vite **7** · Zustand **5** · notistack **3** · recharts 3 · @mui/x-charts 8

---

## Índice

1. [Por qué el stack actual es la base ideal](#1-por-qué-el-stack-actual-es-la-base-ideal)
2. [Visión general](#2-visión-general)
3. [Leyenda de estado de implementación](#3-leyenda-de-estado-de-implementación)
4. [Sistema de diseño — tokens y tema MUI](#4-sistema-de-diseño--tokens-y-tema-mui)
5. [Etapa 1 — Intención y fluidez operativa (Capas 1–3)](#5-etapa-1--intención-y-fluidez-operativa-capas-1-3)
6. [Etapa 2 — Coordinación automática e integración (Capas 4–6)](#6-etapa-2--coordinación-automática-e-integración-capas-4-6)
7. [Etapa 3 — Inteligencia adaptativa y comunicación con propósito (Capas 7–10)](#7-etapa-3--inteligencia-adaptativa-y-comunicación-con-propósito-capas-7-10)
8. [Etapa 4 — Gobernanza, inteligencia colectiva y orquestación (Capas 11–15)](#8-etapa-4--gobernanza-inteligencia-colectiva-y-orquestación-capas-11-15)
9. [Resumen de etapas y fases](#9-resumen-de-etapas-y-fases)
10. [Checklist de implementación frontend](#10-checklist-de-implementación-frontend)

---

## 1. Por qué el stack actual es la base ideal

> Este argumento va primero porque cambia la naturaleza del proyecto: no es construir desde cero, es desbloquear valor que ya existe.

El **event sourcing con Marten** ya captura cada evento como dato inmutable. Esos eventos son la materia prima directa de las capas más avanzadas del Blueprint — sin trabajo adicional de captura.

| Evento ya capturado | Alimenta capa(s) |
|--------------------|-----------------|
| `SaleCompletedEvent` | 9 (aprendizaje), 11 (transparencia), 13 (inteligencia colectiva), 14 (radar) |
| `StockAdjustedEvent` | 4 (dependencias), 5 (anticipación), 14 (radar) |
| `SyncCompletedEvent` | 6 (integración), 11 (transparencia) |
| `TurnOpenedEvent` | 2 (timing), 3 (contexto de sesión), 8 (adaptación) |
| `DevolucionVentaEvent` | 11 (transparencia), 12 (supervisión ética) |

> **El event store no es deuda técnica — es una ventaja competitiva ya construida.**
> Cada evento que SincoPos registra hoy es inteligencia que las capas del Blueprint pueden consumir mañana.

---

## 2. Visión general

El Blueprint propone transformar SincoPos de un POS de *registro y control* a una **plataforma guiada por inteligencia contextual**. No implica reescribir el sistema, sino agregar capas de inteligencia sobre la arquitectura de event sourcing ya diseñada.

### El cambio de paradigma

| Antes | Después |
|-------|---------|
| ERP basado en control y registro | ERP que aprende, anticipa y se adapta |
| Usuarios ejecutores | Decisores asistidos por IA |
| Módulos aislados | Módulos con relaciones inteligentes |
| Notificaciones genéricas | Comunicación contextual con propósito |

### Diagnóstico del estado actual del frontend

| Archivo | Problema | Capa afectada |
|---------|----------|---------------|
| `theme/theme.ts` | Colores MUI por defecto (`#1976d2`, `#9c27b0`), sin identidad visual Sinco | Todas |
| `AppLayout.tsx` | Colores inline sin tokens centralizados | 8 |
| `POSPage.tsx` | Gradiente hardcodeado 3 veces inline, sin design tokens | 1, 7 |
| Múltiples páginas | Hero azul `#1565c0` copy-paste en todas sin componente compartido | 8 |
| `notistack` | Usado sin jerarquía de prioridad — errores, éxitos e info reciben mismo tratamiento | 7 |
| `POSPage.tsx` | Búsqueda genérica, sin UI de intención ni sugerencias anticipadas | 1, 5 |
| Sin `useUiConfig` | No existe lógica de adaptación de UI por rol | 8 |
| Sin `AuditTimeline` | La trazabilidad no tiene representación visual | 11 |

---

## 3. Leyenda de estado de implementación

Cada capa incluye una tabla de estado real en SincoPos al momento de escribir este documento.

| Símbolo | Significado |
|---------|-------------|
| ✅ Implementado | Ya existe y funciona en el código actual |
| 🔧 Parcial | La base existe, falta exposición o completar |
| 📋 Pendiente | Requiere desarrollo nuevo dentro del stack actual |
| 🔮 Futuro | Requiere infraestructura externa no disponible aún |

---

## 4. Sistema de diseño — tokens y tema MUI

> Prerequisito para todas las capas con componentes frontend. Sin tokens centralizados, cada capa agrega deuda de estilo.

### `frontend/src/theme/tokens.ts` — archivo nuevo

```typescript
// Design tokens centralizados — única fuente de verdad de colores y espaciado.
// Todos los componentes importan de aquí, nunca hardcodean hex.

export const sincoColors = {
  brand: {
    900: '#0D2F5E',
    800: '#1565c0',  // primario principal
    700: '#1976d2',
    600: '#1E88E5',
    500: '#2196F3',
    100: '#BBDEFB',
    50:  '#E3F2FD',
  },
  success: { main: '#2e7d32', light: '#4caf50', bg: '#F1F8E9' },
  warning: { main: '#E65100', light: '#ff9800', bg: '#FFF3E0' },
  error:   { main: '#c62828', light: '#ef5350', bg: '#FFEBEE' },
  info:    { main: '#0277BD', light: '#03a9f4', bg: '#E1F5FE' },
  surface: {
    page:    '#F5F7FA',
    paper:   '#FFFFFF',
    subtle:  '#F0F4F8',
    overlay: 'rgba(13, 47, 94, 0.06)',
  },
  text: {
    primary:   '#1A2332',
    secondary: '#4A5568',
    disabled:  '#A0AEC0',
    inverse:   '#FFFFFF',
  },
  gradients: {
    heroBlue:    'linear-gradient(135deg, #1565c0 0%, #0d47a1 50%, #01579b 100%)',
    heroSubtle:  'linear-gradient(135deg, #1976d2 0%, #1565c0 100%)',
    heroSuccess: 'linear-gradient(135deg, #2e7d32 0%, #1b5e20 100%)',
    heroWarning: 'linear-gradient(135deg, #E65100 0%, #BF360C 100%)',
  },
} as const;

export const sincoSpacing = {
  heroRadius:   '16px',
  cardRadius:   '12px',
  chipRadius:   '8px',
  drawerWidth:  260,
  appBarHeight: 64,
} as const;

// Colores por capa del Blueprint — para badges visuales en documentación interna
export const sincoBlueprintLayer: Record<number, string> = {
  1:  '#1565c0', 2:  '#0277BD', 3:  '#00838F',
  4:  '#2E7D32', 5:  '#558B2F', 6:  '#6A1B9A',
  7:  '#AD1457', 8:  '#4527A0', 9:  '#37474F',
  10: '#4E342E', 11: '#E65100', 12: '#B71C1C',
  13: '#01579B', 14: '#004D40', 15: '#1A237E',
};
```

### `frontend/src/theme/theme.ts` — reemplazar completamente

```typescript
import { createTheme } from '@mui/material/styles';
import { sincoColors, sincoSpacing } from './tokens';

export const theme = createTheme({
  palette: {
    primary:    { main: sincoColors.brand[800], light: sincoColors.brand[600], dark: sincoColors.brand[900], contrastText: sincoColors.text.inverse },
    secondary:  { main: sincoColors.brand[900], light: sincoColors.brand[700], dark: '#061829', contrastText: sincoColors.text.inverse },
    error:      { main: sincoColors.error.main,   light: sincoColors.error.light },
    warning:    { main: sincoColors.warning.main, light: sincoColors.warning.light },
    info:       { main: sincoColors.info.main,    light: sincoColors.info.light },
    success:    { main: sincoColors.success.main, light: sincoColors.success.light },
    background: { default: sincoColors.surface.page, paper: sincoColors.surface.paper },
    text:       { primary: sincoColors.text.primary, secondary: sincoColors.text.secondary, disabled: sincoColors.text.disabled },
  },
  typography: {
    fontFamily: ['Inter', '-apple-system', 'BlinkMacSystemFont', '"Segoe UI"', 'Roboto', 'sans-serif'].join(','),
    h5: { fontSize: '1.1rem', fontWeight: 600 },
    h6: { fontSize: '1rem',   fontWeight: 600 },
    body1: { fontSize: '0.9375rem', lineHeight: 1.6 },
    body2: { fontSize: '0.875rem',  lineHeight: 1.5 },
    caption: { fontSize: '0.75rem', letterSpacing: '0.02em' },
  },
  shape: { borderRadius: 10 },
  components: {
    MuiButton: {
      styleOverrides: {
        root: { textTransform: 'none', fontWeight: 600, borderRadius: sincoSpacing.chipRadius },
        containedPrimary: {
          background: sincoColors.gradients.heroSubtle,
          '&:hover': { background: sincoColors.gradients.heroBlue },
        },
      },
    },
    MuiCard:   { styleOverrides: { root: { borderRadius: sincoSpacing.cardRadius, boxShadow: '0 1px 3px rgba(0,0,0,0.08)', border: '1px solid rgba(0,0,0,0.06)' } } },
    MuiPaper:  { styleOverrides: { root: { borderRadius: sincoSpacing.cardRadius, boxShadow: '0 1px 3px rgba(0,0,0,0.08)' } } },
    MuiAppBar: { styleOverrides: { root: { background: sincoColors.gradients.heroBlue, boxShadow: '0 2px 8px rgba(13,47,94,0.3)' } } },
    MuiDrawer: { styleOverrides: { paper: { background: sincoColors.brand[900], color: sincoColors.text.inverse, borderRight: 'none' } } },
    MuiChip:   { styleOverrides: { root: { borderRadius: sincoSpacing.chipRadius, fontWeight: 500 } } },
    MuiAlert:  { styleOverrides: { root: { borderRadius: sincoSpacing.chipRadius } } },
  },
});
```

### Componente compartido: `frontend/src/components/common/HeroBanner.tsx`

Elimina el gradiente hardcodeado copy-paste en todas las páginas.

```tsx
import { Box, Typography } from '@mui/material';
import { sincoColors } from '@/theme/tokens';

interface HeroBannerProps {
  title:     string;
  subtitle?: string;
  variant?:  'blue' | 'success' | 'warning';
  actions?:  React.ReactNode;
  info?:     React.ReactNode;
}

export function HeroBanner({ title, subtitle, variant = 'blue', actions, info }: HeroBannerProps) {
  const gradient = {
    blue:    sincoColors.gradients.heroBlue,
    success: sincoColors.gradients.heroSuccess,
    warning: sincoColors.gradients.heroWarning,
  }[variant];

  return (
    <Box sx={{
      background: gradient, borderRadius: '16px', p: 2, mb: 3,
      position: 'relative', overflow: 'hidden',
      '&::before': { content: '""', position: 'absolute', top: -40, right: -40, width: 140, height: 140, borderRadius: '50%', background: 'rgba(255,255,255,0.06)' },
      '&::after':  { content: '""', position: 'absolute', bottom: -30, left: 60, width: 100, height: 100, borderRadius: '50%', background: 'rgba(255,255,255,0.04)' },
    }}>
      <Box sx={{ display: 'flex', gap: 3, alignItems: 'center', flexWrap: 'wrap', justifyContent: 'space-between', position: 'relative', zIndex: 1 }}>
        <Box>
          <Typography variant="h6" fontWeight={700} sx={{ color: '#fff', lineHeight: 1.2 }}>{title}</Typography>
          {subtitle && <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.7)' }}>{subtitle}</Typography>}
        </Box>
        {info    && <Box sx={{ display: 'flex', gap: 3, alignItems: 'center', flexWrap: 'wrap' }}>{info}</Box>}
        {actions && <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>{actions}</Box>}
      </Box>
    </Box>
  );
}
```

**Dependencia a instalar:**
```bash
npm install @fontsource/inter

# En frontend/src/main.tsx agregar:
import '@fontsource/inter/400.css';
import '@fontsource/inter/500.css';
import '@fontsource/inter/600.css';
import '@fontsource/inter/700.css';
```

---

## 5. Etapa 1 — Intención y fluidez operativa (Capas 1–3)

**Alineación con fases SincoPos:** Fase 1–2
**Objetivo:** Reducir fricción operativa sin necesidad de IA — solo buen diseño de flujo.

---

### Capa 1 · Entrada multimodal — menos pasos, más propósito

> El cajero usa el canal más natural para el momento: escanea, escribe, apunta la cámara o habla. El sistema resuelve el resto.

El `ProductSearch` actual ya cubre texto libre y código de barras. La evolución es normalizar todos los modos de entrada bajo un mismo comando de intención.

**Backend — `SaleIntentCommand`:**

```csharp
public record SaleIntentCommand(
    Guid   RegisterId,
    string RawInput,      // "2 coca cola", código de barras, imagen en base64, o transcripción de voz
    InputMode Mode,       // Barcode | FreeText | Camera | Voice
    Guid?  CustomerId
);

public class SaleIntentHandler
{
    public async Task<SaleLineItem> Handle(SaleIntentCommand cmd)
    {
        var product = cmd.Mode switch
        {
            InputMode.Barcode  => await _productResolver.ResolveByBarcode(cmd.RawInput),
            InputMode.FreeText => await _productResolver.ResolveByText(cmd.RawInput),
            InputMode.Camera   => await _productResolver.ResolveFromImage(cmd.RawInput),  // OCR / barcode desde imagen
            InputMode.Voice    => await _productResolver.ResolveByText(
                                      await _speechService.TranscribeAsync(cmd.RawInput)),
            _ => throw new ArgumentOutOfRangeException()
        };
        var price = await _pricingService.GetPrice(product.Id, cmd.RegisterId);
        return new SaleLineItem(product, price, quantity: cmd.ParsedQuantity);
    }
}

// Resolver de imagen — dos estrategias según el contenido capturado:
public class ProductImageResolver
{
    public async Task<Product> ResolveFromImage(string imageBase64)
    {
        // 1. Intentar decodificar como código de barras/QR (offline, @zxing en frontend)
        //    Si el frontend ya extrajo el código, llega como InputMode.Barcode normal.
        //
        // 2. Si es texto (nombre de producto en factura, etiqueta manual):
        //    Enviar imagen al servicio OCR y resolver el texto resultante.
        var ocrText = await _ocrService.ExtractTextAsync(imageBase64);
        return await ResolveByText(ocrText);
    }
}
```

#### OCR — análisis de viabilidad y stack

**Tres casos de uso concretos para SincoPos:**

| Caso de uso | Descripción | Modo |
|------------|-------------|------|
| Barcode por cámara | El cajero apunta el celular a un producto sin escáner físico disponible | Decodificación de imagen — sin servidor |
| Nombre en etiqueta | Producto con etiqueta artesanal o código ilegible — captura el texto de la etiqueta | OCR de texto — cliente o servidor |
| Factura de proveedor | Foto de una factura para registrar una compra sin digitación manual | OCR estructurado — servidor |

**Stack recomendado por caso:**

| Caso | Librería | Procesamiento | Offline | Precisión |
|------|----------|--------------|---------|-----------|
| Barcode/QR por cámara | `@zxing/browser` | Cliente (WASM) | ✅ Sí | Alta para códigos estándar |
| Texto en etiqueta simple | `Tesseract.js` | Cliente (WASM) | ✅ Sí | Media (~80%) — depende del fondo |
| Factura / texto complejo | `Azure Computer Vision` | Servidor (API REST) | ❌ No | Alta (>95%) |

> **Decisión para SincoPos:**
> - **Fase inmediata:** `@zxing/browser` para barcode/QR por cámara — sin servidor, PWA compatible, sin costo por llamada.
> - **Fase futura:** `Azure Computer Vision` para lectura de facturas — requiere conexión, costo por llamada (~$1 USD / 1000 imágenes).
> - `Tesseract.js` se descarta para producción: 10 MB de WASM, latencia alta (2–5s), impreciso con fondos complejos.

**Frontend — `frontend/src/features/pos/components/CameraInput.tsx`** (archivo nuevo):

```tsx
import { useEffect, useRef, useState } from 'react';
import { Box, IconButton, Dialog, DialogContent, DialogTitle, Typography, CircularProgress } from '@mui/material';
import CameraAltIcon from '@mui/icons-material/CameraAlt';
import CloseIcon     from '@mui/icons-material/Close';
import { BrowserMultiFormatReader } from '@zxing/browser'; // npm install @zxing/browser
import { sincoColors } from '@/theme/tokens';

interface CameraInputProps {
  onDetected: (rawValue: string, mode: 'barcode' | 'text') => void;
}

export function CameraInput({ onDetected }: CameraInputProps) {
  const [open,       setOpen]       = useState(false);
  const [scanning,   setScanning]   = useState(false);
  const [error,      setError]      = useState<string | null>(null);
  const videoRef   = useRef<HTMLVideoElement>(null);
  const readerRef  = useRef<BrowserMultiFormatReader | null>(null);

  const startScan = async () => {
    setOpen(true);
    setScanning(true);
    setError(null);

    try {
      readerRef.current = new BrowserMultiFormatReader();
      const devices = await BrowserMultiFormatReader.listVideoInputDevices();
      if (devices.length === 0) { setError('No se encontró cámara disponible'); return; }

      // Preferir cámara trasera en móviles
      const deviceId = devices.find(d => d.label.toLowerCase().includes('back'))?.deviceId
                    ?? devices[0].deviceId;

      await readerRef.current.decodeFromVideoDevice(
        deviceId,
        videoRef.current!,
        (result, err) => {
          if (result) {
            onDetected(result.getText(), 'barcode');
            stopScan();
          }
          // err es normal mientras no hay código en cuadro — ignorar
        }
      );
    } catch (e) {
      setError('No se pudo acceder a la cámara. Verifica los permisos del navegador.');
      setScanning(false);
    }
  };

  const stopScan = () => {
    readerRef.current?.reset();
    setOpen(false);
    setScanning(false);
  };

  useEffect(() => () => { readerRef.current?.reset(); }, []);

  return (
    <>
      <IconButton
        onClick={startScan}
        size="small"
        sx={{
          color: sincoColors.brand[700],
          '&:hover': { bgcolor: sincoColors.brand[50] },
        }}
        title="Escanear con cámara"
      >
        <CameraAltIcon fontSize="small" />
      </IconButton>

      <Dialog open={open} onClose={stopScan} maxWidth="sm" fullWidth>
        <DialogTitle sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <Typography fontWeight={600}>Escanear con cámara</Typography>
          <IconButton onClick={stopScan} size="small"><CloseIcon /></IconButton>
        </DialogTitle>
        <DialogContent>
          {error ? (
            <Typography color="error" variant="body2">{error}</Typography>
          ) : (
            <Box sx={{ position: 'relative', borderRadius: '12px', overflow: 'hidden', bgcolor: '#000' }}>
              <video ref={videoRef} style={{ width: '100%', display: 'block' }} />
              {/* Guía visual de encuadre */}
              <Box sx={{
                position: 'absolute', top: '50%', left: '50%',
                transform: 'translate(-50%, -50%)',
                width: 200, height: 120,
                border: `3px solid ${sincoColors.brand[400]}`,
                borderRadius: '8px',
                boxShadow: '0 0 0 1000px rgba(0,0,0,0.4)',
              }} />
              {scanning && (
                <Box sx={{ position: 'absolute', bottom: 16, left: '50%', transform: 'translateX(-50%)', display: 'flex', alignItems: 'center', gap: 1 }}>
                  <CircularProgress size={16} sx={{ color: 'white' }} />
                  <Typography variant="caption" color="white">Buscando código…</Typography>
                </Box>
              )}
            </Box>
          )}
          <Typography variant="caption" color="text.secondary" display="block" mt={1} textAlign="center">
            Apunta la cámara al código de barras o QR del producto
          </Typography>
        </DialogContent>
      </Dialog>
    </>
  );
}
```

**Integración en `IntentSearch.tsx`** — agregar el botón de cámara junto al ícono de búsqueda:

```tsx
import { CameraInput } from './CameraInput';

// En el Paper de búsqueda, después de SearchIcon:
<CameraInput
  onDetected={(value, mode) => {
    if (mode === 'barcode') handleSearch(value); // resolver directamente
  }}
/>
```

**Dependencia a instalar:**
```bash
npm install @zxing/browser
```

**Frontend — `frontend/src/features/pos/components/IntentSearch.tsx`** (archivo nuevo):

```tsx
import { useState, useCallback } from 'react';
import { Box, InputBase, Paper, List, ListItem, ListItemText, ListItemAvatar, Avatar, Typography, Chip, CircularProgress } from '@mui/material';
import SearchIcon  from '@mui/icons-material/Search';
import FlashOnIcon from '@mui/icons-material/FlashOn';
import { sincoColors } from '@/theme/tokens';
import { useAnticipatedProducts } from '../hooks/useAnticipatedProducts';
import type { ProductoDTO } from '@/types/api';

interface IntentSearchProps {
  onSelectProduct:   (product: ProductoDTO) => void;
  cashierId:         string;
  anticipatedLimit?: number;
}

export function IntentSearch({ onSelectProduct, cashierId, anticipatedLimit = 8 }: IntentSearchProps) {
  const [query,      setQuery]      = useState('');
  const [isSearching, setSearching] = useState(false);
  const [results,    setResults]    = useState<ProductoDTO[]>([]);

  // Capa 5 — productos anticipados (cuando el campo está vacío)
  const { data: anticipated = [] } = useAnticipatedProducts(cashierId);

  const handleSearch = useCallback(async (value: string) => {
    setQuery(value);
    if (value.length < 2) { setResults([]); return; }
    setSearching(true);
    const found = await productosApi.search(value); // extiende el endpoint existente
    setResults(found);
    setSearching(false);
  }, []);

  const showAnticipated = query.length === 0 && anticipated.length > 0;

  return (
    <Box>
      <Paper sx={{
        display: 'flex', alignItems: 'center', px: 2, py: 1, mb: 2,
        border: `2px solid ${sincoColors.brand[600]}`,
        borderRadius: '12px',
        boxShadow: `0 0 0 4px ${sincoColors.brand[50]}`,
      }}>
        <SearchIcon sx={{ color: sincoColors.brand[700], mr: 1.5 }} />
        <InputBase
          fullWidth autoFocus
          placeholder="Nombre, código o cantidad — ej: '2 coca cola'"
          value={query}
          onChange={(e) => handleSearch(e.target.value)}
          sx={{ fontSize: '1rem', fontWeight: 500 }}
        />
        {isSearching && <CircularProgress size={18} sx={{ ml: 1 }} />}
      </Paper>

      {/* Capa 5 — Chips de productos anticipados */}
      {showAnticipated && (
        <Box sx={{ mb: 2 }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
            <FlashOnIcon sx={{ fontSize: 16, color: sincoColors.warning.main }} />
            <Typography variant="caption" color="text.secondary" fontWeight={600}>
              Frecuentes — basado en tu historial
            </Typography>
          </Box>
          <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
            {anticipated.slice(0, anticipatedLimit).map((p) => (
              <Chip
                key={p.id} label={p.nombre}
                onClick={() => onSelectProduct(p)}
                sx={{
                  cursor: 'pointer',
                  bgcolor: sincoColors.brand[50], color: sincoColors.brand[800],
                  fontWeight: 500, border: `1px solid ${sincoColors.brand[100]}`,
                  '&:hover': { bgcolor: sincoColors.brand[100] },
                }}
              />
            ))}
          </Box>
        </Box>
      )}

      {results.length > 0 && (
        <List dense disablePadding>
          {results.map((product) => (
            <ListItem
              key={product.id}
              onClick={() => { onSelectProduct(product); setQuery(''); setResults([]); }}
              sx={{ borderRadius: '8px', cursor: 'pointer', mb: 0.5, '&:hover': { bgcolor: sincoColors.surface.overlay } }}
            >
              <ListItemAvatar>
                <Avatar sx={{ bgcolor: sincoColors.brand[50], color: sincoColors.brand[800], fontSize: '0.75rem' }}>
                  {product.codigoBarras?.slice(0, 3) ?? '---'}
                </Avatar>
              </ListItemAvatar>
              <ListItemText
                primary={<Typography variant="body2" fontWeight={600}>{product.nombre}</Typography>}
                secondary={product.categoria}
              />
              <Typography variant="body2" fontWeight={700} color="primary">
                ${product.precioVenta?.toLocaleString('es-CO')}
              </Typography>
            </ListItem>
          ))}
        </List>
      )}
    </Box>
  );
}
```

**Cambio en `POSPage.tsx`:**
```tsx
// ANTES — hero gradiente inline de 12 líneas + ProductSearch genérico
// DESPUÉS:
<HeroBanner title="Punto de Venta" subtitle="Sesión activa" info={<SessionInfo />} actions={<CambiarCajaBtn />} />

// En el panel izquierdo:
<IntentSearch
  onSelectProduct={handleSelectProduct}
  cashierId={user?.id ?? ''}
  anticipatedLimit={uiConfig.quickProductsLimit}
/>
```

**Métrica de éxito:** Interacciones para completar una venta ≥5 → ≤2. Tiempo de registro de línea < 3s.

**Modo degradado:** Si el resolver de intención falla, cae al buscador manual actual. Si la anticipación no tiene datos (< 20 ventas), muestra los más vendidos de la tienda.

**En SincoPos hoy:**

| Componente | Estado |
|-----------|--------|
| Búsqueda por texto libre (`ProductSearch`) | ✅ Implementado |
| Lectura de código de barras (escáner físico) | ✅ Implementado |
| `HeroBanner` reutilizable | ✅ Implementado |
| `IntentSearch` con `InputMode` unificado | ✅ Implementado |
| `CameraInput` — barcode/QR por cámara (`@zxing/browser`) | ✅ Implementado |
| OCR de texto en etiquetas (`Tesseract.js`) | 🔮 Futuro (baja prioridad — precisión insuficiente offline) |
| OCR de facturas (`Azure Computer Vision`) | 🔮 Futuro (requiere conexión + presupuesto API) |
| Entrada por voz | 🔮 Futuro |

---

### Capa 2 · Tiempo como valor

> Sincronizar acciones con el momento de máximo valor: proactivo, sincrónico, reactivo controlado.

```csharp
public class SyncOrchestrator
{
    // 🟩 Proactivo: antes de apertura de turno
    public async Task PreloadTurnContext(Guid registerId)
    {
        await _cache.WarmProducts(registerId);
        await _cache.WarmTopProducts(registerId, n: 50);
    }

    // 🟧 Reactivo controlado: retener sincronización ERP hasta confirmar fiscal
    public async Task OnSaleCompleted(SaleCompletedEvent evt)
    {
        await _auditLog.Record(evt);
        await _stockAdjuster.Adjust(evt);
        _erpQueue.Enqueue(evt, condition: HasFiscalNumber);
    }
}
```

**Tres comportamientos temporales:**
- 🟩 **Proactivo** — el sistema actúa antes de que el usuario lo pida
- 🟨 **Sincrónico** — ejecuta en tiempo real con el flujo del usuario
- 🟧 **Reactivo controlado** — espera una condición externa antes de actuar

**Métrica de éxito:** Tiempo de primera interacción después de apertura de turno < 1s.

**Modo degradado:** Si la precarga falla (sin conexión), opera con IndexedDB local (PWA offline ya implementada). El Outbox persiste y se drena al reconectar.

**En SincoPos hoy:**

| Componente | Estado |
|-----------|--------|
| ERP Outbox con condición fiscal (`ErpSyncBackgroundService`) | ✅ Implementado |
| Auditoría e inventario inmediatos post-venta | ✅ Implementado |
| PWA offline con IndexedDB | ✅ Implementado |
| Precarga proactiva al abrir turno (`useTurnPreload`) | ✅ Implementado |

---

### Capa 3 · Repetición cero

> El sistema recuerda todo — ningún dato se pide dos veces.

```csharp
public class RegisterSessionContext
{
    public async Task OpenTurn(Guid cashierId, Guid registerId)
    {
        Session = new TurnSession
        {
            Cashier         = await _users.Get(cashierId),
            AssignedStock   = await _stock.GetQuota(registerId),
            RecentCustomers = await _customers.GetRecent(cashierId, limit: 20),
            PendingOrders   = await _orders.GetPending(registerId)
        };
    }
}
```

**Repeticiones a eliminar:**
- Re-autenticación durante el turno activo
- Ingresar datos del cliente si ya existe en historial
- Confirmar el registro/caja en cada transacción

**Métrica de éxito:** 0 interrupciones por datos ya conocidos durante un turno activo.

**Modo degradado:** Si los datos no se pueden precargar, se piden solo una vez y se cachean para el resto del turno.

**En SincoPos hoy:**

| Componente | Estado |
|-----------|--------|
| Sesión de caja/sucursal en `localStorage` (`posSessionCache`) | ✅ Implementado |
| Auto-selección de caja única al abrir POS | ✅ Implementado |
| Precarga de clientes recientes en apertura | 📋 Pendiente |
| Precarga de órdenes pendientes en apertura | 📋 Pendiente |

---

## 6. Etapa 2 — Coordinación automática e integración (Capas 4–6)

**Alineación con fases SincoPos:** Fase 3–4
**Objetivo:** Conectar módulos automáticamente y extender SincoPos al ecosistema ERP.

---

### Capa 4 · Dependencias inteligentes

> Un evento en ventas activa automáticamente inventario, auditoría y ERP — sin intervención manual.

```csharp
public class SaleCompletedEventHandler : IEventHandler<SaleCompletedEvent>
{
    public async Task Handle(SaleCompletedEvent evt)
    {
        await Task.WhenAll(
            _inventoryService.DecrementStock(evt.Items),
            _auditProjection.Apply(evt),
            _customerHistory.Record(evt.CustomerId, evt),
            _erpPublisher.PublishAsync(new SaleDocument(evt))
        );
        if (evt.RequiresFiscalDocument)
            await _fiscalQueue.Enqueue(evt);
    }
}
```

**Clasificación de dependencias:**
- **Notificable** — otro módulo necesita saber que ocurrió algo
- **Accionable** — otro módulo debe ejecutar algo automáticamente
- **Predictiva** — otro módulo puede anticipar su próximo paso

**Métrica de éxito:** 0 acciones manuales de coordinación entre módulos tras registrar una venta.

**Modo degradado:** Si el handler de ERP falla, el Outbox garantiza reintento. Inventario y auditoría son síncronos — si fallan, la venta no se confirma.

**En SincoPos hoy:**

| Componente | Estado |
|-----------|--------|
| `VentaService` dispara inventario + auditoría + ERP outbox | ✅ Implementado |
| Facturación electrónica condicional post-venta | ✅ Implementado |
| Historial de cliente por venta | 🔧 Parcial (guardado en venta, falta proyección dedicada) |

---

### Capa 5 · Anticipación funcional

> El sistema aprende las rutinas del usuario — qué vende, cuándo y con quién — para actuar antes de que lo pida.

Esta capa se ocupa de **comportamiento del operador**. Se diferencia de la Capa 14 en que su unidad de análisis es el usuario, no la tienda.

| Capa | Unidad de análisis | Salida | Consumidor |
|------|--------------------|--------|------------|
| Capa 5 | Cajero / cliente | Sugerencias de productos, precarga | POS — durante la venta |
| Capa 14 | Tienda / empresa | Forecasting, anomalías, ruptura de stock | Dashboard — gerente |

**Backend:**

```csharp
public class UserBehaviorProjection : SingleStreamProjection<UserBehavior>
{
    public void Apply(SaleCompletedEvent evt, UserBehavior behavior)
    {
        behavior.RecordSale(evt.Items, evt.Hour, evt.DayOfWeek);
        behavior.UpdateFrequentProducts();
        behavior.UpdateFrequentCustomers();
    }
}
```

**Frontend — `frontend/src/features/pos/hooks/useAnticipatedProducts.ts`** (archivo nuevo):

```typescript
import { useQuery } from '@tanstack/react-query';
import { productosApi } from '@/api/productos';

export function useAnticipatedProducts(cashierId: string) {
  return useQuery({
    queryKey: ['anticipated-products', cashierId],
    queryFn:  () => productosApi.getAnticipated(cashierId), // endpoint nuevo en backend
    staleTime: 5 * 60 * 1000, // 5 min — no re-fetchar en cada keystroke
    enabled: !!cashierId,
    // Fallback: si no hay datos, devolver [] — IntentSearch muestra top productos
    placeholderData: [],
  });
}
```

**Métrica de éxito:** Primer producto sugerido coincide con el primero que agrega el cajero en ≥ 60% de ventas después de 2 semanas.

**Modo degradado:** Sin suficientes datos (< 20 ventas), mostrar top productos de la tienda como sugerencia genérica.

**En SincoPos hoy:**

| Componente | Estado |
|-----------|--------|
| `SaleCompletedEvent` capturado en Marten | ✅ Implementado |
| `UserBehaviorProjection` | 📋 Pendiente |
| `useAnticipatedProducts` hook | 📋 Pendiente |
| Chips de productos frecuentes en POS | 📋 Pendiente |

---

### Capa 6 · Integración extendida

> SincoPos actúa como orquestador — el usuario siente que todo ocurre dentro del sistema.

```csharp
public class ErpIntegrationService
{
    private readonly IErpClient  _erpClient; // abstracción — REST hoy, gRPC en objetivo
    private readonly IMessageBus _bus;

    public async Task<decimal> GetCurrentPrice(string productCode, Guid storeId)
        => await _erpClient.GetPriceAsync(new PriceRequest(productCode, storeId));

    public async Task TransmitSaleDocument(SaleCompletedEvent evt)
        => await _bus.Publish(new SaleDocumentMessage(evt));
}
```

**Mapa de integraciones — estado actual vs. objetivo:**

| Sistema externo | Canal actual | Canal objetivo | Tipo | Momento |
|----------------|-------------|----------------|------|---------|
| ERP Sinco | REST + Outbox | RabbitMQ | Asíncrono | Transmisión de documentos post-venta |
| ERP Sinco | REST | gRPC | Síncrono | Consulta de precios en tiempo real |
| Facturación DIAN | REST/SOAP | REST/SOAP | Asíncrono | Post-venta |
| Terminales offline | IndexedDB + sync manual | SignalR bidireccional | Sync | Apertura/cierre de turno |

> **Nota:** gRPC requiere que ERP Sinco exponga un endpoint gRPC — infraestructura futura. La abstracción `IErpClient` permite migrar sin cambiar lógica de negocio.

**Métrica de éxito:** 0 documentos perdidos. Latencia de consulta de precio < 200ms.

**Modo degradado:** Si el ERP no responde, usar precio local de sucursal. Outbox encola y reintenta.

**En SincoPos hoy:**

| Componente | Estado |
|-----------|--------|
| ERP Outbox para ventas y compras | ✅ Implementado |
| Facturación electrónica DIAN | ✅ Implementado |
| Precios por sucursal con fallback al precio base | ✅ Implementado |
| PWA offline con sync al reconectar | ✅ Implementado |
| gRPC para consulta en tiempo real | 🔮 Futuro |
| RabbitMQ como bus de mensajes | 🔮 Futuro |

---

## 7. Etapa 3 — Inteligencia adaptativa y comunicación con propósito (Capas 7–10)

**Alineación con fases SincoPos:** Fase 5–6
**Objetivo:** El sistema aprende, notifica con propósito y se adapta según el rol y contexto.

---

### Capa 7 · Comunicación contextual

> Solo notificar cuando el mensaje tiene valor real — canal correcto, tono correcto, momento correcto.

**Backend:**

```csharp
public class ContextualNotificationService
{
    public async Task NotifyCashier(Guid registerId, PosNotification notif)
    {
        if (notif.Priority == NotificationPriority.Informational) return; // nunca ruido

        await _hub.Clients.Group($"register-{registerId}").SendAsync("Notification", new
        {
            notif.Message,
            notif.Action,    // qué puede hacer el cajero
            notif.Context,   // por qué se notifica
            notif.ExpiresAt
        });
    }
}
// ✓ "Stock de Coca-Cola: 3 unidades" → acción: solicitar reabastecimiento
// ✓ "12 transacciones pendientes de sync" → acción: conectar a red
// ✗ "Transacción guardada exitosamente" → no agrega valor, no se envía
```

**Frontend — `frontend/src/hooks/useContextualNotification.ts`** (archivo nuevo):

```typescript
import { useSnackbar } from 'notistack';

type NotificationLevel = 'operational' | 'informational' | 'system' | 'anticipation';

interface ContextualNotif {
  message:    string;
  action?:    string;
  context?:   string;   // Capa 10 — por qué se notifica
  level:      NotificationLevel;
  expiresIn?: number;
}

export function useContextualNotification() {
  const { enqueueSnackbar } = useSnackbar();

  const notify = ({ message, context, level, expiresIn = 4000 }: ContextualNotif) => {
    if (level === 'informational') return; // Capa 7 — silenciar ruido

    const fullMessage = context ? `${message} — ${context}` : message;

    enqueueSnackbar(fullMessage, {
      variant: level === 'system' ? 'error' : level === 'operational' ? 'warning' : 'info',
      autoHideDuration: expiresIn,
      anchorOrigin: level === 'system'
        ? { vertical: 'top',    horizontal: 'center' }   // errores: arriba centro
        : { vertical: 'bottom', horizontal: 'right' },   // operacionales: abajo derecha
    });
  };

  return {
    notify,
    operacional: (message: string, context?: string) =>
      notify({ message, context, level: 'operational', expiresIn: 5000 }),
    sistema: (message: string) =>
      notify({ message, level: 'system', expiresIn: 8000 }),
    anticipacion: (message: string, context: string) =>
      enqueueSnackbar(`${message} — ${context}`, {
        variant: 'info', autoHideDuration: 6000,
        anchorOrigin: { vertical: 'bottom', horizontal: 'left' }, // posición diferente
      }),
  };
}
```

**Migración en `POSPage.tsx`:**
```tsx
// ANTES — todo con enqueueSnackbar sin jerarquía:
enqueueSnackbar('Caja seleccionada correctamente', { variant: 'success' });
enqueueSnackbar('Venta completada exitosamente', { variant: 'success' });
enqueueSnackbar(`${producto.nombre} no tiene stock`, { variant: 'warning' });

// DESPUÉS — con jerarquía y propósito:
const { operacional, sistema } = useContextualNotification();

operacional(`Sin stock: ${producto.nombre}`, `Disponible: 0 unidades`);
sistema(`Error al procesar la venta: ${mensaje}`);
// "Venta completada" → ELIMINAR: el diálogo VentaConfirmDialog ya lo comunica
// "Caja seleccionada" → ELIMINAR: informacional, no accionable
```

**Métrica de éxito:** Tasa de acción sobre notificación > 70%. 0 notificaciones informacionales enviadas.

**Modo degradado:** Si SignalR no está disponible, notificaciones críticas se muestran como banner persistente al siguiente poll.

**En SincoPos hoy:**

| Componente | Estado |
|-----------|--------|
| `useNotifications` con SignalR | ✅ Implementado |
| `OfflineStatusBanner` con conteo de pendientes | ✅ Implementado |
| `useContextualNotification` con jerarquía de prioridad | 📋 Pendiente |
| `ExpiresAt` por notificación | 📋 Pendiente |

---

### Capa 8 · Adaptación dinámica

> La interfaz y los flujos cambian según el rol, el contexto y la experiencia del usuario.

**Backend:**

```csharp
public class UiContextService
{
    public async Task<RegisterUiConfig> GetConfig(Guid userId, Guid registerId)
    {
        var user = await _users.GetWithRole(userId);
        return user.Role switch
        {
            Role.Cashier    => new RegisterUiConfig { ShowPriceOverride = false, Layout = UiLayout.Simplified, QuickProductsLimit = 12 },
            Role.Supervisor => new RegisterUiConfig { ShowPriceOverride = true, ShowAuditLog = true, Layout = UiLayout.Extended },
            Role.Manager    => new RegisterUiConfig { ShowDashboard = true, ShowCrossStoreData = true, Layout = UiLayout.Dashboard },
            _               => UiConfig.Default
        };
    }
}
```

**Frontend — `frontend/src/hooks/useUiConfig.ts`** (archivo nuevo):

```typescript
import { useMemo } from 'react';
import { useAuth } from './useAuth';

export interface RegisterUiConfig {
  rolLabel:             string;
  showInventoryDetails: boolean;
  showPriceOverride:    boolean;
  showAuditLog:         boolean;
  showCrossStoreData:   boolean;
  showDashboard:        boolean;
  quickProductsLimit:   number;
  layout:               'simplified' | 'extended' | 'dashboard';
  drawerSections:       string[];
}

export function useUiConfig(): { uiConfig: RegisterUiConfig } {
  const { isCajero, user } = useAuth();

  const uiConfig = useMemo((): RegisterUiConfig => {
    const roles      = user?.roles ?? [];
    const isManager  = roles.includes('Admin') || roles.includes('Gerente');
    const isSuperv   = roles.includes('Supervisor');

    if (isManager) return {
      rolLabel: 'Gerente', showInventoryDetails: true, showPriceOverride: true,
      showAuditLog: true, showCrossStoreData: true, showDashboard: true,
      quickProductsLimit: 0, layout: 'dashboard',
      drawerSections: ['pos', 'ventas', 'inventario', 'reportes', 'configuracion', 'auditoria'],
    };

    if (isSuperv) return {
      rolLabel: 'Supervisor', showInventoryDetails: true, showPriceOverride: true,
      showAuditLog: true, showCrossStoreData: false, showDashboard: false,
      quickProductsLimit: 20, layout: 'extended',
      drawerSections: ['pos', 'ventas', 'inventario', 'reportes'],
    };

    return {  // Cajero — mínimo necesario para operar
      rolLabel: 'Cajero', showInventoryDetails: false, showPriceOverride: false,
      showAuditLog: false, showCrossStoreData: false, showDashboard: false,
      quickProductsLimit: 12, layout: 'simplified',
      drawerSections: ['pos', 'devoluciones'],
    };
  }, [user?.roles]);

  return { uiConfig };
}
```

**Uso en `AppLayout.tsx`:**
```tsx
const { uiConfig } = useUiConfig();

// Drawer toolbar con identidad Sinco:
<Toolbar sx={{ background: sincoColors.gradients.heroBlue, minHeight: `${sincoSpacing.appBarHeight}px !important` }}>
  <Typography variant="h6" fontWeight={700} color="white">{APP_NAME}</Typography>
  <Chip label={uiConfig.rolLabel} size="small" sx={{ bgcolor: 'rgba(255,255,255,0.15)', color: 'white', fontSize: '0.7rem' }} />
</Toolbar>

// Filtrar secciones del menú por rol:
{menuSections
  .filter(s => uiConfig.drawerSections.includes(s.id))
  .map((section, i) => <MenuSection key={i} section={section} />)
}
```

**Uso en `POSPage.tsx`:**
```tsx
const { uiConfig } = useUiConfig();

// Ocultar controles que el cajero no puede usar:
{uiConfig.showPriceOverride && <IconButton onClick={handleEditPrice}><EditIcon /></IconButton>}
```

**Métrica de éxito:** Cajero nunca ve opciones que no puede usar. Supervisor no cambia de pantalla para su rol.

**Modo degradado:** Si el servicio falla, aplicar perfil `Default` con acciones básicas seguras para todos los roles.

**En SincoPos hoy:**

| Componente | Estado |
|-----------|--------|
| `ProtectedRoute` con control por roles | ✅ Implementado |
| Menú dinámico por rol (`menuSections.tsx`) | ✅ Implementado |
| `useUiConfig` con configuración granular por rol | 📋 Pendiente |
| Adaptación por momento del día (apertura/pico/cierre) | 📋 Pendiente |

---

### Capa 9 · Aprendizaje continuo

> Cada acción del usuario alimenta el sistema — aprende en tres niveles.

```csharp
// Nivel individual (insumo para Capa 5)
public class CashierPatternProjection : SingleStreamProjection<CashierPattern>
{
    public void Apply(SaleCompletedEvent evt, CashierPattern pattern)
    {
        pattern.RecordSale(evt.Items, evt.Hour, evt.DayOfWeek);
        pattern.UpdateTopProducts();
        pattern.UpdatePeakHours();
    }
}

// Nivel organizacional (insumo para Capa 14)
public class StorePatternProjection : MultiStreamProjection<StorePattern, Guid>
{
    public override Guid Identity(SaleCompletedEvent evt) => evt.StoreId;

    public void Apply(SaleCompletedEvent evt, StorePattern pattern)
    {
        pattern.RecordSale(evt);
        pattern.UpdateStockVelocity(evt.Items);
        pattern.DetectAnomalies();
    }
}
// Nivel colectivo: ver Capa 13
```

| Nivel | Fuente | Consumidor | Beneficio |
|-------|--------|------------|-----------|
| Individual | Acciones del cajero | Capa 5 (anticipación) | Sugerencias personalizadas |
| Organizacional | Ventas de la tienda | Capa 14 (radar) | Optimización de stock local |
| Colectivo | Todas las tiendas | Capa 13 (inteligencia colectiva) | Mejora global del sistema |

**Métrica de éxito:** Proyecciones reconstruibles < 5s desde el event store. Mínimo 30 días de datos para activar sugerencias individuales.

**Modo degradado:** Sin suficientes eventos, proyecciones retornan vacío. Consumidores tienen fallback a datos globales de la tienda.

**En SincoPos hoy:**

| Componente | Estado |
|-----------|--------|
| `SaleCompletedEvent` y `StockAdjustedEvent` capturados | ✅ Implementado |
| Proyecciones Marten (`InventarioProjection`) | ✅ Implementado |
| `CashierPatternProjection` | ✅ Implementado |
| `StorePatternProjection` (velocidad de productos + horas pico) | ✅ Implementado |

---

### Capa 10 · Explicabilidad

> Toda acción automática tiene un "por qué" visible para el usuario.

**Backend:**

```csharp
public record AutomaticAction(
    string Description,
    string Reason,      // "Detecté patrón: vendes esto cada viernes"
    string DataSource,  // "Basado en 47 ventas históricas"
    double Confidence,  // 0.87
    bool   CanOverride
);

public class SuggestionService
{
    public async Task<AutomaticAction> SuggestReorder(Guid productId, Guid storeId)
    {
        var pattern = await _patterns.GetStockPattern(productId, storeId);
        return new AutomaticAction(
            Description: $"Reordenar {pattern.RecommendedQty} unidades de {pattern.ProductName}",
            Reason:      $"Stock actual ({pattern.CurrentStock}) cae por debajo del mínimo para {pattern.UpcomingPeakDay}",
            DataSource:  $"Basado en {pattern.SampleSize} semanas de datos",
            Confidence:  pattern.ConfidenceScore,
            CanOverride: true
        );
    }
}
```

**Frontend — campo `reason` visible en `AuditTimeline`** (ver Capa 11):
```tsx
{entry.reason && (
  <Box sx={{ mt: 0.5, p: 0.75, bgcolor: sincoColors.surface.subtle, borderRadius: '6px', borderLeft: `3px solid ${sincoColors.brand[600]}` }}>
    <Typography variant="caption" color="text.secondary">
      <strong>Por qué:</strong> {entry.reason}
    </Typography>
  </Box>
)}
```

**Métrica de éxito:** 100% de acciones automáticas exponen `Reason` y `DataSource`. Tasa de rechazo de sugerencias monitoreable.

**Modo degradado:** Sin datos suficientes para calcular `Confidence`, la sugerencia no se muestra. Nunca se muestra una sugerencia sin respaldo de datos.

**En SincoPos hoy:**

| Componente | Estado |
|-----------|--------|
| Alertas de vencimiento con contexto (`AlertaVencimientoBackgroundService`) | 🔧 Parcial |
| `AutomaticAction` con `Reason` / `DataSource` / `CanOverride` | 📋 Pendiente |
| UI para mostrar y rechazar sugerencias | 📋 Pendiente |

---

## 8. Etapa 4 — Gobernanza, inteligencia colectiva y orquestación (Capas 11–15)

**Alineación con fases SincoPos:** Fase 7–8
**Objetivo:** Auditoría total, ética en automatización, inteligencia entre tiendas y orquestación de todo el pipeline.

---

### Capa 11 · Transparencia operativa

> Toda acción — manual o automática — deja rastro visible y comprensible.

**Backend:**

```csharp
// El event store de Marten YA ES trazabilidad automática
public class AuditTrailProjection : MultiStreamProjection<AuditTrail, Guid>
{
    public void Apply(SaleCompletedEvent evt, AuditTrail trail)
        => trail.AddEntry(new AuditEntry
        {
            Timestamp = evt.Timestamp, Actor = evt.CashierId.ToString(),
            ActorType = ActorType.Human, Action = "Venta completada",
            Details = $"{evt.Items.Count} productos · ${evt.Total}", IsAutomated = false
        });

    public void Apply(StockAutoAdjustedEvent evt, AuditTrail trail)
        => trail.AddEntry(new AuditEntry
        {
            ActorType = ActorType.System, Action = "Ajuste automático de stock",
            Reason = evt.TriggerReason, IsAutomated = true
        });
}
```

**Frontend — `frontend/src/features/auditoria/components/AuditTimeline.tsx`** (archivo nuevo):

```tsx
import { Box, Typography, Chip, Divider, Avatar } from '@mui/material';
import PersonIcon   from '@mui/icons-material/Person';
import SmartToyIcon from '@mui/icons-material/SmartToy';
import { sincoColors } from '@/theme/tokens';

interface AuditEntry {
  id: string; timestamp: string; actor: string;
  actorType: 'human' | 'system'; action: string;
  details?: string; reason?: string; isAutomated: boolean;
}

export function AuditTimeline({ entries }: { entries: AuditEntry[] }) {
  return (
    <Box>
      {entries.map((entry, i) => (
        <Box key={entry.id}>
          <Box sx={{ display: 'flex', gap: 2, py: 1.5, alignItems: 'flex-start' }}>
            <Avatar sx={{
              width: 32, height: 32, mt: 0.5,
              bgcolor: entry.actorType === 'human' ? sincoColors.brand[50] : sincoColors.surface.subtle,
              color:   entry.actorType === 'human' ? sincoColors.brand[800] : sincoColors.text.secondary,
            }}>
              {entry.actorType === 'human' ? <PersonIcon sx={{ fontSize: 16 }} /> : <SmartToyIcon sx={{ fontSize: 16 }} />}
            </Avatar>
            <Box sx={{ flex: 1 }}>
              <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', mb: 0.5 }}>
                <Typography variant="body2" fontWeight={600}>{entry.action}</Typography>
                {entry.isAutomated && (
                  <Chip label="automático" size="small" sx={{ height: 18, fontSize: '0.65rem', bgcolor: sincoColors.info.bg, color: sincoColors.info.main }} />
                )}
              </Box>
              {entry.details && <Typography variant="caption" color="text.secondary" display="block">{entry.details}</Typography>}
              {/* Capa 10 — el sistema explica sus acciones */}
              {entry.reason && (
                <Box sx={{ mt: 0.5, p: 0.75, bgcolor: sincoColors.surface.subtle, borderRadius: '6px', borderLeft: `3px solid ${sincoColors.brand[600]}` }}>
                  <Typography variant="caption" color="text.secondary">
                    <strong>Por qué:</strong> {entry.reason}
                  </Typography>
                </Box>
              )}
              <Typography variant="caption" color="text.disabled" display="block" mt={0.5}>
                {entry.actor} · {new Date(entry.timestamp).toLocaleTimeString('es-CO')}
              </Typography>
            </Box>
          </Box>
          {i < entries.length - 1 && <Divider sx={{ ml: 6 }} />}
        </Box>
      ))}
    </Box>
  );
}
```

**Métrica de éxito:** Cualquier acción reconstruible < 10s. 0 acciones sin trazabilidad.

**Modo degradado:** La trazabilidad es el último servicio en fallar. Si el audit log no puede escribir, la acción se bloquea.

**En SincoPos hoy:**

| Componente | Estado |
|-----------|--------|
| Activity Logs con `ActivityLogService` + canal | ✅ Implementado |
| Event store Marten (trazabilidad inmutable) | ✅ Implementado |
| `AuditTrailProjection` legible para humanos | 🔧 Parcial (EF Activity Logs existen, falta proyección Marten humanizada) |
| `AuditTimeline` en frontend | 📋 Pendiente |
| Registro de sugerencias rechazadas | 📋 Pendiente |

---

### Capa 12 · Supervisión ética

> Ninguna automatización ocurre sin reglas explícitas y posibilidad de revisión humana.

```csharp
public class EthicalGuardService
{
    public async Task<GuardResult> ValidateAutomation(AutomationRequest req)
    {
        var rules = await _rules.GetForContext(req.Context);
        foreach (var rule in rules)
            if (!rule.IsSatisfied(req)) return GuardResult.Blocked(rule.ViolationMessage);
        await _auditLog.RecordValidation(req, rules);
        return GuardResult.Approved();
    }
}
// Reglas activas en SincoPos:
// - Venta por debajo del costo: bloqueada siempre (ya en VentaService)
// - Descuento automático máximo: 10%
// - Price override requiere supervisor activo en tienda
// - Transferencia de stock > 50 unidades requiere confirmación manual
// - Ninguna venta puede procesarse sin evento en el store
```

**Métrica de éxito:** 0 automatizaciones ejecutadas sin pasar por el guard. Todas las reglas configurables sin redespliegue.

**Modo degradado:** En caso de duda, el guard bloquea y escala al supervisor. Nunca aprueba por defecto.

**En SincoPos hoy:**

| Componente | Estado |
|-----------|--------|
| Validación precio < costo (bloqueada en `VentaService`) | ✅ Implementado |
| Validación de descuentos 0–100% | ✅ Implementado |
| Autorización por rol en controllers | ✅ Implementado |
| `EthicalGuardService` como componente explícito y configurable | 📋 Pendiente |
| Reglas configurables en BD sin redespliegue | 📋 Pendiente |

---

### Capa 13 · Inteligencia colectiva

> Lo que aprende una tienda beneficia a todas — propagación inteligente de patrones.

> ⚠️ **Prerequisito arquitectónico:** Requiere un **servicio central Sinco** que agregue patrones de todas las instancias. Los `_globalPatterns` viven en una BD separada multi-tenant — no en la BD local de cada tienda.
>
> **Criterio de activación:** mínimo 5 tiendas con datos de ≥ 90 días cada una.

```csharp
public class CollectiveIntelligenceService
{
    public async Task PropagatePattern(StorePattern localPattern)
    {
        var globalPattern = await _globalPatterns.GetOrCreate(localPattern.ProductId);
        globalPattern.Merge(localPattern);
        if (globalPattern.StoreCount >= 5 && globalPattern.Confidence >= 0.8)
            await _bus.Publish(new GlobalPatternUpdatedEvent(globalPattern));
    }

    public async Task OnGlobalPatternUpdated(GlobalPatternUpdatedEvent evt)
        => await _localCache.UpdatePattern(evt.Pattern);
}
```

**Métrica de éxito:** Patrón global activo < 24h desde que una tienda lo genera. Mejora medible en tasa de acierto de Capa 5 tras recibir patrones globales.

**Modo degradado:** Si el servicio central no está disponible, cada tienda opera con sus patrones locales (Capa 5 sigue funcionando).

**En SincoPos hoy:**

| Componente | Estado |
|-----------|--------|
| Patrones locales por tienda | 📋 Pendiente (depende de Capa 9) |
| Servicio central Sinco multi-tenant | 🔮 Futuro |
| Bus de mensajes para propagación global | 🔮 Futuro |

---

### Capa 14 · Radar de Negocio

> De datos históricos a decisiones predictivas para gerentes y supervisores.

Esta capa comparte infraestructura con Capa 5 pero su unidad de análisis es la **tienda y el negocio**.

**Backend:**

```csharp
public class BusinessRiskProjection : MultiStreamProjection<BusinessRadar, Guid>
{
    public override Guid Identity(SaleCompletedEvent evt) => evt.StoreId;

    public void Apply(SaleCompletedEvent evt, BusinessRadar radar)
    {
        radar.UpdateRevenue(evt.Total, evt.Timestamp);
        radar.UpdateProductVelocity(evt.Items);
        radar.RecalculateForecasts();  // proyección próximas 2 semanas
        radar.DetectAnomalies();       // ventas inusualmente bajas/altas
    }
}

public async Task<BusinessRadarDto> GetRadar(Guid storeId)
{
    var radar = await _session.LoadAsync<BusinessRadar>(storeId);
    return new BusinessRadarDto
    {
        CurrentRevenue  = radar.TodayRevenue,
        ProjectedWeekly = radar.WeekForecast,
        StockRisks      = radar.ProductsAtRisk,  // ruptura en < 7 días
        TopPerformers   = radar.TopProducts,
        Anomalies       = radar.ActiveAnomalies
    };
}
```

**Frontend — `frontend/src/features/dashboard/components/BusinessRadar.tsx`** (archivo nuevo):

```tsx
import { Grid2 as Grid, Card, CardContent, Typography, Box, Chip } from '@mui/material';
import { AreaChart, Area, XAxis, YAxis, Tooltip, ResponsiveContainer } from 'recharts';
import TrendingUpIcon   from '@mui/icons-material/TrendingUp';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import { sincoColors } from '@/theme/tokens';

interface RadarMetric {
  label: string; value: number;
  format: 'currency' | 'units' | 'percent';
  trend?: 'up' | 'down' | 'stable'; alert?: string;
}

interface BusinessRadarProps {
  metrics:    RadarMetric[];
  forecast:   { date: string; actual?: number; projected: number }[];
  stockRisks: { name: string; daysLeft: number; currentStock: number }[];
}

function MetricCard({ m }: { m: RadarMetric }) {
  const fmt = (v: number) =>
    m.format === 'currency' ? `$${v.toLocaleString('es-CO')}` :
    m.format === 'percent'  ? `${v.toFixed(1)}%` : v.toLocaleString('es-CO');

  const trendColor = m.trend === 'up' ? sincoColors.success.main : m.trend === 'down' ? sincoColors.error.main : sincoColors.text.secondary;

  return (
    <Card sx={{ height: '100%' }}>
      <CardContent>
        <Typography variant="caption" color="text.secondary" fontWeight={600} textTransform="uppercase" letterSpacing="0.05em">
          {m.label}
        </Typography>
        <Typography variant="h4" fontWeight={700} sx={{ my: 0.5 }}>{fmt(m.value)}</Typography>
        {m.trend && (
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
            <TrendingUpIcon sx={{ fontSize: 14, color: trendColor, transform: m.trend === 'down' ? 'scaleY(-1)' : 'none' }} />
            <Typography variant="caption" sx={{ color: trendColor }}>
              {m.trend === 'up' ? 'En alza' : m.trend === 'down' ? 'En baja' : 'Estable'}
            </Typography>
          </Box>
        )}
        {m.alert && (
          <Chip icon={<WarningAmberIcon sx={{ fontSize: '14px !important' }} />} label={m.alert} size="small"
            sx={{ mt: 1, bgcolor: sincoColors.warning.bg, color: sincoColors.warning.main, fontSize: '0.7rem' }} />
        )}
      </CardContent>
    </Card>
  );
}

export function BusinessRadar({ metrics, forecast, stockRisks }: BusinessRadarProps) {
  return (
    <Box>
      <Grid container spacing={2} sx={{ mb: 3 }}>
        {metrics.map((m, i) => (
          <Grid key={i} size={{ xs: 12, sm: 6, md: 3 }}><MetricCard m={m} /></Grid>
        ))}
      </Grid>

      <Card sx={{ mb: 3 }}>
        <CardContent>
          <Typography variant="h6" fontWeight={600} mb={2}>Proyección de ventas — próximos 14 días</Typography>
          <ResponsiveContainer width="100%" height={200}>
            <AreaChart data={forecast}>
              <defs>
                <linearGradient id="actualGrad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%"  stopColor={sincoColors.brand[700]} stopOpacity={0.15} />
                  <stop offset="95%" stopColor={sincoColors.brand[700]} stopOpacity={0} />
                </linearGradient>
              </defs>
              <XAxis dataKey="date" tick={{ fontSize: 11 }} />
              <YAxis tick={{ fontSize: 11 }} tickFormatter={(v) => `${(v/1000).toFixed(0)}k`} />
              <Tooltip formatter={(v: number) => `$${v.toLocaleString('es-CO')}`} />
              <Area type="monotone" dataKey="actual"    stroke={sincoColors.brand[700]}   fill="url(#actualGrad)" strokeWidth={2} name="Real" />
              <Area type="monotone" dataKey="projected" stroke={sincoColors.warning.main} fill="none" strokeWidth={2} strokeDasharray="5 5" name="Proyectado" />
            </AreaChart>
          </ResponsiveContainer>
        </CardContent>
      </Card>

      {stockRisks.length > 0 && (
        <Card>
          <CardContent>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 2 }}>
              <WarningAmberIcon sx={{ color: sincoColors.warning.main }} />
              <Typography variant="h6" fontWeight={600}>Productos en riesgo de ruptura</Typography>
            </Box>
            {stockRisks.map((risk, i) => (
              <Box key={i} sx={{
                display: 'flex', justifyContent: 'space-between', alignItems: 'center',
                p: 1.5, mb: 1,
                bgcolor: risk.daysLeft <= 2 ? sincoColors.error.bg : sincoColors.warning.bg,
                borderRadius: '8px',
                borderLeft: `4px solid ${risk.daysLeft <= 2 ? sincoColors.error.main : sincoColors.warning.main}`,
              }}>
                <Typography variant="body2" fontWeight={600}>{risk.name}</Typography>
                <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
                  <Typography variant="caption" color="text.secondary">{risk.currentStock} uds</Typography>
                  <Chip label={`${risk.daysLeft} día${risk.daysLeft !== 1 ? 's' : ''}`} size="small"
                    sx={{ bgcolor: risk.daysLeft <= 2 ? sincoColors.error.main : sincoColors.warning.main, color: 'white', fontWeight: 700, fontSize: '0.7rem' }} />
                </Box>
              </Box>
            ))}
          </CardContent>
        </Card>
      )}
    </Box>
  );
}
```

**Métrica de éxito:** Forecast de ingresos de la semana siguiente con error < 15%. Alertas de ruptura con ≥ 5 días de anticipación.

**Modo degradado:** Sin suficientes datos (< 30 días), el radar muestra solo datos actuales sin proyecciones. Anomalías requieren mínimo 2 semanas.

**En SincoPos hoy:**

| Componente | Estado |
|-----------|--------|
| Dashboard con ventas del día, semana, mes | ✅ Implementado |
| Reportes Excel por período | ✅ Implementado |
| Alertas de vencimiento de lotes | ✅ Implementado |
| `BusinessRiskProjection` con forecasting | 📋 Pendiente |
| `BusinessRadar` en frontend | 📋 Pendiente |
| Detección de anomalías | 📋 Pendiente |

---

### Capa 15 · Orquestación contextual

> Todas las capas coordinadas en un pipeline inteligente — desde la intención hasta el resultado.

```csharp
public class SaleOrchestrator
{
    public async Task<SaleResult> Process(SaleIntentCommand intent)
    {
        var items         = await _intentResolver.Resolve(intent);         // Capa 1
        await _ethicalGuard.ValidateOrThrow(items);                        // Capa 12
        var enrichedItems = await _contextEnricher.Enrich(items, intent.CustomerId); // Capa 3

        var saleEvent = new SaleCompletedEvent(enrichedItems, intent);
        await _session.Events.AppendOptimistic(intent.RegisterId, saleEvent);
        await _session.SaveChangesAsync();
        // Capas 4, 6: pipeline de dependencias (asíncrono — no bloquea al cajero)

        await _notifications.NotifyIfRelevant(saleEvent);                  // Capa 7
        return SaleResult.Success(enrichedItems);
    }
}
```

> Este orquestador unifica la lógica actualmente dispersa entre `VentaService` (backend) y `handleCobrar` (frontend) en un solo pipeline auditable.

**Métrica de éxito:** Latencia total (intención → confirmación al cajero) < 500ms. Cada paso del pipeline trazable individualmente.

**Modo degradado:** Si un paso falla después de persistir el evento, el sistema registra el estado parcial y permite reanudar. El cajero recibe confirmación solo cuando el evento está en el store — el resto es asíncrono.

**En SincoPos hoy:**

| Componente | Estado |
|-----------|--------|
| `VentaService.CrearVenta` (lógica centralizada) | ✅ Implementado |
| `handleCobrar` con validaciones pre-venta | ✅ Implementado |
| ERP outbox post-venta | ✅ Implementado |
| `SaleOrchestrator` unificado | 📋 Pendiente |
| Trazabilidad por paso del pipeline | 📋 Pendiente |

---

## 9. Resumen de etapas y fases

| Etapa | Capas | Fases SincoPos | Valor entregado |
|-------|-------|----------------|-----------------|
| **1 · Intención y fluidez** | 1, 2, 3 | Fase 1–2 | Cajero opera con menos clics, sin repetir datos |
| **2 · Coordinación e integración** | 4, 5, 6 | Fase 3–4 | Eventos coordinan módulos, ERP integrado, anticipación activa |
| **3 · Inteligencia adaptativa** | 7, 8, 9, 10 | Fase 5–6 | Sistema aprende, notifica con propósito, se adapta por rol |
| **4 · Gobernanza y orquestación** | 11, 12, 13, 14, 15 | Fase 7–8 | Auditoría total, inteligencia entre tiendas, radar predictivo |

### Estado consolidado por capa

| Capa | Nombre | Backend | Frontend |
|------|--------|---------|----------|
| 1 | Entrada multimodal | 🔧 Parcial | ✅ Implementado (IntentSearch + CameraInput + chips frecuentes) |
| 2 | Tiempo como valor | 🔧 Parcial | ✅ Implementado (PWA offline + useTurnPreload) |
| 3 | Repetición cero | 🔧 Parcial | ✅ Implementado (posSessionCache) |
| 4 | Dependencias inteligentes | ✅ Implementado | ✅ Implementado |
| 5 | Anticipación funcional | ✅ Implementado (UserBehaviorProjection + /productos/anticipados) | ✅ Implementado (useAnticipatedProducts + chips en IntentSearch) |
| 6 | Integración extendida | 🔧 Parcial | ✅ Implementado |
| 7 | Comunicación contextual | 🔧 Parcial | ✅ Implementado (useContextualNotification + integrado en POSPage) |
| 8 | Adaptación dinámica | 📋 Pendiente | ✅ Implementado (useUiConfig + AppLayout + CartItem) |
| 9 | Aprendizaje continuo | ✅ Implementado (CashierPatternProjection + StorePatternProjection + AprendizajeController) | N/A |
| 10 | Explicabilidad | 📋 Pendiente | ✅ Implementado (campo reason en AuditTimeline) |
| 11 | Transparencia operativa | 🔧 Parcial | ✅ Implementado (AuditTimeline en AuditoriaPage) |
| 12 | Supervisión ética | 🔧 Parcial | ✅ Implementado (validaciones) |
| 13 | Inteligencia colectiva | 🔮 Futuro | 🔮 Futuro |
| 14 | Radar de negocio | ✅ Implementado (BusinessRiskProjection + RadarController + 8 tests) | ✅ Implementado (BusinessRadar en DashboardPage) |
| 15 | Orquestación contextual | 🔧 Parcial | 🔧 Parcial |

---

## 10. Checklist de implementación frontend

### Archivos a crear — estado 2026-03-17 ✅ COMPLETADO

- [x] `frontend/src/theme/tokens.ts`
- [x] `frontend/src/components/common/HeroBanner.tsx`
- [x] `frontend/src/hooks/useUiConfig.ts`
- [x] `frontend/src/hooks/useContextualNotification.ts`
- [x] `frontend/src/features/pos/components/IntentSearch.tsx`
- [x] `frontend/src/features/pos/components/CameraInput.tsx`
- [x] `frontend/src/features/pos/hooks/useAnticipatedProducts.ts`
- [x] `frontend/src/features/auditoria/components/AuditTimeline.tsx`
- [x] `frontend/src/features/dashboard/components/BusinessRadar.tsx`

### Archivos modificados — estado 2026-03-17

| Archivo | Cambio | Capa | Estado |
|---------|--------|------|--------|
| `theme/theme.ts` | Tokens centralizados | Todas | ✅ |
| `components/layout/AppLayout.tsx` | `useUiConfig` + `sincoColors` | 8 | ✅ |
| `features/pos/pages/POSPage.tsx` | `HeroBanner` + `IntentSearch` + `useContextualNotification` | 1, 7 | ✅ |
| `features/pos/components/CartItem.tsx` | `showPriceOverride` + `showDiscountOverride` | 8 | ✅ |
| `features/dashboard/pages/DashboardPage.tsx` | `BusinessRadar` + `HeroBanner` + `useUiConfig` | 14 | ✅ |
| `features/auditoria/pages/AuditoriaPage.tsx` | `AuditTimeline` + `useUiConfig` | 11 | ✅ |
| `main.tsx` | `@fontsource/inter` | Todas | ✅ |

### Dependencias instaladas

```bash
npm install @fontsource/inter  # ✅ instalado 2026-03-17
npm install @zxing/browser     # ✅ instalado
```

### Resumen capas → componentes frontend

| Capa | Componente | Archivo |
|------|-----------|---------|
| 1 · Entrada multimodal | `IntentSearch` + `CameraInput` | `pos/components/IntentSearch.tsx` · `pos/components/CameraInput.tsx` |
| 5 · Anticipación | `useAnticipatedProducts` + chips en `IntentSearch` | `pos/hooks/useAnticipatedProducts.ts` |
| 7 · Comunicación | `useContextualNotification` | `hooks/useContextualNotification.ts` |
| 8 · Adaptación | `useUiConfig` + filtrado de menú | `hooks/useUiConfig.ts` |
| 10 · Explicabilidad | Campo `reason` en `AuditTimeline` | `auditoria/components/AuditTimeline.tsx` |
| 11 · Transparencia | `AuditTimeline` | `auditoria/components/AuditTimeline.tsx` |
| 14 · Radar | `BusinessRadar` | `dashboard/components/BusinessRadar.tsx` |

---

*Documento vivo — actualizar estado de implementación con cada sprint.*
*Stack: C# · ASP.NET Core 9 · PostgreSQL 16 · Marten 8 · React 19 · TypeScript · MUI v7 · Vite 7 · Zustand 5 · notistack 3 · recharts 3*
