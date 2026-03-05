# SincoPos - Frontend

Frontend profesional para SincoPos construido con React, TypeScript y Material-UI.

## 🚀 Stack Tecnológico

- **React 18** con **TypeScript 5**
- **Vite** - Bundler rápido
- **Material-UI (MUI) v6** - Componentes UI
- **React Router v6** - Navegación
- **TanStack Query (React Query) v5** - Estado del servidor
- **Axios** - Cliente HTTP
- **React OIDC Context** - Autenticación con Keycloak
- **Zustand** - Estado global
- **React Hook Form** + **Zod** - Formularios y validación
- **date-fns** - Manejo de fechas
- **notistack** - Notificaciones

## 📋 Prerequisitos

Antes de ejecutar el frontend, asegúrate de tener:

1. **Backend API** corriendo en `http://localhost:5000`
2. **Keycloak** corriendo en `http://localhost:8080`
3. **Node.js** 18+ instalado
4. **npm** o **yarn**

## 🔧 Instalación

```bash
# Instalar dependencias
npm install
```

## ⚙️ Configuración

El proyecto usa variables de entorno definidas en `.env.development`:

```env
VITE_API_URL=http://localhost:5000
VITE_KEYCLOAK_URL=http://localhost:8080
VITE_KEYCLOAK_REALM=sincopos
VITE_KEYCLOAK_CLIENT_ID=sincopos-frontend
```

## 🏃 Ejecutar en Desarrollo

```bash
npm run dev
```

El frontend estará disponible en: `http://localhost:5173`

## 🏗️ Build para Producción

```bash
npm run build
npm run preview
```

## 📁 Estructura del Proyecto

```
src/
├── api/              # Clientes API (axios)
├── components/       # Componentes reutilizables
│   ├── common/      # Componentes genéricos
│   └── layout/      # Layout principal
├── features/        # Módulos por dominio
│   ├── auth/        # Autenticación
│   ├── productos/   # Gestión de productos
│   ├── ventas/      # Punto de venta
│   ├── compras/     # Órdenes de compra
│   ├── inventario/  # Inventario y traslados
│   └── reportes/    # Dashboards y reportes
├── hooks/           # Custom hooks
├── stores/          # Zustand stores
├── types/           # TypeScript types
├── utils/           # Utilidades
├── theme/           # Configuración MUI
├── App.tsx          # Componente principal
└── main.tsx         # Entry point
```

## 🔐 Autenticación

El sistema usa **Keycloak** para autenticación OAuth2/OIDC:

1. Usuario accede a la aplicación
2. Es redirigido a Keycloak para login
3. Keycloak devuelve tokens JWT
4. Los tokens se almacenan en sessionStorage
5. Axios añade automáticamente el token a las peticiones

## 🎨 Características Implementadas

- ✅ **Autenticación** con Keycloak
- ✅ **Dashboard** con métricas y gráficos
- ✅ **Layout** con navegación lateral
- ✅ **Protección de rutas** por roles
- ⏳ Punto de Venta (POS)
- ⏳ Gestión de Productos
- ⏳ Órdenes de Compra
- ⏳ Inventario y Traslados
- ⏳ Reportes avanzados

## 📦 Dependencias Principales

```json
{
  "@mui/material": "^6.x",
  "@mui/x-charts": "^7.x",
  "@mui/x-data-grid": "^7.x",
  "react": "^18.x",
  "react-router-dom": "^6.x",
  "@tanstack/react-query": "^5.x",
  "axios": "^1.x",
  "react-oidc-context": "^3.x",
  "zustand": "^5.x",
  "react-hook-form": "^7.x",
  "zod": "^3.x"
}
```

## 🧪 Testing

```bash
# Tests unitarios (cuando se implementen)
npm test
```

## 📝 Notas

- El frontend consume el API REST del backend SincoPos
- Todos los componentes usan Material-UI para consistencia visual
- TypeScript strict mode habilitado
- React Query maneja el caché y sincronización con el servidor
- Zustand para estado global ligero (auth, carrito POS)

## 🔗 Enlaces Útiles

- [Backend API](http://localhost:5000)
- [Keycloak Admin](http://localhost:8080/admin)
- [Material-UI Docs](https://mui.com/)
- [TanStack Query Docs](https://tanstack.com/query/latest)
- [React Router Docs](https://reactrouter.com/)
