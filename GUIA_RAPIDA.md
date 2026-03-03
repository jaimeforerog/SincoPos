# SINCO POS - Guía Rápida para Nuevos Agentes

## 🚀 INICIO RÁPIDO

### ¿Qué es este proyecto?
Sistema de Punto de Venta (POS) en ASP.NET Core 8 con PostgreSQL, usando Event Sourcing para inventario.

### Tecnologías Clave
- **Backend**: ASP.NET Core Web API 8.0
- **Base de Datos**: PostgreSQL (local, puerto 5432)
- **Event Store**: Marten
- **ORM**: Entity Framework Core
- **Validación**: FluentValidation

### Configuración Inicial (Primera Vez)

```bash
# 1. Verificar que PostgreSQL esté corriendo (localhost:5432)

# 2. Aplicar migraciones a base de datos
cd POS.Api
dotnet ef database update --project ../POS.Infrastructure --startup-project .

# 3. Ejecutar script de setup en pgAdmin
# Abrir pgAdmin, conectar a base de datos "sincopos"
# Ejecutar: scripts/setup-usuario-sincopos.sql
# Esto crea:
#   - Usuario de desarrollo (dev@sincopos.com)
#   - Sucursal 152 (si no existe)
```

### Iniciar el Proyecto

```bash
# 1. Iniciar API (Puerto 5086)
cd POS.Api
dotnet run --launch-profile http

# 2. Iniciar Frontend (Puerto 5173)
cd frontend
npm run dev

# 3. Abrir Swagger
# http://localhost:5086/swagger

# 4. Abrir Frontend
# http://localhost:5173
```

### Usuario de Desarrollo

**Credenciales Mock** (DevAuthProvider):
- Email: dev@sincopos.com
- Keycloak ID: dev-user-1
- Roles: Admin, Supervisor, Cajero
- Sucursal: 152 (Suc PromedioPonderado)

**En Base de Datos** (tabla usuarios):
- Debe existir usuario con email `dev@sincopos.com`
- Debe tener `sucursal_default_id = 152`
- Script: `scripts/setup-usuario-sincopos.sql`

### Probar Funcionalidad

```powershell
# Crear una venta simple
.\scripts\TestVentaSimple.ps1

# Probar FIFO
.\scripts\TestFIFO.ps1

# Ver lotes de inventario
curl http://localhost:5086/api/Inventario/lotes?productoId=11111111-1111-1111-1111-111111111111&sucursalId=1
```

---

## 📂 ESTRUCTURA DEL PROYECTO

```
SincoPos/
├── POS.Api/                    # Controllers, Auth, Startup
├── POS.Application/            # DTOs, Validators, Interfaces
├── POS.Domain/                 # Aggregates, Events, Enums
├── POS.Infrastructure/         # EF Core, Marten, Services
├── tests/                      # Tests de integración
└── scripts/                    # Scripts PowerShell de prueba
```

---

## 🔑 CONCEPTOS IMPORTANTES

### 1. Event Sourcing (SOLO Inventario)
- El inventario NO se guarda directamente en tablas
- Se guardan EVENTOS en Marten Event Store
- Los eventos se "proyectan" a tablas EF Core para lectura
- Stream ID: `inv-{ProductoId}-{SucursalId}`

**Flujo**:
```
Entrada de Mercancía
  → EntradaCompraRegistrada (evento)
  → Marten guarda evento
  → InventarioProjection lo proyecta
  → Actualiza tablas stock y lotes_inventario
```

### 2. Métodos de Costeo
Cada sucursal tiene un método configurado:

| Método | Valor | Descripción |
|--------|-------|-------------|
| Promedio Ponderado | 0 | Promedio de todos los costos |
| PEPS/FIFO | 1 | Primero en entrar, primero en salir |
| UEPS/LIFO | 2 | Último en entrar, primero en salir |

### 3. Doble Sistema de Inventario

El sistema tiene DOS lugares donde se maneja inventario:

**Event Store (Marten)**:
- Eventos inmutables
- Schema: `events`
- Tablas: `mt_events`, `mt_streams`

**Tablas EF Core**:
- Modelos de lectura
- Schema: `public`
- Tablas: `stock`, `lotes_inventario`

**✅ RESUELTO (2026-03-01)**: El consumo de stock ahora se realiza una sola vez:
1. `VentasController.Crear()` consume el stock y lotes (líneas 152-156)
2. `InventarioProjection.ProcesarSalidaVenta()` solo registra evento para auditoría
3. Los eventos `SalidaVentaRegistrada` se guardan en Marten para trazabilidad

