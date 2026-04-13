# SincoPos - Frontend

Frontend profesional para SincoPos construido con React, TypeScript y Material-UI.

## 🚀 Stack Tecnológico

- **React 19** con **TypeScript 5**
- **Vite 7** - Bundler rápido
- **Material-UI (MUI) v7** - Componentes UI
- **React Router v6** - Navegación
- **TanStack Query v5** - Estado del servidor
- **Axios** - Cliente HTTP
- **WorkOS AuthKit** - Autenticación OAuth2/OIDC
- **Zustand v5** - Estado global
- **notistack** - Notificaciones
- **@microsoft/signalr** - Notificaciones en tiempo real

## 📋 Prerequisitos

Antes de ejecutar el frontend, asegúrate de tener:

1. **Backend API** corriendo en `http://localhost:5086`
2. **Node.js 20+** instalado
3. **npm**

## 🔧 Instalación

```bash
npm install
```

## ⚙️ Configuración

El proyecto usa variables de entorno definidas en `.env.development`:

```env
VITE_API_URL=http://localhost:5086
VITE_WORKOS_CLIENT_ID=client_xxxxxxxxxxxxxxxxxxxx
VITE_API_VERSION=v1
```

## 🏃 Ejecutar en Desarrollo

```bash
npm run dev
```

Frontend disponible en: `http://localhost:5173`

## 🏗️ Build para Producción

```bash
npm run build
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
├── hooks/           # Custom hooks (useAuth, useNotifications, …)
├── stores/          # Zustand stores (auth, offline, cart)
├── types/           # TypeScript types
├── utils/           # Utilidades
├── offline/         # Servicio de cola offline (IndexedDB)
├── App.tsx          # Componente principal
└── main.tsx         # Entry point
```

## 🔐 Autenticación

El sistema usa **WorkOS AuthKit** para autenticación OAuth2/OIDC:

1. Usuario accede a la aplicación
2. Es redirigido a WorkOS para login
3. WorkOS devuelve tokens JWT
4. Los tokens se almacenan en sessionStorage
5. Axios añade automáticamente el token a las peticiones

## 🧪 Testing

```bash
# Todos los tests (Vitest)
npm run test:run

# Modo watch
npm run test
```

## 🔗 Enlaces Útiles

- [Material-UI Docs](https://mui.com/)
- [TanStack Query Docs](https://tanstack.com/query/latest)
- [WorkOS AuthKit Docs](https://workos.com/docs/user-management)
- [React Router Docs](https://reactrouter.com/)
