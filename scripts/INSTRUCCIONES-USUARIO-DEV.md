# Configuración Completa para Desarrollo

## ⚠️ IMPORTANTE
Este script crea automáticamente todo lo necesario:
- ✅ Sucursal (si no existe)
- ✅ Usuario de desarrollo
- ✅ Caja (si no existe)

## 🚀 Ejecución (Recomendado - pgAdmin)

1. Abrir **pgAdmin**
2. Conectarse a la base de datos `sincopos`
3. Abrir **Query Tool** (Herramientas → Query Tool o F5)
4. Abrir el archivo: `scripts/setup-desarrollo-completo.sql`
   - O copiar todo el contenido del archivo
5. Hacer clic en **Execute** (F5 o botón ▶️)
6. Verificar los resultados

## 📋 Resultado Esperado

Verás mensajes como:
```
NOTICE: Usando sucursal existente con ID: 1
  (o)
NOTICE: Sucursal creada con ID: 1

NOTICE: Usuario de desarrollo creado con ID: X
NOTICE:   Email: dev@sincopos.com
NOTICE:   Rol: admin
NOTICE:   Sucursal ID: 1

NOTICE: Caja creada con ID: Y
  (o)
NOTICE: Ya existe una caja con ID: Y
```

Y tres tablas de verificación mostrando:

**SUCURSALES:**
```
tipo       | id | nombre              | ciudad | activa
-----------+----+---------------------+--------+-------
SUCURSALES | 1  | Sucursal Principal  | Bogotá | t
```

**USUARIOS:**
```
tipo     | id | external_id | email             | nombre_completo    | rol   | sucursal_default_id | activo
---------+----+-------------+-------------------+--------------------+-------+---------------------+--------
USUARIOS | X  | dev-user-1  | dev@sincopos.com  | Usuario Desarrollo | admin | 1                   | t
```

**CAJAS:**
```
tipo  | id | nombre          | sucursal           | estado  | activo
------+----+-----------------+--------------------+---------+--------
CAJAS | Y  | Caja Principal  | Sucursal Principal | Cerrada | t
```

## ✅ Después de Ejecutar

1. **Recargar el frontend** (F5 en el navegador)
2. Los errores 400/404 deberían desaparecer
3. Al entrar a **Punto de Venta**:
   - Se mostrará el diálogo de selección de caja
   - Aparecerá la sucursal y caja creadas
   - Deberás **abrir la caja** desde el módulo de Cajas primero

## 🔧 Problemas Comunes

### Error: "relation usuarios does not exist"
Las migraciones no están aplicadas. Ejecutar:
```bash
cd C:\Users\jaime.forero\RiderProjects\SincoPos
dotnet ef database update --project POS.Infrastructure --startup-project POS.Api
```

### Error: "no existe la columna «id»"
PostgreSQL es sensible a mayúsculas/minúsculas. Este script usa la nomenclatura correcta:
- Tablas: `sucursales`, `usuarios`, `cajas` (minúsculas)
- ID: `"Id"` (PascalCase con comillas)
- Otras columnas: `nombre`, `email`, `sucursal_id` (snake_case)

### Error de conexión a PostgreSQL
Verificar que:
- PostgreSQL esté corriendo
- La base de datos `sincopos` exista
- Las credenciales sean correctas

### La caja aparece pero no se puede seleccionar
La caja está cerrada. Ir al módulo **Gestión de Cajas** y abrirla primero.