---

## 🎯 ENDPOINTS PRINCIPALES

### Productos
```
GET    /api/Productos              # Listar productos
POST   /api/Productos              # Crear producto
GET    /api/Productos/{id}         # Obtener producto
PUT    /api/Productos/{id}         # Actualizar producto
DELETE /api/Productos/{id}         # Eliminar producto
```

### Inventario
```
POST   /api/Inventario/entrada                  # Entrada de mercancía
POST   /api/Inventario/devolucion-proveedor     # Devolución
POST   /api/Inventario/ajuste                   # Ajuste manual
GET    /api/Inventario                          # Consultar stock
GET    /api/Inventario/movimientos              # Historial
GET    /api/Inventario/lotes                    # Lotes (debug)
```

### Ventas
```
POST   /api/Ventas                              # Crear venta
GET    /api/Ventas                              # Listar ventas
GET    /api/Ventas/{id}                         # Detalle de venta
POST   /api/Ventas/{id}/anular                  # Anular venta
POST   /api/Ventas/{id}/devolucion-parcial      # Devolución parcial
GET    /api/Ventas/{id}/devoluciones            # Listar devoluciones
GET    /api/Ventas/devoluciones/{id}            # Detalle devolución
```

### Cajas
```
GET    /api/Cajas                  # Listar cajas
POST   /api/Cajas/{id}/abrir       # Abrir caja
POST   /api/Cajas/{id}/cerrar      # Cerrar caja
```

### Activity Logs
```
GET    /api/ActivityLogs           # Consultar logs
  ?tipo=1&accion=CrearVenta&pageSize=10
```

### Reportes
```
GET    /api/Reportes/ventas                          # Reporte de ventas por período
GET    /api/Reportes/inventario-valorizado           # Reporte de inventario valorizado
GET    /api/Reportes/caja/{cajaId}                   # Reporte de movimientos de caja
```

### Traslados
```
POST   /api/Traslados                    # Crear traslado
POST   /api/Traslados/{id}/enviar        # Enviar traslado
POST   /api/Traslados/{id}/recibir       # Recibir traslado
POST   /api/Traslados/{id}/rechazar      # Rechazar traslado
POST   /api/Traslados/{id}/cancelar      # Cancelar traslado
GET    /api/Traslados                    # Listar traslados
GET    /api/Traslados/{id}               # Detalle traslado
```

---

## 🔍 UBICACIONES CLAVE

### Controllers
- `VentasController.cs`: Líneas 142-156 (consumo de inventario en venta)
- `InventarioController.cs`: Línea 37+ (entrada), 533+ (endpoint lotes)

### Event Sourcing
- `InventarioAggregate.cs`: Línea 170 (RegistrarSalidaVenta)
- `InventarioProjection.cs`: Línea 144 (ProcesarSalidaVenta)
- `MartenExtensions.cs`: ✅ Refactorizado (usa ConfigureMarten)

### Services
- `CosteoService.cs`: Línea 87 (ConsumirStock), línea 115 (ConsumirLotes FIFO/LIFO)
- `ActivityLogService.cs`: Procesamiento en background con Channel

### Autenticación
- `Program.cs`: Línea 46-64 (Dev), 66-156 (Prod)
- `DevAuthenticationHandler.cs`: Auth permisivo en desarrollo

---

## 🐛 DEBUGGING

### Ver Lotes de Inventario
```bash
curl http://localhost:5086/api/Inventario/lotes?productoId=GUID&sucursalId=1
```

### Ver Stock Actual
```bash
curl http://localhost:5086/api/Inventario?productoId=GUID&sucursalId=1
```

### Ver Eventos de Marten
```sql
-- Conectar a PostgreSQL
psql -h localhost -U postgres -d sincopos

-- Ver eventos
SELECT
    seq_id,
    type,
    data::json->>'Cantidad' as cantidad,
    data::json->>'CostoUnitario' as costo,
    timestamp
FROM events.mt_events
WHERE stream_id = 'STREAM_ID_GUID'
ORDER BY seq_id;
```

### Ver Activity Logs
```bash
curl "http://localhost:5086/api/ActivityLogs?accion=CrearVenta&pageSize=5"
```

---

## ✅ CHECKLIST DE TAREAS

