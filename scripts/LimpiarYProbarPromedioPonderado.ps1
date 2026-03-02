# Script completo: Limpiar DB y probar Promedio Ponderado
# IMPORTANTE: Ejecutar ANTES de correr este script:
# 1. Asegurarse que la API este corriendo (cd POS.Api && dotnet run)
# 2. Asegurarse que PostgreSQL este corriendo
# 3. Asegurarse que metodo_costeo = 0 en sucursal ID 1

$psql = "C:\Program Files\PostgreSQL\16\bin\psql.exe"
$baseUrl = "http://localhost:5086"
$productoId = "11111111-1111-1111-1111-111111111111"
$sucursalId = 1

# Set PostgreSQL password
$env:PGPASSWORD = "postgrade"

Write-Host "`n========================================"  -ForegroundColor Cyan
Write-Host "LIMPIEZA DE BASE DE DATOS" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Limpiar tablas
Write-Host "Limpiando tablas..." -ForegroundColor Yellow

& $psql -h localhost -U postgres -d sincopos -c "DELETE FROM public.detalle_ventas;"
& $psql -h localhost -U postgres -d sincopos -c "DELETE FROM public.ventas;"
& $psql -h localhost -U postgres -d sincopos -c "DELETE FROM public.lotes_inventario;"
& $psql -h localhost -U postgres -d sincopos -c "DELETE FROM public.stock;"
& $psql -h localhost -U postgres -d sincopos -c "DELETE FROM events.mt_events;"
& $psql -h localhost -U postgres -d sincopos -c "DELETE FROM events.mt_streams;"

Write-Host "Limpieza completa.`n" -ForegroundColor Green

# Verificar metodo de costeo
Write-Host "Verificando metodo de costeo..." -ForegroundColor Yellow
$metodoResult = & $psql -h localhost -U postgres -d sincopos -t -A -c 'SELECT metodo_costeo FROM public.sucursales WHERE "Id" = 1;'
$metodo = if ($metodoResult) { $metodoResult.Trim() } else { "" }
Write-Host "Metodo actual: $metodo (0=PromedioPonderado)`n" -ForegroundColor White

if ($metodo -ne "0") {
    Write-Host "[ADVERTENCIA] Cambiando metodo a Promedio Ponderado..." -ForegroundColor Red
    & $psql -h localhost -U postgres -d sincopos -c 'UPDATE public.sucursales SET metodo_costeo = 0 WHERE "Id" = 1;'
    Write-Host "Metodo actualizado.`n" -ForegroundColor Green
}

# Esperar para que la API procese
Start-Sleep -Seconds 3

Write-Host "`n========================================"  -ForegroundColor Cyan
Write-Host "TEST PROMEDIO PONDERADO" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Crear 3 lotes con diferentes costos
Write-Host "PASO 1: Crear Lote 1 - 100 unidades @ `$20.00" -ForegroundColor Yellow
$lote1 = @{
    productoId = $productoId
    sucursalId = $sucursalId
    cantidad = 100
    costoUnitario = 20.00
    porcentajeImpuesto = 0
    montoImpuestoUnitario = 0
    referencia = "LOTE-PP-001"
    terceroId = 1
} | ConvertTo-Json

$response1 = Invoke-RestMethod -Uri "$baseUrl/api/Inventario/entrada" -Method Post -Body $lote1 -ContentType "application/json"
Write-Host "  OK - Lote creado" -ForegroundColor Green
Write-Host "  Costo promedio esperado: `$20.00" -ForegroundColor White
Write-Host "  Formula: 100 * 20 / 100 = `$20.00`n" -ForegroundColor Gray

Start-Sleep -Seconds 2

# Verificar stock despues del lote 1
$stock1 = Invoke-RestMethod -Uri "$baseUrl/api/Inventario?productoId=$productoId&sucursalId=$sucursalId"
Write-Host "  Stock actual: $($stock1.cantidad) unidades" -ForegroundColor White
Write-Host "  Costo promedio actual: `$$([math]::Round($stock1.costoPromedio, 2))" -ForegroundColor White

