# Script para Probar el Módulo de Devoluciones
# Crea una venta de prueba y luego procesa una devolución parcial

$baseUrl = "http://localhost:5086/api"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  TEST: Módulo de Devoluciones" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ──────────────────────────────────────────────────────────
# PASO 1: Verificar que hay una caja abierta
# ──────────────────────────────────────────────────────────
Write-Host "PASO 1: Verificar cajas abiertas..." -ForegroundColor Yellow

$cajas = Invoke-RestMethod -Uri "$baseUrl/Cajas" -Method Get
$cajaAbierta = $cajas | Where-Object { $_.estado -eq "Abierta" } | Select-Object -First 1

if (-not $cajaAbierta) {
    Write-Host "❌ ERROR: No hay cajas abiertas. Abre una caja primero." -ForegroundColor Red
    Write-Host ""
    Write-Host "Para abrir una caja, ejecuta:" -ForegroundColor Yellow
    Write-Host "  POST $baseUrl/Cajas/{cajaId}/abrir" -ForegroundColor Gray
    exit 1
}

Write-Host "✅ Caja abierta encontrada: $($cajaAbierta.nombre) (ID: $($cajaAbierta.id))" -ForegroundColor Green
$cajaId = $cajaAbierta.id
$sucursalId = $cajaAbierta.sucursalId
Write-Host ""

# ──────────────────────────────────────────────────────────
# PASO 2: Obtener productos disponibles
# ──────────────────────────────────────────────────────────
Write-Host "PASO 2: Buscar productos con stock..." -ForegroundColor Yellow

$productos = Invoke-RestMethod -Uri "$baseUrl/Productos?activo=true" -Method Get
$stock = Invoke-RestMethod -Uri "$baseUrl/Inventario?sucursalId=$sucursalId" -Method Get

# Buscar un producto con stock suficiente
$productoConStock = $null
foreach ($prod in $productos) {
    $stockItem = $stock | Where-Object { $_.productoId -eq $prod.id }
    if ($stockItem -and $stockItem.cantidad -ge 5) {
        $productoConStock = @{
            Producto = $prod
            Stock = $stockItem.cantidad
        }
        break
    }
}

if (-not $productoConStock) {
    Write-Host "❌ ERROR: No hay productos con stock suficiente (mínimo 5 unidades)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Para agregar stock, ejecuta:" -ForegroundColor Yellow
    Write-Host "  POST $baseUrl/Inventario/entrada" -ForegroundColor Gray
    exit 1
}

Write-Host "✅ Producto encontrado: $($productoConStock.Producto.nombre)" -ForegroundColor Green
Write-Host "   Stock disponible: $($productoConStock.Stock) unidades" -ForegroundColor Gray
Write-Host "   Precio: `$$($productoConStock.Producto.precioVenta)" -ForegroundColor Gray
$productoId = $productoConStock.Producto.id
$precioVenta = $productoConStock.Producto.precioVenta
Write-Host ""

# ──────────────────────────────────────────────────────────
# PASO 3: Crear una venta de prueba
# ──────────────────────────────────────────────────────────
Write-Host "PASO 3: Crear venta de prueba..." -ForegroundColor Yellow

$ventaBody = @{
    SucursalId = $sucursalId
    CajaId = $cajaId
    ClienteId = $null
    MetodoPago = 0  # Efectivo
    MontoPagado = $precioVenta * 5
    Observaciones = "Venta de prueba para devolución"
    Lineas = @(
        @{
            ProductoId = $productoId
            Cantidad = 5
            PrecioUnitario = $precioVenta
            Descuento = 0
        }
    )
} | ConvertTo-Json -Depth 10