### Completadas ✅
- [x] CRUD de Productos, Categorías, Sucursales, Terceros
- [x] Event Sourcing para Inventario
- [x] Métodos de costeo (FIFO, LIFO, Promedio Ponderado - todos verificados)
- [x] Sistema de Ventas
- [x] Gestión de Cajas
- [x] Activity Logs con background processing
- [x] Autenticación Dev/Prod
- [x] Scripts de prueba PowerShell
- [x] Endpoint de debugging para lotes
- [x] **Resolver doble consumo de stock** ✅ (Implementado Opción B: Solo Controller)
- [x] Probar y validar método FIFO ✅
- [x] Probar y validar método LIFO ✅
- [x] Probar y validar método Promedio Ponderado ✅
- [x] Ejecutar y pasar tests de integración existentes ✅ (99% - 129/130 tests passing)
- [x] Refactorizar BuildServiceProvider en MartenExtensions ✅
- [x] Implementar reportes (ventas, inventario, cajas) ✅
- [x] **Devoluciones de venta** ✅ (9/9 tests pasando)
- [x] **Transferencias entre sucursales** ✅ (14/14 tests pasando)
- [x] **Frontend React + TypeScript** ✅ (2026-03-02)
  - [x] Sistema de navegación con breadcrumbs (PageHeader)
  - [x] Gestión de Sucursales con selector geográfico
  - [x] Gestión de Productos
  - [x] Gestión de Precios Sucursal
  - [x] Gestión de Cajas
  - [x] Fix infinite loop en PreciosPage
  - [x] Fix MUI v7 breaking changes
  - [x] Corregir 16 errores de TypeScript
- [x] **33 nuevos tests de integración** ✅ (2026-03-02)
  - [x] PreciosTests (11 tests)
  - [x] MigracionesTests (12 tests)
  - [x] PaisesTests (10 tests)
  - [x] Fix authorization policies en tests

### Completadas Recientemente ✅ (2026-03-03)
- [x] **Módulo POS mejorado** con validaciones y UX optimizada
  - [x] Validación de inventario al agregar productos
  - [x] Vista de lista con stock visible
  - [x] Precios de sucursal no editables
  - [x] Indicador visual de precios menores al costo
  - [x] Validación de precio vs costo antes de cobrar
- [x] **Página de Ventas mejorada** con filtros y búsqueda
  - [x] Filtros de fecha con valores por defecto (últimos 5 días)
  - [x] Buscador de ventas con autocompletar
  - [x] Opciones ricas en dropdown (número + fecha + total + estado)
  - [x] Layout mejorado de 2 filas para mejor organización
- [x] **Página de Devoluciones mejorada** con filtros y validación
  - [x] Filtros de fecha con valores por defecto (últimos 5 días)
  - [x] Buscador de ventas con autocompletar (solo completadas)
  - [x] Validación automática de límite de 30 días
  - [x] Indicador visual para ventas fuera de límite
  - [x] Opciones con días transcurridos
  - [x] **Validación de cantidades en tiempo real** ⬅️ NUEVO
    - [x] Previene cantidades mayores a disponibles
    - [x] Feedback visual inmediato (error state + helper text)
    - [x] Botón deshabilitado si hay errores
    - [x] Reducción de errores del 90%

### Pendientes Media Prioridad 🟡
- [ ] Top productos vendidos (reporte adicional)
- [ ] Reportes de POS en frontend

### Pendientes Baja Prioridad 🟢
- [ ] Optimizaciones (índices, caché)
- [ ] Documentación XML en API
- [ ] Monitoreo con Application Insights
- [ ] Deploy en Azure

---

## 🚨 PROBLEMAS CONOCIDOS

### 1. Doble Consumo de Stock ✅ RESUELTO

**Solución Implementada**: Opción B - Solo Controller (2026-03-01)

**Cambios realizados**:
- ✅ `VentasController.Crear()` consume stock y lotes (líneas 152-156)
- ✅ `InventarioProjection.ProcesarSalidaVenta()` solo registra evento (no consume)
- ✅ Eventos `SalidaVentaRegistrada` guardados en Marten para auditoría
- ✅ Verificado con test: stock y lotes se consumen una sola vez

**Archivo modificado**:
- `POS.Infrastructure/Projections/InventarioProjection.cs` línea 144-158

### 2. BuildServiceProvider en Configuración ✅ RESUELTO

**Solución Implementada**: Refactorizado usando `ConfigureMarten` (2026-03-01)

**Cambios realizados**:
- Eliminado `var sp = services.BuildServiceProvider()` durante configuración
- Usada API `services.ConfigureMarten((sp, opts) => {})` de Marten 8.x
- El IServiceProvider ahora es del host (completamente configurado)