if ([math]::Round($stock1.costoPromedio, 2) -eq 20.00) {
    Write-Host "  [OK] Costo promedio correcto`n" -ForegroundColor Green
} else {
    Write-Host "  [ERROR] Esperado `$20.00, obtenido `$$([math]::Round($stock1.costoPromedio, 2))`n" -ForegroundColor Red
    Write-Host "  NOTA: Si hay error aqui, hay inventario previo. Revisar limpieza.`n" -ForegroundColor Yellow
}

# Lote 2
Write-Host "PASO 2: Crear Lote 2 - 50 unidades @ `$30.00" -ForegroundColor Yellow
$lote2 = @{
    productoId = $productoId
    sucursalId = $sucursalId
    cantidad = 50
    costoUnitario = 30.00
    porcentajeImpuesto = 0
    montoImpuestoUnitario = 0
    referencia = "LOTE-PP-002"
    terceroId = 1
} | ConvertTo-Json

$response2 = Invoke-RestMethod -Uri "$baseUrl/api/Inventario/entrada" -Method Post -Body $lote2 -ContentType "application/json"
Write-Host "  OK - Lote creado" -ForegroundColor Green
Write-Host "  Costo promedio esperado: `$23.33" -ForegroundColor White
Write-Host "  Formula: (100 * 20 + 50 * 30) / 150 = 3500 / 150 = `$23.33`n" -ForegroundColor Gray

Start-Sleep -Seconds 2

# Verificar stock despues del lote 2
$stock2 = Invoke-RestMethod -Uri "$baseUrl/api/Inventario?productoId=$productoId&sucursalId=$sucursalId"
$esperado2 = 23.33
Write-Host "  Stock actual: $($stock2.cantidad) unidades" -ForegroundColor White
Write-Host "  Costo promedio actual: `$$([math]::Round($stock2.costoPromedio, 2))" -ForegroundColor White

if ([math]::Abs([math]::Round($stock2.costoPromedio, 2) - $esperado2) -lt 0.01) {
    Write-Host "  [OK] Costo promedio correcto`n" -ForegroundColor Green
} else {
    Write-Host "  [ERROR] Esperado `$$esperado2, obtenido `$$([math]::Round($stock2.costoPromedio, 2))`n" -ForegroundColor Red
}

# Lote 3
Write-Host "PASO 3: Crear Lote 3 - 50 unidades @ `$40.00" -ForegroundColor Yellow
$lote3 = @{
    productoId = $productoId
    sucursalId = $sucursalId
    cantidad = 50
    costoUnitario = 40.00
    porcentajeImpuesto = 0
    montoImpuestoUnitario = 0
    referencia = "LOTE-PP-003"
    terceroId = 1
} | ConvertTo-Json

$response3 = Invoke-RestMethod -Uri "$baseUrl/api/Inventario/entrada" -Method Post -Body $lote3 -ContentType "application/json"
Write-Host "  OK - Lote creado" -ForegroundColor Green
Write-Host "  Costo promedio esperado: `$27.50" -ForegroundColor White
Write-Host "  Formula: (150 * 23.33 + 50 * 40) / 200 = 5500 / 200 = `$27.50`n" -ForegroundColor Gray

Start-Sleep -Seconds 2

# Verificar stock despues del lote 3
$stock3 = Invoke-RestMethod -Uri "$baseUrl/api/Inventario?productoId=$productoId&sucursalId=$sucursalId"
$esperado3 = 27.50
Write-Host "  Stock actual: $($stock3.cantidad) unidades" -ForegroundColor White
Write-Host "  Costo promedio actual: `$$([math]::Round($stock3.costoPromedio, 2))" -ForegroundColor White

