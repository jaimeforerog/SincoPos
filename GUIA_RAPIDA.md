# SINCO POS - Guía Rápida

## ¿Qué es este proyecto?

Sistema de Punto de Venta (POS) para Colombia con:
- **Backend**: ASP.NET Core 9, EF Core 9, Marten 8.22 (Event Sourcing), PostgreSQL 16
- **Frontend**: React 19 + TypeScript + MUI v7
- **Auth**: WorkOS (User Management API — producción y desarrollo)
- **Tiempo real**: SignalR WebSocket
- **DIAN**: Facturación electrónica UBL 2.1 + CUFE

**Estado del proyecto**: ✅ Production-ready · Calificación técnica: **8.2/10**

---

## Inicio rápido

### Prerrequisitos
- .NET 9 SDK, Node.js 20+, Docker Desktop

### 1. Infraestructura (PostgreSQL)
```bash
docker-compose up -d
```

### 2. Aplicar migraciones
```bash
cd POS.Api
dotnet ef database update --project ../POS.Infrastructure --startup-project .
```

### 3. Ejecutar backend
```bash
dotnet run --project POS.Api/POS.Api.csproj --urls "http://localhost:5086"
```
API: `http://localhost:5086` | Swagger: `http://localhost:5086/swagger`

### 4. Ejecutar frontend
```bash
cd frontend
npm install
npm run dev
```
Frontend: `http://localhost:5173`

---

## Usuarios de prueba

Los usuarios se autentican vía WorkOS. Crea los usuarios en el dashboard de WorkOS y luego créalos en la BD local con:

```bash
.\crear-usuario-dev.ps1
```

> El endpoint `GET /api/v1/usuarios/perfil` sincroniza automáticamente el rol del IdP → DB al hacer login.

---

## Tests

```bash
# Backend — suite completa (363/363 passing)
dotnet test tests/POS.IntegrationTests/POS.IntegrationTests.csproj

# Backend — grupo específico
dotnet test --filter "VentasTests"
dotnet test --filter "TaxEngineUnitTests"   # 31 tests del motor tributario

# Frontend — Vitest (423/423 passing, 0 warnings)
cd frontend && npm run test:run
```

Los tests backend usan `pos_test` como base de datos. PostgreSQL debe estar corriendo en `localhost:5432`.

---

## Estructura del proyecto

```
SincoPos/
├── POS.Api/                    # API REST + Hubs SignalR + Program.cs (846 líneas)
│   ├── Controllers/            # 29 controladores REST
│   ├── Extensions/             # ClaimsPrincipalExtensions (WorkOS claims)
│   ├── Hubs/                   # NotificationHub (SignalR)
│   ├── Middleware/             # EmpresaContextMiddleware (multi-tenant)
│   └── Services/               # NotificationService
├── POS.Application/            # DTOs, interfaces de servicios, validadores FluentValidation
├── POS.Domain/                 # Aggregates Marten + eventos de dominio
├── POS.Infrastructure/         # EF Core, Marten, implementaciones de servicios
│   ├── Data/Entities/          # Entidades + configuraciones EF (45 tablas)
│   ├── Migrations/             # Migraciones EF Core
│   └── Services/
│       ├── VentaService.cs               # Venta principal (~504 líneas)
│       ├── VentaAnulacionService.cs      # Anulación de ventas (extraída)
│       ├── VentaDevolucionService.cs     # Devoluciones parciales (extraída)
│       ├── CompraService.cs              # Órdenes de compra — delega recepción
│       ├── CompraRecepcionService.cs     # Recepción de compras (extraída, CQRS)
│       ├── TaxEngine.cs                  # Motor tributario DIAN (~203 líneas)
│       └── FacturacionService.cs         # DIAN UBL 2.1 (~540 líneas)
├── frontend/                   # React 19 + TypeScript + MUI v7
│   └── src/
│       ├── features/           # 23 módulos por dominio
│       ├── hooks/              # useAuth, useNotifications, useDebounce, ...
│       ├── stores/             # Zustand: auth + activeSucursalId
│       └── components/         # Shared: NotificationBell, PageHeader, ...
├── tests/
│   └── POS.IntegrationTests/   # xUnit + WebApplicationFactory + Testcontainers
├── scripts/                    # Scripts de utilidad
├── .github/workflows/ci.yml    # CI/CD: 7 jobs (build+test+docker+staging+prod)
└── docker-compose.yml          # PostgreSQL 16
```

---

## Endpoints principales