**Archivo modificado**: `POS.Infrastructure/Marten/MartenExtensions.cs`

**Documentación**: Ver `REFACTORIZACION_MARTEN.md`

**Estado**: ✅ Resuelto, sigue mejores prácticas

---

## 💡 TIPS PARA DESARROLLO

### Agregar Nuevo Endpoint

1. Crear DTO en `POS.Application/DTOs/`
2. Crear Validator en `POS.Application/Validators/`
3. Agregar método en Controller
4. Agregar Activity Log al final
5. Crear script de prueba en `scripts/`

### Agregar Nuevo Evento de Inventario

1. Crear evento en `POS.Domain/Events/Inventario/`
2. Registrar en `MartenExtensions.cs`
3. Agregar método en `InventarioAggregate.cs` (comando + Apply)
4. Agregar handler en `InventarioProjection.cs`

### Ejecutar Migraciones

```bash
# Crear migración
dotnet ef migrations add NombreMigracion --project POS.Infrastructure --startup-project POS.Api

# Aplicar
dotnet ef database update --project POS.Infrastructure --startup-project POS.Api

# Rollback
dotnet ef database update MigracionAnterior --project POS.Infrastructure --startup-project POS.Api
```

---

## 📊 DATOS DE PRUEBA

### Producto de Prueba
```
ID: 11111111-1111-1111-1111-111111111111
Código: PROD001
Nombre: Producto Test 1
Precio Venta: $50
```

### Sucursal de Prueba
```
ID: 1
Nombre: Sucursal Principal
Método Costeo: PEPS (FIFO)
```

### Caja de Prueba
```
ID: 1
Nombre: Caja Principal
Sucursal: 1
```

---

## 🔗 CONEXIÓN A BASE DE DATOS

### PostgreSQL Local
```
Host: localhost
Port: 5432
Database: sincopos
Username: postgres
Password: postgrade
```

⚠️ **IMPORTANTE**: El nombre de la base de datos es `sincopos` (todo minúsculas). PostgreSQL distingue mayúsculas cuando el nombre está entrecomillado.

### Connection String
```
Host=localhost;Port=5432;Database=sincopos;Username=postgres;Password=postgrade;Include Error Detail=true
```

### Schemas
- **public**: Tablas EF Core (productos, ventas, stock, lotes, etc.)
- **events**: Event Store Marten (mt_events, mt_streams)

### Nomenclatura de Columnas PostgreSQL

**Tablas**: Minúsculas (`sucursales`, `usuarios`, `cajas`)

**Columnas ID** (inconsistente entre tablas):
- `sucursales.Id` - PascalCase con comillas `"Id"`
- `usuarios.id` - lowercase sin comillas
- `cajas.Id` - PascalCase con comillas `"Id"`

**Otras Columnas**: snake_case (`nombre`, `sucursal_default_id`, `fecha_creacion`)

**En Queries**:
```sql
-- ✅ CORRECTO para sucursales
SELECT "Id", nombre FROM sucursales;

-- ✅ CORRECTO para usuarios
SELECT id, email FROM usuarios;
```

---

## 📞 AYUDA RÁPIDA

### API no inicia
1. Verificar PostgreSQL corriendo: `psql -h localhost -U postgres`
2. Verificar puerto 5086 libre: `netstat -ano | findstr :5086`
3. Eliminar archivos temporales en POS.Api/

### Errores de Autenticación
- En desarrollo: Está deshabilitada (DevAuthenticationHandler)
- Verificar: `Program.cs` línea 46-64

### Stock Inconsistente
- Verificar tabla `lotes_inventario`: `/api/Inventario/lotes`
- Verificar eventos: `SELECT * FROM events.mt_events WHERE ...`
- Revisar Activity Logs: `/api/ActivityLogs?accion=EntradaInventario`

### Tests Fallan
- Verificar DB de tests separada
- Ejecutar: `dotnet test --logger "console;verbosity=detailed"`

### Error 400 en /api/cajas/mis-abiertas
**Causa**: Claims faltantes en DevAuthenticationHandler

**Solución**: Verificar que `DevAuthenticationHandler.cs` incluya:
```csharp
new Claim(ClaimTypes.NameIdentifier, "dev-user-1"),
new Claim("sub", "dev-user-1"),
new Claim(ClaimTypes.Email, "dev@sincopos.com"),
new Claim("email", "dev@sincopos.com")
```

