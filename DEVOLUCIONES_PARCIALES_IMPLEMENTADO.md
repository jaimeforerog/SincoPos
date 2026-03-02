# Devoluciones Parciales de Venta - Implementación Completa

## 📋 Resumen

Se ha implementado exitosamente el sistema de **Devoluciones Parciales** para el POS, permitiendo a los clientes devolver solo algunos productos de una venta sin necesidad de anular toda la transacción.

---

## ✅ Características Implementadas

### 1. **Modelo de Datos**

**Nuevas entidades creadas:**

- `DevolucionVenta`: Registro maestro de devolución
  - Número de devolución único (DEV-000001)
  - Motivo obligatorio
  - Total devuelto
  - Fecha de devolución
  - Usuario que autorizó
  - Campos de auditoría

- `DetalleDevolucion`: Detalle de productos devueltos
  - Cantidad devuelta
  - Precio unitario (del momento de la venta)
  - Costo unitario (del momento de la venta)
  - Subtotal devuelto

**Migración aplicada:** `AgregarDevolucionesParciales`

### 2. **Endpoints REST API**

#### POST `/api/Ventas/{ventaId}/devolucion-parcial`
Crea una devolución parcial de productos de una venta.

**Autorización:** Requiere policy `Supervisor`

**Request Body:**
```json
{
  "motivo": "Producto defectuoso",
  "lineas": [
    {
      "productoId": "guid-del-producto",
      "cantidad": 3
    }
  ]
}
```

**Response:**
```json
{
  "id": 1,
  "ventaId": 123,
  "numeroVenta": "V-000123",
  "numeroDevolucion": "DEV-000001",
  "motivo": "Producto defectuoso",
  "totalDevuelto": 3000.00,
  "fechaDevolucion": "2026-03-01T12:00:00Z",
  "autorizadoPor": "usuario@example.com",
  "detalles": [
    {
      "id": 1,
      "productoId": "guid-del-producto",
      "nombreProducto": "Producto X",
      "cantidadDevuelta": 3,
      "precioUnitario": 1000.00,
      "costoUnitario": 500.00,
      "subtotalDevuelto": 3000.00
    }
  ]
}
```

#### GET `/api/Ventas/{ventaId}/devoluciones`
Obtiene todas las devoluciones de una venta específica.

**Response:** Lista de `DevolucionVentaDto`

#### GET `/api/Ventas/devoluciones/{devolucionId}`
Obtiene el detalle de una devolución específica.

**Response:** `DevolucionVentaDto`

### 3. **Validaciones de Negocio**

El sistema valida:

✅ **Estado de venta:** Solo ventas completadas pueden tener devoluciones
✅ **Límite de tiempo:** 30 días desde la fecha de venta
✅ **Producto en venta:** El producto debe estar en la venta original
✅ **Cantidad disponible:** No se puede devolver más de lo vendido
✅ **Devoluciones acumuladas:** Suma devoluciones anteriores del mismo producto
✅ **Motivo obligatorio:** Máximo 500 caracteres
✅ **Mínimo una línea:** Al menos un producto a devolver

### 4. **Procesamiento de Inventario**

**Event Sourcing:**
- Registra evento `EntradaCompraRegistrada` con referencia a la devolución
- Mantiene trazabilidad completa en Marten

**Costeo:**
- Restaura con el **costo original** de la venta (no el costo actual)
- Crea nuevo lote de entrada
- Actualiza stock en EF Core
- Actualiza costo promedio/FIFO según método de costeo

**Caja:**
- Reduce `MontoActual` por el total devuelto
- Mantiene consistencia contable

### 5. **Auditoría**

**Activity Log:**
- Acción: `DevolucionParcial`
- Tipo: `Venta`
- Registra todos los detalles de la devolución
- Usuario que autorizó
- Productos devueltos con cantidades

**Campos automáticos:**
- `CreadoPor`, `FechaCreacion` (automático en AppDbContext)
- IP, User Agent (si disponible en ActivityLog)

---

## 🧪 Tests Implementados

Se crearon **9 tests de integración** en `DevolucionesTests.cs`:

