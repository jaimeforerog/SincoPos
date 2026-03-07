# SincoPos - Sistema de Punto de Venta

Sistema de Punto de Venta moderno para Colombia con facturación electrónica DIAN, notificaciones en tiempo real y arquitectura limpia.

## Arquitectura

- **Clean Architecture**: Api → Application → Domain → Infrastructure
- **Event Sourcing** con Marten para inventario (entradas, salidas, ajustes)
- **PostgreSQL 16** para persistencia relacional y eventos
- **Keycloak** para autenticación OAuth2/OIDC
- **SignalR** para notificaciones en tiempo real (WebSocket)
- **.NET 9** + **React 18 + TypeScript + MUI v7**

## Módulos implementados

| Módulo | Estado |
|--------|--------|
| Productos + UnidadMedida DIAN | ✅ |
| Inventario (Event Sourcing, 4 métodos costeo) | ✅ |
| Ventas + Devoluciones parciales | ✅ |
| Cajas + POS (Punto de Venta) | ✅ |
| Sucursales + Multi-sucursal por usuario | ✅ |
| Traslados inter-sucursal | ✅ |
| Precios por sucursal (cascada sin N+1) | ✅ |
| Terceros (CRUD + Fiscal + CIIU) | ✅ |
| Impuestos (TaxEngine: IVA, Inc, Retefuente) | ✅ |
| Compras (órdenes + recepción) | ✅ |
| Reportes + Dashboard | ✅ |
| Geografía (Países / Depto / Municipio) | ✅ |
| Auditoría (Activity Logs) | ✅ |
| Facturación Electrónica DIAN (UBL 2.1 + CUFE) | ✅ |
| Seguridad / Roles frontend | ✅ |
| Notificaciones en tiempo real (SignalR) | ✅ |
| CI/CD (GitHub Actions + Docker) | ✅ |

**Suite de tests: 233/234 pruebas de integración (233 passing, 1 skip)**

## Inicio rápido

### Prerrequisitos

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 20+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)

### 1. Infraestructura

```bash
docker-compose up -d
# Esperar ~60s hasta que Keycloak esté listo
```

### 2. Configurar Keycloak

```bash
bash scripts/keycloak-init.sh
```

O sigue la guía manual en [scripts/keycloak-setup.md](scripts/keycloak-setup.md).

### 3. Migrar base de datos

```bash
dotnet ef database update --project POS.Infrastructure --startup-project POS.Api
```

### 4. Ejecutar backend

```bash
dotnet run --project POS.Api/POS.Api.csproj --urls "http://localhost:5086"
```

API en `http://localhost:5086` | Swagger en `http://localhost:5086/swagger`

### 5. Ejecutar frontend

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
dotnet test                              # Todos los tests
dotnet test tests/POS.IntegrationTests  # Solo integración
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
├── scripts/
│   └── keycloak-init.sh
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

**Backend**: .NET 9, ASP.NET Core, EF Core 9, Marten, PostgreSQL 16, SignalR, FluentValidation, Rate Limiting, Response Compression, Health Checks

**Frontend**: React 18, TypeScript, Vite, MUI v7, TanStack Query, Zustand, notistack, @microsoft/signalr

**Auth**: Keycloak JWT Bearer (realm `sincopos`)

**Testing**: xUnit, FluentAssertions, WebApplicationFactory (PostgreSQL local)

**DevOps**: Docker, Docker Compose, GitHub Actions, ghcr.io
