# SincoPos - Sistema de Punto de Venta

Sistema de Punto de Venta moderno para Colombia con facturación electrónica DIAN, notificaciones en tiempo real y arquitectura limpia.

## Arquitectura

- **Clean Architecture**: Api → Application → Domain → Infrastructure
- **Event Sourcing** con Marten para inventario (entradas, salidas, ajustes)
- **PostgreSQL 16** para persistencia relacional y eventos
- **WorkOS** para autenticación OAuth2/OIDC (User Management API)
- **SignalR** para notificaciones en tiempo real (WebSocket)
- **.NET 9** + **React 19 + TypeScript + MUI v7**

## Módulos implementados

| Módulo | Backend | Frontend |
|--------|---------|----------|
| Productos + UnidadMedida DIAN | ✅ 6 tests | - |
| Inventario (Event Sourcing, FIFO/LIFO/PP) | ✅ 19 tests | ✅ 7 tests |
| Lotes/Vencimiento (FEFO, DiasVidaUtil) | ✅ 14 tests | - |
| Ventas + ERP Outbox + Devoluciones | ✅ 17 tests | ✅ 16 tests |
| Compras + ERP Sinco + Retenciones | ✅ 24 tests | ✅ 18 tests |
| Traslados (FEFO, propagación lotes) | ✅ 14 tests | ✅ 10 tests |
| Precios por sucursal (batch sin N+1) | ✅ 11 tests | - |
| POS (Punto de Venta) | ✅ 18 tests | ✅ 11 tests |
| Cajas (apertura/cierre/arqueo) | ✅ | ✅ 10 tests |
| Sucursales + Multi-sucursal por usuario | ✅ 16 tests | ✅ 22 tests |
| Terceros (CRUD + Fiscal + CIIU) | ✅ 20 tests | ✅ 8 tests |
| Impuestos + TaxEngine (DIAN: IVA, INC, retenciones) | ✅ 31 tests | - |
| Reportes + Dashboard (Excel) | ✅ 15 tests | ✅ 9 tests |
| Geografía (Países / Depto / Municipio) | ✅ 10 tests | - |
| Auditoría (Activity Logs) | ✅ 23 tests | - |
| Facturación Electrónica DIAN (UBL 2.1 + CUFE + firma) | ✅ 42 tests | - |
| Seguridad / Roles frontend | ✅ 17 tests | ✅ 8 tests |
| ERP Ventas (Outbox, VentaErpService) | ✅ 5 tests | ✅ 5 tests |
| Migraciones contables ERP | ✅ 12 tests | - |
| Usuarios | ✅ 10 tests | - |
| CI/CD (GitHub Actions + Docker + Azure) | ✅ | - |
| Cart store (POS) | - | ✅ 25 tests |

**Suite de tests: 363/363 backend · 137/137 frontend — 0 Skips**

## Inicio rápido

### Prerrequisitos

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 20+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)

### 1. Migrar base de datos

```bash
dotnet ef database update --project POS.Infrastructure --startup-project POS.Api
```

### 3. Ejecutar backend

```bash
dotnet run --project POS.Api/POS.Api.csproj --urls "http://localhost:5086"
```

API en `http://localhost:5086` | Swagger en `http://localhost:5086/swagger`

### 4. Ejecutar frontend

```bash
cd frontend
npm install
npm run dev
```

Frontend en `http://localhost:5173`

## Usuarios de prueba

| Email | Password | Rol |
|-------|----------|-----|
| admin@sincopos.com | Admin123! | admin |
| supervisor@sincopos.com | Supervisor123! | supervisor |
| cajero@sincopos.com | Cajero123! | cajero |

## Tests

```bash
# Backend
dotnet test                              # Todos los tests
dotnet test tests/POS.IntegrationTests  # Solo integración

# Frontend
cd frontend && npm run test:run          # Todos los vitest (137 tests)
```

## Estructura del proyecto

```
SincoPos/
├── POS.Api/                    # API REST + Hubs SignalR
│   ├── Controllers/
│   ├── Hubs/                   # NotificationHub (SignalR)
│   └── Services/               # NotificationService
├── POS.Application/            # DTOs, interfaces de servicios, validadores
├── POS.Domain/                 # Aggregates + eventos de dominio
├── POS.Infrastructure/         # EF Core, Marten, implementaciones de servicios
│   ├── Data/Entities/
│   ├── Data/Configurations/
│   ├── Migrations/
│   └── Services/
├── frontend/                   # React 18 + TypeScript + MUI v7
│   └── src/
│       ├── features/           # Módulos por dominio (ventas, inventario, etc.)
│       ├── hooks/              # useAuth, useNotifications, ...
│       ├── stores/             # Zustand (auth, sucursal activa)
│       └── components/
├── tests/
│   └── POS.IntegrationTests/   # Testcontainers + xUnit
├── scripts/                    # Scripts de utilidad y prueba
└── .github/workflows/ci.yml    # CI/CD: build + test + Docker push a ghcr.io
```

## CI/CD

GitHub Actions (`.github/workflows/ci.yml`):
- **backend**: `dotnet build` + `dotnet test` + cobertura
- **frontend**: `npm ci` + ESLint + `vite build`
- **docker**: build + push a `ghcr.io/jaimeforerog/sincopos-api` (solo en push a `main`)

## Roles y permisos

| Ruta | cajero | supervisor | admin |
|------|--------|------------|-------|
| Dashboard, POS, Ventas, Inventario, Cajas | ✅ | ✅ | ✅ |
| Compras, Traslados, Devoluciones, Reportes | ❌ | ✅ | ✅ |
| Configuración, Productos, Precios, Terceros, Auditoría | ❌ | ✅ | ✅ |
| Usuarios | ❌ | ❌ | ✅ |

## Facturación Electrónica DIAN

- UBL 2.1 + CUFE con SHA-384
- Firma digital XMLDSIG con certificado PKCS#12
- Ambientes habilitador (`vpfe-hab`) y producción (`vpfe`)
- Generación de Notas Crédito (devoluciones)
- Background queue fire-and-forget para no bloquear ventas

## Notificaciones en tiempo real

Eventos que se envían automáticamente vía WebSocket al grupo `sucursal-{id}`:

| Evento | Nivel | Disparado por |
|--------|-------|---------------|
| Venta completada | success | VentaService |
| Stock bajo | warning | VentaService (por cada línea ≤ StockMinimo) |
| Traslado recibido | info | TrasladoService |
| Factura aceptada DIAN | success | FacturacionService |
| Factura rechazada DIAN | error | FacturacionService |

## Tecnologías

**Backend**: .NET 9, ASP.NET Core, EF Core 9, Marten 8.22, PostgreSQL 16, SignalR, FluentValidation, ProblemDetails RFC 7807, Rate Limiting, Response Compression, Health Checks

**Frontend**: React 19, TypeScript, Vite 7, MUI v7, TanStack Query v5, Zustand v5, notistack, @microsoft/signalr

**Auth**: WorkOS User Management API — JWT via `IIdentityProviderService` (WorkOsIdentityProviderService)

**Testing backend**: xUnit, FluentAssertions, WebApplicationFactory + Testcontainers (PostgreSQL)

**Testing frontend**: Vitest 4, @testing-library/react 16, MSW v2, jsdom

**DevOps**: Docker, Docker Compose, GitHub Actions, ghcr.io
