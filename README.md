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
| Ventas + ERP Outbox + Anulaciones + Devoluciones | ✅ 17 tests | ✅ 16 tests |
| Compras + ERP Sinco + Retenciones | ✅ 24 tests | ✅ 18 tests |
| Traslados (FEFO, propagación lotes) | ✅ 14 tests | ✅ 10 tests |
| Precios por sucursal (batch sin N+1, paginación) | ✅ 11 tests | - |
| POS (Punto de Venta) | ✅ 18 tests | ✅ 11 tests |
| Cajas (apertura/cierre/arqueo) | ✅ | ✅ 10 tests |
| Sucursales + Multi-sucursal por usuario | ✅ 16 tests | ✅ 22 tests |
| Terceros (CRUD + Fiscal + CIIU) | ✅ 20 tests | ✅ 8 tests |
| Impuestos + TaxEngine (DIAN: IVA, INC, Saludable, Bolsa, retenciones) | ✅ 31 tests | - |
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

**Suite de tests: 363/363 backend · 423/423 frontend — 0 Skips · 0 Warnings**

## Inicio rápido

### Prerrequisitos

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 20+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)

### 1. Infraestructura (PostgreSQL)

```bash
docker-compose up -d
```

### 2. Migrar base de datos

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

Los usuarios se autentican vía WorkOS. Para desarrollo local, crear con:

```bash
.\crear-usuario-dev.ps1
```

> `GET /api/v1/usuarios/perfil` sincroniza automáticamente rol IdP → BD al hacer login.

## Tests

```bash
# Backend — suite completa (363 tests)
dotnet test tests/POS.IntegrationTests/POS.IntegrationTests.csproj

# Backend — grupo específico
dotnet test --filter "VentasTests"
dotnet test --filter "TaxEngineUnitTests"

# Frontend — Vitest (424 tests, 0 warnings)
cd frontend && npm run test:run
```

## Estructura del proyecto

```
SincoPos/
├── POS.Api/                    # API REST + Hubs SignalR
│   ├── Controllers/            # 29 controladores REST
│   ├── Extensions/             # ClaimsPrincipalExtensions (WorkOS)
│   ├── Hubs/                   # NotificationHub (SignalR)
│   ├── Middleware/             # EmpresaContextMiddleware
│   └── Services/               # NotificationService
├── POS.Application/            # DTOs, interfaces, validadores FluentValidation
├── POS.Domain/                 # Aggregates Marten + eventos de dominio
├── POS.Infrastructure/         # EF Core, Marten, implementaciones
│   ├── Data/Entities/          # Entidades + configuraciones EF (45 tablas)
│   ├── Migrations/             # Migraciones EF Core
│   └── Services/               # Implementaciones de IXxxService
│       ├── VentaService.cs           # Venta principal (504 líneas)
│       ├── VentaAnulacionService.cs  # Anulación extraída
│       ├── VentaDevolucionService.cs # Devolución extraída
│       ├── CompraRecepcionService.cs # Recepción de compras (extraída de CompraService)
│       └── TaxEngine.cs             # Motor tributario DIAN
├── frontend/                   # React 19 + TypeScript + MUI v7
│   └── src/
│       ├── features/           # Módulos por dominio (23 features)
│       ├── hooks/              # useAuth, useNotifications, ...
│       ├── stores/             # Zustand: auth + activeSucursalId
│       └── components/
├── tests/
│   └── POS.IntegrationTests/   # xUnit + Testcontainers + WebApplicationFactory
├── scripts/                    # Scripts de utilidad
└── .github/workflows/ci.yml    # CI/CD completo (ver sección CI/CD)
```

## CI/CD

GitHub Actions (`.github/workflows/ci.yml`) — pipeline de 7 jobs:

| Job | Trigger | Acción |
|-----|---------|--------|
| **backend** | push/PR main, develop | `dotnet build` + `dotnet test` + cobertura + OpenAPI |
| **frontend** | push/PR main, develop | `npm ci` + ESLint + Vitest + `vite build` |
| **docker** | push main, develop | build + push `ghcr.io/jaimeforerog/sincopos-api` |
| **deploy-staging-backend** | push develop | Migraciones + Azure App Service (`staging`) |
| **deploy-staging-frontend** | push develop | Azure Static Web Apps (`staging`) |
| **deploy-backend** | push main | Migraciones + Azure App Service (`production`) |
| **deploy-frontend** | push main | Azure Static Web Apps (`production`) |

Variables de entorno por environment (`staging` / `production`):
- `AZURE_APP_NAME`, `AZURE_RESOURCE_GROUP`
- `DB_CONNECTION_STRING`, `AZURE_CREDENTIALS`
- `AZURE_STATIC_WEB_APPS_API_TOKEN`

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

## Motor tributario (TaxEngine)

Implementa la cascada tributaria colombiana (Ley 2277/2022):

| Impuesto | Tipo | Cálculo |
|----------|------|---------|
| IVA | Porcentaje sobre base | 19%, 5%, 0% |
| INC | Monofásico (no acumula con IVA) | 8% |
| Saludable ultraprocesados | % sobre base | Per ley |
| Bebidas azucaradas | Tramo g/100ml | ≤6g=$18, ≤10g=$35, >10g=$55 |
| Bolsa plástica | Valor fijo × unidad | $66 tarifa 2024 |
| ReteFuente | Matriz vendedor/comprador + UVT | Configurable por DB |
| ReteICA | Territorial (municipio) | Configurable por DB |

Umbral factura electrónica: `totalNeto > 5 × UVT`

## Notificaciones en tiempo real

Eventos enviados vía WebSocket al grupo `sucursal-{id}`:

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

**Testing frontend**: Vitest 4, @testing-library/react 16, MSW v2, jsdom — 0 `act()` warnings, 0 MUI console noise

**DevOps**: Docker, Docker Compose, GitHub Actions, ghcr.io, Azure App Service, Azure Static Web Apps
