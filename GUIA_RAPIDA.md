# SINCO POS - Guía Rápida

## ¿Qué es este proyecto?

Sistema de Punto de Venta (POS) para Colombia con:
- **Backend**: ASP.NET Core 9, EF Core, Marten (Event Sourcing), PostgreSQL 16
- **Frontend**: React 18 + TypeScript + MUI v7
- **Auth**: Keycloak JWT Bearer (realm `sincopos`)
- **Tiempo real**: SignalR WebSocket
- **DIAN**: Facturación electrónica UBL 2.1 + CUFE

---

## Inicio rápido

### Prerrequisitos
- .NET 9 SDK, Node.js 20+, Docker Desktop

### 1. Infraestructura (PostgreSQL + Keycloak)
```bash
docker-compose up -d
# Esperar ~60s hasta que Keycloak esté listo
```

### 2. Configurar Keycloak (primera vez)
```bash
bash scripts/keycloak-init.sh
```
O ver [scripts/keycloak-setup.md](scripts/keycloak-setup.md) para configuración manual.

### 3. Aplicar migraciones
```bash
cd POS.Api
dotnet ef database update --project ../POS.Infrastructure --startup-project .
```

### 4. Ejecutar backend
```bash
dotnet run --project POS.Api/POS.Api.csproj --urls "http://localhost:5086"
```
API: `http://localhost:5086` | Swagger: `http://localhost:5086/swagger`

### 5. Ejecutar frontend
```bash
cd frontend
npm install
npm run dev
```
Frontend: `http://localhost:5173`

---

## Usuarios de prueba (Keycloak)

| Email | Password | Rol |
|-------|----------|-----|
| admin@sincopos.com | Admin123! | admin |
| supervisor@sincopos.com | Supervisor123! | supervisor |
| cajero@sincopos.com | Cajero123! | cajero |

> Los usuarios deben existir en Keycloak (realm `sincopos`) y en la tabla `usuarios` de PostgreSQL.
> El endpoint `GET /api/usuarios/perfil` sincroniza automáticamente el rol de Keycloak → DB al hacer login.

---

## Tests

```bash
# Suite completa (233/234 passing)
dotnet test tests/POS.IntegrationTests/POS.IntegrationTests.csproj

# Grupo específico
dotnet test --filter "VentasTests"
```

Los tests usan `pos_test` como base de datos. PostgreSQL debe estar corriendo en `localhost:5432`.

---

## Estructura del proyecto

```
SincoPos/
├── POS.Api/                    # API REST + Hubs SignalR + Program.cs
│   ├── Controllers/            # 18 controladores
│   ├── Hubs/                   # NotificationHub (SignalR)
│   └── Services/               # NotificationService
├── POS.Application/            # DTOs, interfaces de servicios, validadores FluentValidation
├── POS.Domain/                 # Aggregates Marten + eventos de dominio
├── POS.Infrastructure/         # EF Core, Marten, implementaciones de servicios
│   ├── Data/Entities/          # Entidades + configuraciones EF
│   ├── Migrations/             # Migraciones EF Core
│   └── Services/               # Implementaciones de IXxxService
├── frontend/                   # React 18 + TypeScript + MUI v7
│   └── src/
│       ├── features/           # Módulos por dominio
│       ├── hooks/              # useAuth, useNotifications, ...
│       ├── stores/             # Zustand: auth + activeSucursalId
│       └── components/         # Shared: NotificationBell, PageHeader, ...
├── tests/
│   └── POS.IntegrationTests/   # xUnit + WebApplicationFactory + PostgreSQL local
├── scripts/
│   ├── keycloak-init.sh        # Script automático de configuración Keycloak
│   └── keycloak-setup.md       # Guía manual Keycloak
├── .github/workflows/ci.yml    # CI: build + test + Docker push a ghcr.io
└── docker-compose.yml          # PostgreSQL 16 + Keycloak 24
```

---

## Endpoints principales

| Módulo | Base URL |
|--------|----------|
| Productos | `GET/POST /api/Productos` |
| Ventas | `POST /api/Ventas`, `GET /api/Ventas/{id}` |
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

## Notas de arquitectura

- **Event Sourcing**: Inventario usa Marten. Tablas en schema `events`. Ver `InventarioAggregate.cs`.
- **Precios**: cascada `PrecioSucursal` → `Producto.PrecioVenta` → `Costo × MargenCategoria`. Batch sin N+1.
- **Notificaciones**: SignalR grupos por `sucursal-{id}`. Frontend se une al grupo en `useNotifications.ts`.
- **Facturación DIAN**: Background queue (Channel). No bloquea la venta. Reintentos automáticos.
- **Rate Limiting**: Solo activo en producción (100 req/60s por IP).
- **Output Cache**: 5 min en catálogos (categorías, impuestos, sucursales), 1h en geografía.
