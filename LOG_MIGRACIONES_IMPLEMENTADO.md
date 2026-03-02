# ✅ Sistema de Log de Migraciones - SincoPOS

**Fecha:** 2026-03-02
**Estado:** ✅ Implementado y Aplicado

---

## 📋 Resumen

Se implementó un **sistema de logging de migraciones de base de datos** que registra automáticamente todas las migraciones aplicadas con información de auditoría completa.

### ¿Qué hace?

- Registra cada migración aplicada en una tabla `migraciones_log`
- Captura información adicional: usuario, fecha, duración, estado, descripción
- Complementa la tabla `__ef_migrations_history` de Entity Framework
- Permite consultar el historial completo de cambios de esquema
- Sincroniza automáticamente migraciones existentes

---

## 🏗️ Arquitectura Implementada

### 1. **Entidad: MigracionLog**

```csharp
public class MigracionLog
{
    public int Id { get; set; }
    public string MigracionId { get; set; }          // Nombre de la migración
    public string Descripcion { get; set; }          // Descripción legible
    public string ProductVersion { get; set; }       // Versión de EF Core
    public DateTime FechaAplicacion { get; set; }    // Cuándo se aplicó
    public string AplicadoPor { get; set; }          // Quién la aplicó
    public string Estado { get; set; }               // Success, Failed, Reverted
    public long DuracionMs { get; set; }             // Tiempo de ejecución
    public string? Notas { get; set; }               // Información adicional
    public string? SqlEjecutado { get; set; }        // SQL ejecutado (opcional)
}
```

### 2. **Servicio: MigracionLogService**

Funciones principales:

#### RegistrarMigracion
Registra una migración aplicada exitosamente.

```csharp
await _migracionLogService.RegistrarMigracion(
    migracionId: "20260302211137_AgregarTablaMigracionesLog",
    descripcion: "Agregar tabla para log de migraciones",
    productVersion: "9.0.1",
    aplicadoPor: "admin@sincopos.com",
    duracionMs: 150
);
```

#### RegistrarMigracionFallida
Registra una migración que falló durante la aplicación.

```csharp
await _migracionLogService.RegistrarMigracionFallida(
    migracionId: "20260302_MigracionProblematica",
    descripcion: "Intento de migración fallido",
    productVersion: "9.0.1",
    error: "Error de sintaxis SQL..."
);
```

#### SincronizarMigracionesExistentes
Sincroniza el log con las migraciones existentes en `__ef_migrations_history`.

```csharp
await _migracionLogService.SincronizarMigracionesExistentes();
// Registra todas las migraciones históricas que no estaban en el log
```

### 3. **Controlador: MigracionesController**

Endpoints disponibles (solo para Admin):

#### GET /api/migraciones
Obtiene el historial de migraciones.

```bash
curl -X GET "http://localhost:5086/api/migraciones?limite=50" \
  -H "Authorization: Bearer $TOKEN"
```

Respuesta:
```json
[
  {
    "id": 1,
    "migracionId": "20260302211137_AgregarTablaMigracionesLog",
    "descripcion": "Agregar Tabla Migraciones Log",
    "productVersion": "9.0.1",
    "fechaAplicacion": "2026-03-02T21:11:37Z",
    "aplicadoPor": "sistema",
    "estado": "Success",
    "duracionMs": 150,
    "notas": "Migración histórica - sincronizada automáticamente"
  }
]
```

#### POST /api/migraciones/sincronizar
Sincroniza migraciones históricas.

```bash
curl -X POST "http://localhost:5086/api/migraciones/sincronizar" \
  -H "Authorization: Bearer $TOKEN"
```

Respuesta:
```json
{
  "mensaje": "Migraciones sincronizadas exitosamente"
}
```

#### POST /api/migraciones/registrar
Registra manualmente una migración.

```bash
curl -X POST "http://localhost:5086/api/migraciones/registrar" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "migracionId": "20260302_MiMigracion",
    "descripcion": "Mi descripción",
    "productVersion": "9.0.1",
    "duracionMs": 200,
    "notas": "Nota adicional"
  }'
```

---

## 🗄️ Tabla de Base de Datos

### Estructura: migraciones_log

| Columna | Tipo | Descripción |
|---------|------|-------------|
| Id | INTEGER | Identificador único (PK) |
| migracion_id | VARCHAR(150) | Nombre de la migración |
| descripcion | VARCHAR(500) | Descripción legible |
| product_version | VARCHAR(32) | Versión de EF Core |
| fecha_aplicacion | TIMESTAMP | Fecha/hora de aplicación (UTC) |
| aplicado_por | VARCHAR(255) | Usuario que aplicó |
| estado | VARCHAR(50) | Success/Failed/Reverted |
| duracion_ms | BIGINT | Duración en milisegundos |
| notas | TEXT | Notas adicionales |
| sql_ejecutado | TEXT | SQL ejecutado (opcional) |