| Módulo | Base URL |
|--------|----------|
| Productos | `GET/POST /api/Productos` |
| Ventas | `POST /api/Ventas`, `PUT /api/Ventas/{id}/anular` |
| Devoluciones | `POST /api/Devoluciones/{ventaId}/parcial` |
| Inventario | `POST /api/Inventario/entrada`, `/ajuste`, `/salida` |
| Compras | `POST /api/OrdenesCompra`, `/recepcion` |
| Traslados | `POST /api/Traslados` |
| Precios lote | `GET /api/precios/resolver-lote?sucursalId=X` |
| Reportes | `GET /api/Reportes/ventas`, `/inventario-valorizado` |
| Facturación | `PUT /api/Facturacion/configuracion/{sucursalId}` |
| Notificaciones | WebSocket `/hubs/notificaciones` |
| Health | `GET /health`, `GET /health/ready` |

---

## Permisos por rol

| Sección | cajero | supervisor | admin |
|---------|--------|------------|-------|
| Dashboard, POS, Ventas, Inventario, Cajas | ✅ | ✅ | ✅ |
| Compras, Traslados, Devoluciones, Reportes | ❌ | ✅ | ✅ |
| Configuración, Productos, Precios, Terceros, Auditoría | ❌ | ✅ | ✅ |
| Usuarios | ❌ | ❌ | ✅ |

---

## CI/CD — ambientes

| Rama | Ambiente | URL |
|------|----------|-----|
| `develop` | Staging | `$AZURE_APP_NAME_STAGING.azurewebsites.net` |
| `main` | Production | `$AZURE_APP_NAME.azurewebsites.net` |

Secrets requeridos (GitHub → Settings → Environments):
- `AZURE_CREDENTIALS`, `DB_CONNECTION_STRING[_STAGING]`
- `AZURE_STATIC_WEB_APPS_API_TOKEN[_STAGING]`

Variables de repositorio:
- `AZURE_APP_NAME`, `AZURE_APP_NAME_STAGING`
- `AZURE_RESOURCE_GROUP`, `AZURE_RESOURCE_GROUP_STAGING`
- `VITE_API_URL`, `VITE_WORKOS_CLIENT_ID`, `VITE_API_VERSION`

---

## Notas de arquitectura

- **Event Sourcing**: Inventario usa Marten. Tablas en schema `events`. Ver `InventarioAggregate.cs`.
- **VentaService split**: Anulación → `VentaAnulacionService`, Devolución → `VentaDevolucionService`. Transacciones atómicas Marten + EF Core via `NpgsqlTransaction` compartida.
- **TaxEngine**: Motor tributario DIAN idempotente y sin estado. Cascada: IVA → INC → Saludable → Bebidas azucaradas → Bolsa → Retenciones → flag factura electrónica. 31 unit tests.
- **ExternalId**: Columna `external_id` en `usuarios` (antes `keycloak_id`). Migración EF: `RenombrarKeycloakIdAExternalId`.
- **Precios**: cascada `PrecioSucursal` → `Producto.PrecioVenta` → `Costo × MargenCategoria`. Batch sin N+1. Paginación del lado del servidor (pageSize configurable).
- **Notificaciones**: SignalR grupos por `sucursal-{id}`. Frontend se une al grupo en `useNotifications.ts`.
- **Facturación DIAN**: Background queue (Channel). No bloquea la venta. Reintentos automáticos.
- **Rate Limiting**: Solo activo en producción (100 req/60s por IP).
- **Output Cache**: 5 min en catálogos (categorías, impuestos, sucursales), 1h en geografía.
- **ProblemDetails RFC 7807**: Todos los controllers emiten `Problem()` / `ValidationProblem()`. Frontend lee `.detail`.
- **ERP Outbox**: `VentaErpService` y `CompraErpService` emiten eventos dentro de la transacción; `ErpSyncBackgroundService` los procesa en background.
- **Frontend test setup**: `src/test/setup.ts` filtra warnings MUI conocidos (benignos en jsdom) vía `vi.spyOn` en `beforeEach`/`afterEach`.

---

## Deuda técnica conocida

| Ítem | Prioridad | Esfuerzo |
|------|-----------|----------|
| Tests E2E con Playwright (login → venta → factura) | Media | Alto |
| Extraer `useApiErrorHandler()` hook (mutaciones duplicadas) | Baja | Bajo |
| Documentar flujo de auth WorkOS con diagrama de secuencia | Baja | Bajo |
