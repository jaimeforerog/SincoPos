# Test de Promedio Ponderado
# Formula: Costo Promedio = (Stock Anterior * Costo Anterior + Entrada * Costo Entrada) / Stock Total

$baseUrl = "http://localhost:5086"
$productoId = "11111111-1111-1111-1111-111111111111"
$sucursalId = 1

Write-Host "`n========================================" -ForegroundColor Cyan
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
Write-Host "VENTA 1: 30 unidades (debe usar `$27.50)" -ForegroundColor Yellow
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
Write-Host "  Venta creada: $($ventaResult1.id)" -ForegroundColor Green
Write-Host "  Costo esperado: `$27.50 * 30 = `$825.00" -ForegroundColor White
Write-Host "  Costo obtenido: `$$([math]::Round($ventaResult1.costoTotal, 2))`n" -ForegroundColor White

Start-Sleep -Seconds 2

# Venta 2: 50 unidades
Write-Host "VENTA 2: 50 unidades (debe usar `$27.50)" -ForegroundColor Yellow
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
Write-Host "  Venta creada: $($ventaResult2.id)" -ForegroundColor Green
Write-Host "  Costo esperado: `$27.50 * 50 = `$1,375.00" -ForegroundColor White
Write-Host "  Costo obtenido: `$$([math]::Round($ventaResult2.costoTotal, 2))`n" -ForegroundColor White

Start-Sleep -Seconds 2

# Venta 3: 40 unidades
Write-Host "VENTA 3: 40 unidades (debe usar `$27.50)" -ForegroundColor Yellow
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
Write-Host "  Venta creada: $($ventaResult3.id)" -ForegroundColor Green
Write-Host "  Costo esperado: `$27.50 * 40 = `$1,100.00" -ForegroundColor White
Write-Host "  Costo obtenido: `$$([math]::Round($ventaResult3.costoTotal, 2))`n" -ForegroundColor White

Start-Sleep -Seconds 2

# Stock final
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "VERIFICACION FINAL" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$stockFinal = Invoke-RestMethod -Uri "$baseUrl/api/Inventario?productoId=$productoId&sucursalId=$sucursalId"
Write-Host "Stock final: $($stockFinal.cantidad) unidades" -ForegroundColor White
Write-Host "Costo promedio final: `$$([math]::Round($stockFinal.costoPromedio, 2))" -ForegroundColor White
Write-Host "(Debe seguir siendo `$27.50 porque Promedio Ponderado no cambia con ventas)`n" -ForegroundColor Gray

# Resumen
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "RESUMEN DE RESULTADOS" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$todoCorrecto = $true

# Verificar venta 1
$esperadoVenta1 = 825.00
$obtenidoVenta1 = [math]::Round($ventaResult1.costoTotal, 2)
if ([math]::Abs($obtenidoVenta1 - $esperadoVenta1) -lt 0.01) {
    Write-Host "[OK] Venta 1: `$$obtenidoVenta1 (esperado `$$esperadoVenta1)" -ForegroundColor Green
} else {
    Write-Host "[ERROR] Venta 1: `$$obtenidoVenta1 (esperado `$$esperadoVenta1)" -ForegroundColor Red
    $todoCorrecto = $false
}

# Verificar venta 2
$esperadoVenta2 = 1375.00
$obtenidoVenta2 = [math]::Round($ventaResult2.costoTotal, 2)
if ([math]::Abs($obtenidoVenta2 - $esperadoVenta2) -lt 0.01) {
    Write-Host "[OK] Venta 2: `$$obtenidoVenta2 (esperado `$$esperadoVenta2)" -ForegroundColor Green
} else {
    Write-Host "[ERROR] Venta 2: `$$obtenidoVenta2 (esperado `$$esperadoVenta2)" -ForegroundColor Red
    $todoCorrecto = $false
}

# Verificar venta 3
$esperadoVenta3 = 1100.00
$obtenidoVenta3 = [math]::Round($ventaResult3.costoTotal, 2)
if ([math]::Abs($obtenidoVenta3 - $esperadoVenta3) -lt 0.01) {
    Write-Host "[OK] Venta 3: `$$obtenidoVenta3 (esperado `$$esperadoVenta3)" -ForegroundColor Green
} else {
    Write-Host "[ERROR] Venta 3: `$$obtenidoVenta3 (esperado `$$esperadoVenta3)" -ForegroundColor Red
    $todoCorrecto = $false
}

# Verificar stock final
$stockEsperado = 80
if ($stockFinal.cantidad -eq $stockEsperado) {
    Write-Host "[OK] Stock final: $($stockFinal.cantidad) (esperado $stockEsperado)" -ForegroundColor Green
} else {
    Write-Host "[ERROR] Stock final: $($stockFinal.cantidad) (esperado $stockEsperado)" -ForegroundColor Red
    $todoCorrecto = $false
}

Write-Host "`n========================================" -ForegroundColor Cyan
if ($todoCorrecto) {
    Write-Host "RESULTADO: PROMEDIO PONDERADO FUNCIONA CORRECTAMENTE" -ForegroundColor Green
} else {
    Write-Host "RESULTADO: HAY ERRORES EN PROMEDIO PONDERADO" -ForegroundColor Red
}
Write-Host "========================================`n" -ForegroundColor Cyan