### Índices:
- `IX_migraciones_log_migracion_id` - Para búsquedas rápidas por migración
- `IX_migraciones_log_fecha_aplicacion` - Para ordenar por fecha

---

## 💻 Cómo Usar

### Opción 1: Sincronización Automática

Ejecutar la sincronización al iniciar la aplicación (una vez):

```bash
# Obtener token de admin
TOKEN=$(curl -X POST http://localhost:8080/realms/sincopos/protocol/openid-connect/token \
  -d "client_id=pos-api" \
  -d "username=admin@sincopos.com" \
  -d "password=admin123" \
  -d "grant_type=password" | jq -r '.access_token')

# Sincronizar migraciones históricas
curl -X POST "http://localhost:5086/api/migraciones/sincronizar" \
  -H "Authorization: Bearer $TOKEN"
```

### Opción 2: Registro Manual (Futuro)

Para futuras migraciones, integrar el registro en el proceso de despliegue:

```bash
# Después de aplicar una migración
dotnet ef database update

# Registrar la migración
curl -X POST "http://localhost:5086/api/migraciones/registrar" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "migracionId": "20260302_NuevaMigracion",
    "descripcion": "Descripción de los cambios",
    "productVersion": "9.0.1",
    "duracionMs": 150
  }'
```

### Consultar Historial

```bash
# Ver últimas 20 migraciones
curl -X GET "http://localhost:5086/api/migraciones?limite=20" \
  -H "Authorization: Bearer $TOKEN" | jq
```

---

## 📊 Comparación con __ef_migrations_history

| Característica | __ef_migrations_history | migraciones_log |
|----------------|------------------------|-----------------|
| **Propósito** | Control de versiones de EF | Auditoría completa |
| **Información** | MigrationId, ProductVersion | +Usuario, Fecha, Duración, Estado, Notas |
| **Creado por** | Entity Framework automáticamente | Sistema de auditoría |
| **Acceso** | Solo query SQL | API REST + Query SQL |
| **Descripción** | ❌ No | ✅ Sí |
| **Usuario** | ❌ No | ✅ Sí |
| **Duración** | ❌ No | ✅ Sí |
| **Notas/Errores** | ❌ No | ✅ Sí |
| **Estado** | ❌ No | ✅ Success/Failed |

---

## 🎯 Beneficios

### ✅ Trazabilidad Completa
- Saber exactamente cuándo se aplicó cada cambio de esquema
- Quién autorizó y ejecutó la migración
- Cuánto tiempo tomó

### ✅ Debugging Facilitado
- Correlacionar problemas con cambios de esquema
- Ver qué migraciones se aplicaron en un rango de fechas
- Identificar migraciones problemáticas

### ✅ Auditoría y Cumplimiento
- Registro completo de cambios estructurales
- Útil para auditorías de seguridad
- Cumplimiento normativo (SOX, GDPR, etc.)

### ✅ Documentación Automática
- Historial legible de evolución del esquema
- Descripción en lenguaje natural de cada cambio
- Facilita la comprensión del sistema

### ✅ Gestión de Entornos
- Saber qué migraciones están en cada ambiente
- Facilitar la sincronización entre dev/test/prod
- Detectar discrepancias

---

## 🔧 Integración con CI/CD

### Ejemplo: GitHub Actions

```yaml
- name: Apply Database Migrations
  run: |
    dotnet ef database update --project POS.Infrastructure --startup-project POS.Api

- name: Register Migration in Log
  env:
    API_TOKEN: ${{ secrets.API_TOKEN }}
  run: |
    LAST_MIGRATION=$(dotnet ef migrations list --project POS.Infrastructure --startup-project POS.Api --no-connect | tail -1)

    curl -X POST "https://api.sincopos.com/api/migraciones/registrar" \
      -H "Authorization: Bearer $API_TOKEN" \
      -H "Content-Type: application/json" \
      -d "{
        \"migracionId\": \"$LAST_MIGRATION\",
        \"descripcion\": \"Deployed from GitHub Actions\",
        \"productVersion\": \"9.0.1\",
        \"aplicadoPor\": \"github-actions\"
      }"
```

---

## 📈 Casos de Uso

### 1. Investigar un problema de producción
```bash
# ¿Qué cambios de esquema se hicieron ayer?
curl "http://localhost:5086/api/migraciones" | jq '.[] | select(.fechaAplicacion >= "2026-03-01")'
```

### 2. Comparar ambientes
```sql
-- ¿Qué migraciones tiene producción que no tiene staging?
SELECT ml.migracion_id, ml.descripcion, ml.fecha_aplicacion
FROM public.migraciones_log ml
WHERE ml.migracion_id NOT IN (
  SELECT migracion_id FROM staging.migraciones_log
)
ORDER BY ml.fecha_aplicacion DESC;
```