if ([math]::Abs([math]::Round($stock3.costoPromedio, 2) - $esperado3) -lt 0.01) {
    Write-Host "  [OK] Costo promedio correcto`n" -ForegroundColor Green
} else {
    Write-Host "  [ERROR] Esperado `$$esperado3, obtenido `$$([math]::Round($stock3.costoPromedio, 2))`n" -ForegroundColor Red
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "VENTAS - Todas deben usar `$27.50" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Venta 1: 30 unidades
Write-Host "VENTA 1: 30 unidades @ `$27.50 cada una" -ForegroundColor Yellow
$venta1 = @{
    sucursalId = $sucursalId
    cajaId = 1
    clienteId = 2
    lineas = @(
        @{
            productoId = $productoId
            cantidad = 30
            precioUnitario = 50.00
            descuento = 0
        }
    )
    metodoPago = 1
} | ConvertTo-Json -Depth 3

$ventaResult1 = Invoke-RestMethod -Uri "$baseUrl/api/Ventas" -Method Post -Body $venta1 -ContentType "application/json"
Write-Host "  Venta creada: ID $($ventaResult1.id)" -ForegroundColor Green
Write-Host "  Total venta: `$$($ventaResult1.total)" -ForegroundColor White

Start-Sleep -Seconds 1

# Buscar costo en DB directamente (el DTO de respuesta no incluye costoTotal)
$ventaId1 = $ventaResult1.id
$costoResult1 = & $psql -h localhost -U postgres -d sincopos -t -A -q -c "SELECT COALESCE(SUM(costo_unitario * cantidad), 0) FROM public.detalle_ventas WHERE venta_id = $ventaId1;"
$costoVenta1 = [decimal]($costoResult1.Trim())

Write-Host "  Costo esperado: `$825.00 (30 * `$27.50)" -ForegroundColor White
Write-Host "  Costo obtenido: `$$([math]::Round($costoVenta1, 2))`n" -ForegroundColor White

Start-Sleep -Seconds 2

# Venta 2: 50 unidades
Write-Host "VENTA 2: 50 unidades @ `$27.50 cada una" -ForegroundColor Yellow
$venta2 = @{
    sucursalId = $sucursalId
    cajaId = 1
    clienteId = 2
    lineas = @(
        @{
            productoId = $productoId
            cantidad = 50
            precioUnitario = 50.00
            descuento = 0
        }
    )
    metodoPago = 1
} | ConvertTo-Json -Depth 3

$ventaResult2 = Invoke-RestMethod -Uri "$baseUrl/api/Ventas" -Method Post -Body $venta2 -ContentType "application/json"
Write-Host "  Venta creada: ID $($ventaResult2.id)" -ForegroundColor Green
Write-Host "  Total venta: `$$($ventaResult2.total)" -ForegroundColor White

Start-Sleep -Seconds 1

$ventaId2 = $ventaResult2.id
$costoResult2 = & $psql -h localhost -U postgres -d sincopos -t -A -q -c "SELECT COALESCE(SUM(costo_unitario * cantidad), 0) FROM public.detalle_ventas WHERE venta_id = $ventaId2;"
$costoVenta2 = [decimal]($costoResult2.Trim())

Write-Host "  Costo esperado: `$1,375.00 (50 * `$27.50)" -ForegroundColor White
Write-Host "  Costo obtenido: `$$([math]::Round($costoVenta2, 2))`n" -ForegroundColor White

Start-Sleep -Seconds 2

# Venta 3: 40 unidades
Write-Host "VENTA 3: 40 unidades @ `$27.50 cada una" -ForegroundColor Yellow
$venta3 = @{
    sucursalId = $sucursalId
    cajaId = 1
    clienteId = 2
    lineas = @(
        @{
            productoId = $productoId
            cantidad = 40
            precioUnitario = 50.00
            descuento = 0
        }
    )
    metodoPago = 1
} | ConvertTo-Json -Depth 3

$ventaResult3 = Invoke-RestMethod -Uri "$baseUrl/api/Ventas" -Method Post -Body $venta3 -ContentType "application/json"
Write-Host "  Venta creada: ID $($ventaResult3.id)" -ForegroundColor Green
Write-Host "  Total venta: `$$($ventaResult3.total)" -ForegroundColor White

Start-Sleep -Seconds 1

$ventaId3 = $ventaResult3.id
$costoResult3 = & $psql -h localhost -U postgres -d sincopos -t -A -q -c "SELECT COALESCE(SUM(costo_unitario * cantidad), 0) FROM public.detalle_ventas WHERE venta_id = $ventaId3;"
$costoVenta3 = [decimal]($costoResult3.Trim())

Write-Host "  Costo esperado: `$1,100.00 (40 * `$27.50)" -ForegroundColor White
Write-Host "  Costo obtenido: `$$([math]::Round($costoVenta3, 2))`n" -ForegroundColor White

Start-Sleep -Seconds 2

# Stock final
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "VERIFICACION FINAL" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$stockFinal = Invoke-RestMethod -Uri "$baseUrl/api/Inventario?productoId=$productoId&sucursalId=$sucursalId"
Write-Host "Stock final: $($stockFinal.cantidad) unidades (esperado 80)" -ForegroundColor White
Write-Host "Costo promedio final: `$$([math]::Round($stockFinal.costoPromedio, 2))" -ForegroundColor White
Write-Host "(Debe seguir siendo `$27.50 porque Promedio Ponderado no cambia con ventas)`n" -ForegroundColor Gray

# Resumen
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "RESUMEN DE RESULTADOS" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$todoCorrecto = $true

# Verificar venta 1
$esperadoVenta1 = 825.00
if ([math]::Abs($costoVenta1 - $esperadoVenta1) -lt 0.01) {
    Write-Host "[OK] Venta 1: `$$([math]::Round($costoVenta1, 2)) (esperado `$$esperadoVenta1)" -ForegroundColor Green
} else {
    Write-Host "[ERROR] Venta 1: `$$([math]::Round($costoVenta1, 2)) (esperado `$$esperadoVenta1)" -ForegroundColor Red
    $todoCorrecto = $false
}

# Verificar venta 2
$esperadoVenta2 = 1375.00
if ([math]::Abs($costoVenta2 - $esperadoVenta2) -lt 0.01) {
    Write-Host "[OK] Venta 2: `$$([math]::Round($costoVenta2, 2)) (esperado `$$esperadoVenta2)" -ForegroundColor Green
} else {
    Write-Host "[ERROR] Venta 2: `$$([math]::Round($costoVenta2, 2)) (esperado `$$esperadoVenta2)" -ForegroundColor Red
    $todoCorrecto = $false
}

# Verificar venta 3
$esperadoVenta3 = 1100.00
if ([math]::Abs($costoVenta3 - $esperadoVenta3) -lt 0.01) {
    Write-Host "[OK] Venta 3: `$$([math]::Round($costoVenta3, 2)) (esperado `$$esperadoVenta3)" -ForegroundColor Green
} else {
    Write-Host "[ERROR] Venta 3: `$$([math]::Round($costoVenta3, 2)) (esperado `$$esperadoVenta3)" -ForegroundColor Red
    $todoCorrecto = $false
}

# Verificar stock final
$stockEsperado = 80
if ([math]::Round($stockFinal.cantidad, 0) -eq $stockEsperado) {
    Write-Host "[OK] Stock final: $([math]::Round($stockFinal.cantidad, 0)) (esperado $stockEsperado)" -ForegroundColor Green
} else {
    Write-Host "[ERROR] Stock final: $([math]::Round($stockFinal.cantidad, 0)) (esperado $stockEsperado)" -ForegroundColor Red
    $todoCorrecto = $false
}

# Verificar que costo promedio no cambio
if ([math]::Abs([math]::Round($stockFinal.costoPromedio, 2) - 27.50) -lt 0.01) {
    Write-Host "[OK] Costo promedio se mantiene en `$27.50" -ForegroundColor Green
} else {
    Write-Host "[ERROR] Costo promedio cambio a `$$([math]::Round($stockFinal.costoPromedio, 2))" -ForegroundColor Red
    $todoCorrecto = $false
}

Write-Host "`n========================================" -ForegroundColor Cyan
if ($todoCorrecto) {
    Write-Host "RESULTADO: PROMEDIO PONDERADO FUNCIONA CORRECTAMENTE" -ForegroundColor Green
} else {
    Write-Host "RESULTADO: HAY ERRORES EN PROMEDIO PONDERADO" -ForegroundColor Red
}
Write-Host "========================================`n" -ForegroundColor Cyan