### Tests de Funcionalidad

1. ✅ `DevolucionParcial_Simple_RestaurarStockCorrectamente`
   - Vende 10 unidades, devuelve 3
   - Verifica stock restaurado (40 + 3 = 43)
   - Verifica ajuste de caja

2. ✅ `DevolucionParcial_MultiplesDevoluciones_NoExcederCantidad`
   - Vende 10 unidades
   - Primera devolución: 4 unidades (OK)
   - Segunda devolución: 5 unidades (OK, total 9)
   - Tercera devolución: 2 unidades (FALLA, solo queda 1)

### Tests de Validación

3. ✅ `DevolucionParcial_VentaAnulada_Rechaza`
   - Intenta devolver producto de venta anulada
   - Debe retornar 400 BadRequest

4. ✅ `DevolucionParcial_ProductoNoEnVenta_Rechaza`
   - Intenta devolver producto que no está en la venta
   - Debe retornar 400 BadRequest

5. ✅ `DevolucionParcial_CantidadExcedida_Rechaza`
   - Vende 5 unidades, intenta devolver 6
   - Debe retornar 400 BadRequest

6. ✅ `DevolucionParcial_MotivoVacio_Rechaza`
   - Intenta crear devolución sin motivo
   - Debe retornar 400 BadRequest

### Tests de Consultas

7. ✅ `ObtenerDevolucionesPorVenta_DevuelveListaCompleta`
   - Crea 2 devoluciones de la misma venta
   - Verifica que el endpoint devuelva ambas

8. ✅ `ObtenerDevolucion_PorId_DevuelveDetalle`
   - Consulta devolución por ID
   - Verifica detalles completos

### Tests de Costeo

9. ✅ `DevolucionParcial_UsaCostoOriginalVenta`
   - Vende con costo 800
   - Cambia costo de inventario a 1200
   - Verifica que devolución use costo original (800)

**Resultado:** 🎉 **9/9 tests pasaron correctamente**

---

## 🔍 Escenarios de Uso

### Escenario 1: Devolución Simple
```
1. Cliente compra 5 productos A a $1000 cada uno
2. Cliente devuelve 2 productos defectuosos
3. Sistema:
   - Restaura 2 unidades al inventario (con costo original)
   - Reembolsa $2000 de la caja
   - Registra devolución DEV-000001
   - Crea Activity Log completo
```

### Escenario 2: Devoluciones Múltiples
```
1. Cliente compra 10 productos B
2. Primera devolución: 3 unidades (OK)
3. Segunda devolución: 4 unidades (OK, total 7)
4. Tercera devolución: 4 unidades (FALLA, solo quedan 3)
```

### Escenario 3: Venta con Varios Productos
```
1. Cliente compra:
   - 5 unidades de Producto A
   - 3 unidades de Producto B
   - 2 unidades de Producto C
2. Devuelve solo:
   - 2 unidades de Producto A
   - 1 unidad de Producto C
3. Sistema procesa devolución parcial manteniendo intacta la venta original
```

---

## 📊 Arquitectura de la Solución

### Decisiones Técnicas

**1. Nuevas Tablas vs Solo Activity Log**
- ✅ **Elegido:** Nuevas tablas (DevolucionVenta, DetalleDevolucion)
- **Razón:** Mejor queryabilidad, reportes, validaciones
- **Alternativa rechazada:** Solo Activity Log (dificulta consultas)

**2. Evento de Inventario**
- ✅ **Elegido:** Reutilizar `EntradaCompraRegistrada` con referencia
- **Razón:** Projection ya existe, funcionalidad probada
- **Alternativa rechazada:** Nuevo evento (complejidad innecesaria)

**3. Costo de Restauración**
- ✅ **Elegido:** Costo original de la venta (DetalleVenta.CostoUnitario)
- **Razón:** Correcto contablemente, previene manipulación
- **Alternativa rechazada:** Costo actual (incorrecto)

**4. Múltiples Devoluciones**
- ✅ **Elegido:** Permitir múltiples hasta agotar cantidad
- **Razón:** Flexibilidad para el negocio
- **Implementación:** Suma devoluciones anteriores en validación