### 3. Reporte de cambios mensuales
```sql
SELECT
  DATE_TRUNC('month', fecha_aplicacion) AS mes,
  COUNT(*) AS total_migraciones,
  STRING_AGG(descripcion, ', ') AS cambios
FROM public.migraciones_log
WHERE fecha_aplicacion >= NOW() - INTERVAL '6 months'
GROUP BY mes
ORDER BY mes DESC;
```

---

## 🚀 Próximos Pasos

### Recomendaciones:

1. **Ejecutar sincronización inicial**
   ```bash
   curl -X POST "http://localhost:5086/api/migraciones/sincronizar" \
     -H "Authorization: Bearer $TOKEN"
   ```

2. **Integrar con CI/CD**
   - Automatizar el registro de migraciones en pipelines de despliegue

3. **Dashboard de migraciones** (frontend)
   - Página en el admin para visualizar el historial
   - Gráficos de migraciones por mes
   - Timeline de evolución del esquema

4. **Notificaciones**
   - Email/Slack cuando se aplica una migración en producción
   - Alertas si una migración falla

5. **Backup antes de migración**
   - Script que automáticamente crea backup antes de aplicar
   - Registrar el backup en el log

---

## 📚 Archivos Implementados

### Creados:
- `POS.Infrastructure/Data/Entities/MigracionLog.cs`
- `POS.Infrastructure/Data/Configurations/MigracionLogConfiguration.cs`
- `POS.Infrastructure/Services/MigracionLogService.cs`
- `POS.Api/Controllers/MigracionesController.cs`
- `LOG_MIGRACIONES_IMPLEMENTADO.md` (este archivo)

### Modificados:
- `POS.Infrastructure/Data/AppDbContext.cs` - Agregado DbSet<MigracionLog>
- `POS.Api/Program.cs` - Registrado MigracionLogService

### Migración:
- `POS.Infrastructure/Migrations/20260302211137_AgregarTablaMigracionesLog.cs`

---

## ✅ Checklist de Implementación

- [x] Crear entidad MigracionLog
- [x] Configurar entidad en EF Core
- [x] Agregar DbSet al AppDbContext
- [x] Crear MigracionLogService
- [x] Crear MigracionesController con endpoints
- [x] Registrar servicio en DI container
- [x] Crear migración de base de datos
- [x] Aplicar migración
- [x] Compilación exitosa
- [x] Documentación completa
- [ ] Ejecutar sincronización inicial (pendiente)
- [ ] Crear tests de integración (pendiente)
- [ ] Integrar con CI/CD (pendiente)
- [ ] Dashboard frontend (pendiente - siguiente sprint)

---

## 🧪 Testing

### Test 1: Sincronizar migraciones históricas

```bash
# 1. Obtener token
TOKEN=$(curl -X POST http://localhost:8080/realms/sincopos/protocol/openid-connect/token \
  -d "client_id=pos-api" \
  -d "username=admin@sincopos.com" \
  -d "password=admin123" \
  -d "grant_type=password" | jq -r '.access_token')

# 2. Sincronizar
curl -X POST "http://localhost:5086/api/migraciones/sincronizar" \
  -H "Authorization: Bearer $TOKEN"

# 3. Verificar en BD
psql -U postgres -d SincoPos -c "SELECT migracion_id, descripcion, fecha_aplicacion FROM public.migraciones_log ORDER BY fecha_aplicacion DESC LIMIT 10;"

# Resultado esperado: Todas las migraciones de __ef_migrations_history ahora en migraciones_log
```

### Test 2: Consultar historial vía API

```bash
# Consultar últimas 5 migraciones
curl -X GET "http://localhost:5086/api/migraciones?limite=5" \
  -H "Authorization: Bearer $TOKEN" | jq

# Resultado esperado:
# [
#   {
#     "id": 1,
#     "migracionId": "20260302211137_AgregarTablaMigracionesLog",
#     "descripcion": "Agregar Tabla Migraciones Log",
#     ...
#   }
# ]
```

### Test 3: Registrar migración manual

```bash
curl -X POST "http://localhost:5086/api/migraciones/registrar" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "migracionId": "20260302_TestManual",
    "descripcion": "Test de registro manual",
    "productVersion": "9.0.1",
    "duracionMs": 100,
    "notas": "Prueba de funcionalidad"
  }'

# Verificar
psql -U postgres -d SincoPos -c "SELECT * FROM public.migraciones_log WHERE migracion_id = '20260302_TestManual';"
```

---

**Implementado por:** Claude Code
**Revisado:** Pendiente
**Estado:** ✅ Listo para uso

---

## 💡 Notas Adicionales

- El sistema NO usa Event Sourcing, es un log simple en una tabla tradicional
- La tabla `__ef_migrations_history` sigue siendo la fuente de verdad para EF
- El log de migraciones es COMPLEMENTARIO, no reemplaza __ef_migrations_history
- Los registros son solo de lectura/inserción, no se deben actualizar ni eliminar
- La sincronización es idempotente, se puede ejecutar múltiples veces sin problemas
