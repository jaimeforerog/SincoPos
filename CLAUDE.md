# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Backend
```bash
# Build
dotnet build SincoPos.sln

# Run API (dev)
dotnet run --project POS.Api/POS.Api.csproj --urls "http://localhost:5086"

# Run Azure Functions (dev)
dotnet run --project POS.Functions/POS.Functions.csproj

# Migrations
dotnet ef migrations add <NombreMigracion> --project POS.Infrastructure --startup-project POS.Api
dotnet ef database update --project POS.Infrastructure --startup-project POS.Api

# Tests
dotnet test tests/POS.UnitTests/POS.UnitTests.csproj
dotnet test tests/POS.IntegrationTests/POS.IntegrationTests.csproj
dotnet test --filter "DisplayName~VentasTests"
```

### Frontend
```bash
cd frontend
npm run dev          # Vite dev server → http://localhost:5173
npm run typecheck    # tsc --noEmit
npm run lint         # ESLint
npm run test         # Vitest watch
npm run test:run     # Vitest single run
npm run test:coverage
```

## Architecture

### Solution structure
```
POS.Domain          → Agregados, eventos de dominio, interfaces puras
POS.Application     → DTOs, interfaces de servicios, validadores (FluentValidation)
POS.Infrastructure  → EF Core, Marten, implementaciones de servicios, clientes externos
POS.Api             → Controllers, Middleware, SignalR Hubs, Program.cs
POS.Functions       → Azure Functions (solo ErpSyncFunction por ahora)
DbMigrator          → Utilidad CLI para aplicar migraciones en staging/prod
tests/POS.UnitTests          → xUnit + FluentAssertions, solo referencia POS.Domain
tests/POS.IntegrationTests   → xUnit + WebApplicationFactory + Testcontainers (PostgreSQL real)
frontend/           → React 19 + TypeScript + Vite + MUI v7
```

### Multi-tenancy
`ICurrentEmpresaProvider` (scoped) es el eje del multi-tenancy. El middleware `EmpresaContextMiddleware` lo setea al inicio de cada request resolviendo el `EmpresaId` desde el header `X-Empresa-Id` (validado contra `usuario_sucursales`) o como fallback por la primera empresa activa del usuario. Los global query filters en `AppDbContext` filtran automáticamente por `EmpresaId`. En Azure Functions no hay request, por lo que se registra `BackgroundEmpresaProvider` con `EmpresaId = null`, lo que hace pasar todos los filtros.

### Event Sourcing (inventario)
Marten (esquema `events`) maneja el estado de inventario mediante eventos de dominio:
- `EntradaCompraRegistrada`, `SalidaVentaRegistrada`, `DevolucionProveedorRegistrada`, `AjusteInventarioRegistrado`, `StockMinimoActualizado`, `VentaCompletadaEvent`
- Las proyecciones son **inline** — se aplican sincrónicamente en el mismo `SaveChanges`
- El resto del sistema usa EF Core (esquema `public`)

### Patrón Outbox (ERP)
La tabla `erp_outbox_messages` actúa como cola persistente. Los servicios (`VentaErpService`, `CompraErpService`) escriben un `ErpOutboxMessage` en la misma transacción que la venta/compra. La `ErpSyncFunction` (Timer Trigger cada 30 s) lee hasta 10 mensajes pendientes, llama a `IErpClient` y actualiza el estado. Máximo `ErpSinco:MaxReintentos` reintentos (default: 5); luego el mensaje pasa a `Descartado`.

### Motor tributario colombiano (`ITaxEngine`)
Calcula en cascada: IVA (19 %/5 %/0 %) → INC (8 %, monofásico) → Impuesto Saludable (tramos por g/100 ml) → Bolsa plástica (valor fijo) → Retenciones (matriz vendedor/comprador + UVT) → ReteICA (territorial). La configuración viene de tablas de BD (`Impuesto`, `RetencionRegla`, `TramoBebidasAzucaradas`). Genera flag de factura electrónica si el total supera 5 × UVT.

### Facturación electrónica DIAN
Pipeline: XML UBL 2.1 → firma XMLDSIG con PKCS#12 → `DianSoapService` (circuit breaker + retry exponencial). La generación corre en `FacturacionBackgroundService` (fire-and-forget) para no bloquear la confirmación de venta. Estado rastreado en `DocumentoElectronico`.

### Autenticación
WorkOS (no Keycloak). El frontend usa `@workos-inc/authkit-react`. El backend valida JWT contra el JWKS de WorkOS. El rol del usuario se mapea desde la tabla `usuarios` en cada request (no viene en el token).

### Frontend
- **Estado servidor**: TanStack Query v5
- **Estado cliente**: Zustand v5 (auth, `activeSucursalId`)
- **Notificaciones real-time**: `@microsoft/signalr` conectado a `/hubs/notificaciones`, agrupa por `sucursal-{id}`
- Organizado por features en `src/features/` (pos, ventas, compras, inventario, cajas, traslados, etc.)

## Configuration

### Backend — variables críticas para desarrollo local
Configurar con `dotnet user-secrets` en `POS.Api`:
```
ConnectionStrings:Postgres   Host=localhost;Port=5432;Database=sincopos;Username=postgres;Password=...
WorkOs:ClientId              client_...
WorkOs:ApiKey                sk_test_...
```
`ErpSinco:BaseUrl` vacío activa `MockErpClient` automáticamente.

### Frontend — `.env.local`
```
VITE_WORKOS_CLIENT_ID=client_...
```
`VITE_API_URL` y `VITE_SIGNALR_HUB_URL` tienen defaults de desarrollo en `src/config.ts`.

### Migraciones
Se aplican automáticamente en startup de `Program.cs`. Para agregar una nueva migración, siempre especificar ambos proyectos (`--project POS.Infrastructure --startup-project POS.Api`). Nombrar con formato `PascalCase` descriptivo.

## Known constraints

- Los tests de integración levantan un contenedor PostgreSQL real via Testcontainers; requieren Docker corriendo.
- El `AppDbContext` tiene dos constructores: DI resuelve el completo solo si puede inyectar `ICurrentEmpresaProvider`. En contextos sin DI completo (tests manuales, etc.) se puede usar el constructor mínimo con solo `DbContextOptions`.
- Soft delete se aplica via `ISoftDelete` + filtro global `Activo = true`. Para incluir registros inactivos usar `.IgnoreQueryFilters()`.
- La columna `Payload` de `ErpOutboxMessage` es tipo JSONB en PostgreSQL.