**5. Límite de Tiempo**
- ✅ **Elegido:** 30 días (hardcoded)
- **Futuro:** Configurable por sucursal

---

## 🗂️ Archivos Modificados/Creados

### Creados
- ✅ `POS.Infrastructure/Data/Migrations/[timestamp]_AgregarDevolucionesParciales.cs`
- ✅ `tests/POS.IntegrationTests/DevolucionesTests.cs`

### Modificados
- ✅ `POS.Infrastructure/Data/Entities/Venta.cs` - Entidades DevolucionVenta y DetalleDevolucion
- ✅ `POS.Infrastructure/Data/Configurations/VentaConfiguration.cs` - Configuraciones EF
- ✅ `POS.Infrastructure/Data/AppDbContext.cs` - DbSets
- ✅ `POS.Application/DTOs/VentaDTOs.cs` - DTOs de request/response
- ✅ `POS.Application/Validators/VentaValidators.cs` - Validadores FluentValidation
- ✅ `POS.Api/Controllers/VentasController.cs` - 3 nuevos endpoints

---

## 🚀 Próximos Pasos (Mejoras Futuras)

1. **Configuración por Sucursal**
   - Límite de tiempo configurable
   - Monto máximo de devolución sin autorización adicional

2. **Notas de Crédito**
   - Opción de emitir nota de crédito en lugar de reembolso directo
   - Aplicable a futuras compras

3. **Tarifas de Reposición (Restocking Fees)**
   - Descuento porcentual en devoluciones
   - Configurable por categoría de producto

4. **Dashboard de Analíticas**
   - Productos más devueltos
   - Motivos de devolución más frecuentes
   - Tendencias por sucursal/periodo

5. **Integración Contable**
   - Exportación de movimientos contables
   - Conciliación automática

6. **Autorización Multi-nivel**
   - Supervisor para montos pequeños
   - Gerente para montos grandes
   - Notificaciones automáticas

---

## 📝 Notas de Implementación

### Reutilización de Código
El sistema aprovecha patrones existentes:
- Event Sourcing de inventario (Marten)
- CosteoService para lotes y costos
- Activity Log para auditoría
- Validadores FluentValidation
- Políticas de autorización

### Trade-offs
- **Complejidad:** +2 tablas, +3 endpoints, +1 migración
- **Beneficio:** Trazabilidad completa, reportes fáciles, validaciones robustas
- **Mantenibilidad:** Alta (sigue patrones del sistema)

### Performance
- Consultas optimizadas con Include()
- Índices en campos clave (numero_devolucion, fecha_devolucion, venta_id)
- Transacciones atómicas (Marten + EF Core)

---

## ✅ Checklist de Implementación

- [x] Entidades DevolucionVenta y DetalleDevolucion
- [x] Configuraciones EF Core
- [x] Migración aplicada a BD
- [x] DTOs de request/response
- [x] Validadores FluentValidation
- [x] Endpoint POST devolucion-parcial
- [x] Endpoint GET devoluciones por venta
- [x] Endpoint GET devolución por ID
- [x] Event Sourcing de inventario
- [x] Ajuste de stock con costeo correcto
- [x] Ajuste de caja
- [x] Activity Log completo
- [x] 9 tests de integración (todos pasando)
- [x] Documentación completa

---

## 🎯 Conclusión

El sistema de **Devoluciones Parciales** está completamente implementado, probado y documentado. Cumple con todos los requerimientos de negocio:

✅ Integridad del histórico (venta original intacta)
✅ Restauración correcta de inventario vía Event Sourcing
✅ Validaciones exhaustivas de reglas de negocio
✅ Trazabilidad completa (Event Sourcing + Activity Log)
✅ Extensible para futuras mejoras
✅ Tests de integración completos

**Tiempo de implementación:** ~3 horas
**Líneas de código:** ~800 líneas
**Tests:** 9/9 pasando ✅
**Cobertura:** Funcionalidad completa

---

**Fecha de implementación:** 2026-03-01
**Versión:** 1.0.0
**Estado:** ✅ Producción Ready