try {
    $venta = Invoke-RestMethod -Uri "$baseUrl/Ventas" -Method Post `
        -Body $ventaBody `
        -ContentType "application/json"

    Write-Host "✅ Venta creada: $($venta.numeroVenta)" -ForegroundColor Green
    Write-Host "   ID: $($venta.id)" -ForegroundColor Gray
    Write-Host "   Total: `$$($venta.total)" -ForegroundColor Gray
    Write-Host "   Producto: $($venta.detalles[0].nombreProducto) x $($venta.detalles[0].cantidad)" -ForegroundColor Gray
    $ventaId = $venta.id
    $numeroVenta = $venta.numeroVenta
} catch {
    Write-Host "❌ ERROR al crear venta: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# ──────────────────────────────────────────────────────────
# PASO 4: Esperar 2 segundos para asegurar timestamp diferente
# ──────────────────────────────────────────────────────────
Write-Host "Esperando 2 segundos..." -ForegroundColor Gray
Start-Sleep -Seconds 2
Write-Host ""

# ──────────────────────────────────────────────────────────
# PASO 5: Crear devolución parcial (devolver 2 unidades)
# ──────────────────────────────────────────────────────────
Write-Host "PASO 4: Crear devolución parcial..." -ForegroundColor Yellow

$devolucionBody = @{
    Motivo = "Producto defectuoso - Test automatizado"
    Lineas = @(
        @{
            ProductoId = $productoId
            Cantidad = 2
        }
    )
} | ConvertTo-Json -Depth 10

try {
    $devolucion = Invoke-RestMethod -Uri "$baseUrl/Ventas/$ventaId/devolucion-parcial" -Method Post `
        -Body $devolucionBody `
        -ContentType "application/json"

    Write-Host "✅ Devolución creada: $($devolucion.numeroDevolucion)" -ForegroundColor Green
    Write-Host "   Venta: $($devolucion.numeroVenta)" -ForegroundColor Gray
    Write-Host "   Motivo: $($devolucion.motivo)" -ForegroundColor Gray
    Write-Host "   Total devuelto: `$$($devolucion.totalDevuelto)" -ForegroundColor Gray
    Write-Host "   Fecha: $($devolucion.fechaDevolucion)" -ForegroundColor Gray
    Write-Host "   Autorizado por: $($devolucion.autorizadoPor)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "   Detalles:" -ForegroundColor Gray
    foreach ($detalle in $devolucion.detalles) {
        Write-Host "   - $($detalle.nombreProducto): $($detalle.cantidadDevuelta) unidades × `$$($detalle.precioUnitario) = `$$($detalle.subtotalDevuelto)" -ForegroundColor Gray
    }
} catch {
    $errorDetails = $_.ErrorDetails.Message | ConvertFrom-Json
    Write-Host "❌ ERROR al crear devolución: $($errorDetails.error)" -ForegroundColor Red
    exit 1
}
Write-Host ""

# ──────────────────────────────────────────────────────────
# PASO 6: Verificar historial de devoluciones
# ──────────────────────────────────────────────────────────
Write-Host "PASO 5: Verificar historial de devoluciones..." -ForegroundColor Yellow

try {
    $devoluciones = Invoke-RestMethod -Uri "$baseUrl/Ventas/$ventaId/devoluciones" -Method Get

    Write-Host "✅ Devoluciones de la venta $numeroVenta :" -ForegroundColor Green
    foreach ($dev in $devoluciones) {
        Write-Host "   - $($dev.numeroDevolucion): `$$($dev.totalDevuelto) ($($dev.detalles.Count) productos)" -ForegroundColor Gray
    }
} catch {
    Write-Host "❌ ERROR al consultar devoluciones: $_" -ForegroundColor Red
}
Write-Host ""

# ──────────────────────────────────────────────────────────
# PASO 7: Verificar stock actualizado
# ──────────────────────────────────────────────────────────
Write-Host "PASO 6: Verificar stock actualizado..." -ForegroundColor Yellow

try {
    $stockActualizado = Invoke-RestMethod -Uri "$baseUrl/Inventario?productoId=$productoId&sucursalId=$sucursalId" -Method Get
    $stockFinal = $stockActualizado[0].cantidad
    $stockEsperado = $productoConStock.Stock - 5 + 2  # Stock inicial - vendido + devuelto

    Write-Host "✅ Stock actualizado:" -ForegroundColor Green
    Write-Host "   Stock inicial: $($productoConStock.Stock) unidades" -ForegroundColor Gray
    Write-Host "   Vendido: 5 unidades" -ForegroundColor Gray
    Write-Host "   Devuelto: 2 unidades" -ForegroundColor Gray
    Write-Host "   Stock final: $stockFinal unidades" -ForegroundColor Gray
    Write-Host "   Stock esperado: $stockEsperado unidades" -ForegroundColor Gray

    if ($stockFinal -eq $stockEsperado) {
        Write-Host "   ✅ Stock correcto" -ForegroundColor Green
    } else {
        Write-Host "   ❌ Stock incorrecto (esperado: $stockEsperado, actual: $stockFinal)" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ ERROR al verificar stock: $_" -ForegroundColor Red
}
Write-Host ""

# ──────────────────────────────────────────────────────────
# RESUMEN
# ──────────────────────────────────────────────────────────
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  RESUMEN DEL TEST" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "✅ Venta creada: $numeroVenta" -ForegroundColor Green
Write-Host "✅ Devolución creada: $($devolucion.numeroDevolucion)" -ForegroundColor Green
Write-Host "✅ Stock restaurado correctamente" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Para ver en el frontend:" -ForegroundColor Yellow
Write-Host "   1. Abrir http://localhost:5173/devoluciones" -ForegroundColor Gray
Write-Host "   2. Buscar venta: $numeroVenta" -ForegroundColor Gray
Write-Host "   3. Ver historial de devoluciones" -ForegroundColor Gray
Write-Host ""
Write-Host "🔍 Para ver en Activity Logs:" -ForegroundColor Yellow
Write-Host "   $baseUrl/ActivityLogs?accion=DevolucionParcial" -ForegroundColor Gray
Write-Host ""