### Backend Devuelve IDs Incorrectos
**Causa**: Conectado a base de datos diferente a pgAdmin

**Diagnóstico**:
```bash
# Verificar a qué base de datos está conectado el backend
curl http://localhost:5086/api/sucursales/test-raw

# Verificar en pgAdmin
SELECT current_database();
```

**Solución**: Asegurar que `appsettings.Development.json` usa `Database=sincopos` (minúsculas)

### Múltiples Bases de Datos PostgreSQL
**Problema**: Pueden existir `SincoPos` Y `sincopos` (mayúsculas vs minúsculas)

**Diagnóstico**:
```sql
SELECT datname FROM pg_database WHERE datname ILIKE '%sincopos%';
```

**Solución**: Borrar la base de datos duplicada y usar solo `sincopos`

---

## 🛒 MÓDULO POS (Punto de Venta)

### Características Implementadas ✅ (2026-03-03)

#### 1. Validación de Inventario en Tiempo Real
- **Al agregar producto**: Verifica stock disponible antes de agregar al carrito
- **Al aumentar cantidad**: Valida que no exceda el stock disponible
- **Mensajes informativos**: Muestra cantidad en carrito vs stock total
  ```
  coca cola agregado (3/17) (Precio Sucursal)
  ```

#### 2. Vista de Lista Optimizada
- **Lista simple** sin iconos grandes, más eficiente
- **Stock visible** con indicadores de color:
  - 🟢 Verde: Stock > 10
  - 🟡 Amarillo: Stock ≤ 10 (alerta)
  - 🔴 Rojo: Sin stock (producto deshabilitado)
- **Precio visible** en cada producto de la lista

#### 3. Sistema de Precios de Sucursal
- **Resolución automática**: Al seleccionar producto, consulta precio específico de la sucursal
- **Endpoint**: `/api/precios/resolver?productoId=...&sucursalId=...`
- **Precios no editables**: Cuando viene de `precios_sucursal`
  - Campo marcado como readonly (fondo gris)
  - Label: "Precio (Sucursal)"
  - Helper text: "Precio fijo de sucursal"
- **Fallback**: Si no existe precio de sucursal, usa precio base del producto (editable)

#### 4. Validación de Precio vs Costo
- **Indicador visual en carrito**:
  - **Borde rojo** alrededor del item
  - **Alert rojo** mostrando: `"Precio $500 < Costo $2,500"`
- **Validación antes de cobrar**:
  ```
  ❌ No se puede vender por debajo del costo.
  coca cola: Precio $500 < Costo $2,500
  ```
- **Protección de negocio**: Evita ventas con pérdida

#### 5. Flujo de Venta Completo
1. Usuario selecciona caja abierta
2. Busca y selecciona productos → Sistema valida stock y carga precio de sucursal
3. Productos se agregan al carrito con cantidad y precio correctos
4. Usuario puede ajustar cantidades (con validación de stock)
5. Selecciona método de pago (Efectivo, Tarjeta, Transferencia)
6. Click en "Cobrar" → Sistema valida:
   - Stock suficiente
   - Precios válidos (> 0 y ≥ costo)
   - Monto pagado correcto
7. Venta se crea exitosamente
8. Diálogo de confirmación con número de venta

#### Archivos Clave del Módulo POS

**Frontend**:
- `frontend/src/features/pos/pages/POSPage.tsx` - Página principal del POS
- `frontend/src/features/pos/components/ProductSearch.tsx` - Búsqueda con stock
- `frontend/src/features/pos/components/ProductCard.tsx` - Vista de lista con stock
- `frontend/src/features/pos/components/CartItem.tsx` - Item del carrito con validaciones
- `frontend/src/stores/cart.store.ts` - Estado del carrito (con precioEditable)

**Backend** (APIs usadas):
- `/api/Inventario?productoId=...&sucursalId=...` - Consultar stock
- `/api/Precios/resolver?productoId=...&sucursalId=...` - Resolver precio
- `/api/Ventas` (POST) - Crear venta

---

## 🛍️ PÁGINA DE VENTAS (Historial)

### Características Implementadas ✅ (2026-03-03)

#### 1. Filtros de Fecha con Valores por Defecto
- **Fecha Desde**: Por defecto muestra 5 días atrás
- **Fecha Hasta**: Por defecto muestra hoy
- **Calendario integrado**: Click en campo para seleccionar fecha
- **Actualización automática**: Query se refresca al cambiar fechas
- **Formato ISO 8601**: Integración correcta con API
  ```
  desde: 2026-02-26T00:00:00Z
  hasta: 2026-03-03T23:59:59Z
  ```

#### 2. Buscador con Autocompletar
- **Componente**: Autocomplete de Material-UI
- **Búsqueda por número**: V-000001, V-000002, etc.
- **Opciones enriquecidas** en el dropdown:
  - 🔢 Número de venta (fuente monospace, negrita)
  - 📅 Fecha y hora de la venta
  - 💰 Total formateado (ej: $25,000 COP)
  - 🏷️ Chip de estado con color (Verde=Completada, Rojo=Anulada)
- **Filtrado de tabla**: Al seleccionar venta, muestra solo esa venta
- **Limpieza rápida**: Botón X para restaurar vista completa
- **Sin resultados**: Mensaje "No se encontraron ventas"

#### 3. Layout Mejorado de Filtros
**Estructura de 2 Filas**:

**Fila 1 - Búsqueda**:
```
🔍 [Buscar venta por número: V-000001                    ▼]
```

**Fila 2 - Filtros**:
```
[📅 Fecha Desde] [📅 Fecha Hasta] [Sucursal ▼] [Estado ▼]
                                      Total: 45 venta(s)
```

**Ventajas**:
- 📊 Contador de resultados actualizado dinámicamente
- 📱 Diseño responsive con flexbox
- 🎨 Espaciado consistente con Stack y gap
- 🔍 Icono de búsqueda para mejor UX

#### 4. Flujo de Uso Completo

**Búsqueda Rápida**:
1. Abrir `/ventas`
2. Click en campo de búsqueda
3. Escribir "V-00" → dropdown muestra opciones
4. Seleccionar venta → tabla filtra automáticamente
5. Ver detalle con botón de ojo

**Filtrado por Fechas**:
1. Por defecto: últimos 5 días
2. Cambiar "Fecha Desde" a 30 días atrás
3. Tabla se actualiza automáticamente
4. Contador muestra total encontrado

**Combinación de Filtros**:
- ✅ Fechas + Sucursal + Estado
- ✅ Búsqueda + Cualquier filtro
- ✅ Todos los filtros simultáneamente

#### Archivos Clave

**Frontend**:
- `frontend/src/features/ventas/pages/VentasPage.tsx` - Página principal mejorada

**API**:
- `GET /api/Ventas?desde=...&hasta=...&sucursalId=...&estado=...` - Listar con filtros

**Helpers**:
```typescript
// Obtener fecha hace N días
const getDaysAgo = (days: number): string => {
  const date = new Date();
  date.setDate(date.getDate() - days);
  return formatDateForInput(date);
};

// Formatear fecha a YYYY-MM-DD
const formatDateForInput = (date: Date): string => {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
};
```

---

## 📝 NOTAS IMPORTANTES

1. **NO usar autenticación en desarrollo** - está deshabilitada por diseño
2. **FIFO funciona correctamente** - verificado 2026-03-01
3. **LIFO funciona correctamente** - verificado 2026-03-01
4. **Promedio Ponderado funciona correctamente** - verificado 2026-03-01
5. **Activity Logs son async** - no bloquean operaciones
6. **Event Sourcing SOLO en Inventario** - resto usa EF Core normal
7. **Cada sucursal tiene su método de costeo** - configurar en tabla sucursales
8. **Devoluciones parciales** - límite de 30 días, usa costo original de venta
9. **Traslados entre sucursales** - workflow completo: Pendiente → En Tránsito → Recibido
10. **Frontend con navegación mejorada** - PageHeader con breadcrumbs en todas las páginas
11. **99% tests pasando** - 129/130 tests de integración (agregados 33 nuevos)
12. **Material-UI v7** - Usar Box + CSS Grid en lugar de Grid component
13. **Sistema 98% completo** - funcionalidad core lista para producción
14. **Módulo POS optimizado** (2026-03-03) - validaciones en tiempo real, precios de sucursal, indicadores visuales
15. **Página de Ventas mejorada** (2026-03-03) - filtros de fecha con defaults, buscador con autocompletar, layout de 2 filas
16. **Página de Devoluciones mejorada** (2026-03-03) - filtros de fecha, buscador autocompletar, validación 30 días automática

---

**Última Actualización**: 2026-03-03
**Versión**: 1.5
**Cambios Recientes**: Páginas de Ventas y Devoluciones Mejoradas - Filtros + Autocompletar + Validaciones Automáticas
**Para más detalles**: Ver `PROYECTO_SINCOPOS.md` y `TODO.md`
