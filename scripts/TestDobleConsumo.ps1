# Test para verificar que NO hay doble consumo de stock
# Verifica que el stock y los lotes se consumen una sola vez

$psql = "C:\Program Files\PostgreSQL\16\bin\psql.exe"
$baseUrl = "http://localhost:5086"
$productoId = "11111111-1111-1111-1111-111111111111"
$sucursalId = 1

# Set PostgreSQL password
$env:PGPASSWORD = "postgrade"

Write-Host "`n========================================"  -ForegroundColor Cyan
Write-Host "TEST: VERIFICAR NO HAY DOBLE CONSUMO" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Limpiar BD
Write-Host "Limpiando base de datos..." -ForegroundColor Yellow
& $psql -h localhost -U postgres -d sincopos -c "DELETE FROM public.detalle_ventas;"
& $psql -h localhost -U postgres -d sincopos -c "DELETE FROM public.ventas;"
& $psql -h localhost -U postgres -d sincopos -c "DELETE FROM public.lotes_inventario;"
& $psql -h localhost -U postgres -d sincopos -c "DELETE FROM public.stock;"
& $psql -h localhost -U postgres -d sincopos -c "DELETE FROM events.mt_events;"
& $psql -h localhost -U postgres -d sincopos -c "DELETE FROM events.mt_streams;"
Write-Host "BD limpia.`n" -ForegroundColor Green

# Configurar metodo FIFO para verificar consumo de lotes
Write-Host "Configurando metodo FIFO..." -ForegroundColor Yellow
& $psql -h localhost -U postgres -d sincopos -c 'UPDATE public.sucursales SET metodo_costeo = 1 WHERE "Id" = 1;'
Write-Host "Metodo configurado.`n" -ForegroundColor Green

Start-Sleep -Seconds 2

# Crear UN SOLO lote con 100 unidades
Write-Host "PASO 1: Crear lote con 100 unidades @ `$30.00" -ForegroundColor Yellow
$lote = @{
    productoId = $productoId
    sucursalId = $sucursalId
    cantidad = 100
    costoUnitario = 30.00
    porcentajeImpuesto = 0
    montoImpuestoUnitario = 0
    referencia = "LOTE-TEST-001"
    terceroId = 1
} | ConvertTo-Json

Invoke-RestMethod -Uri "$baseUrl/api/Inventario/entrada" -Method Post -Body $lote -ContentType "application/json" | Out-Null
Write-Host "  Lote creado: 100 unidades @ `$30.00`n" -ForegroundColor Green

Start-Sleep -Seconds 2

# Verificar stock inicial
$stockInicial = Invoke-RestMethod -Uri "$baseUrl/api/Inventario?productoId=$productoId&sucursalId=$sucursalId"
Write-Host "Stock inicial: $($stockInicial.cantidad) unidades`n" -ForegroundColor White

# Verificar lotes iniciales
Write-Host "Verificando lotes ANTES de la venta..." -ForegroundColor Yellow
$lotesResult = & $psql -h localhost -U postgres -d sincopos -t -A -q -c "SELECT cantidad_inicial, cantidad_disponible FROM public.lotes_inventario WHERE producto_id = '$productoId' AND sucursal_id = $sucursalId;"
$lotesAntes = $lotesResult -split "`n" | Where-Object { $_ -ne "" }
foreach ($lote in $lotesAntes) {
    $parts = $lote -split "\|"
    Write-Host "  Lote: Inicial=$($parts[0]), Disponible=$($parts[1])" -ForegroundColor White
}
Write-Host ""

# Crear venta de 30 unidades
Write-Host "PASO 2: Crear venta de 30 unidades" -ForegroundColor Yellow
$venta = @{
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

$ventaResult = Invoke-RestMethod -Uri "$baseUrl/api/Ventas" -Method Post -Body $venta -ContentType "application/json"
Write-Host "  Venta creada: ID $($ventaResult.id)`n" -ForegroundColor Green

Start-Sleep -Seconds 3

# Verificar stock final
$stockFinal = Invoke-RestMethod -Uri "$baseUrl/api/Inventario?productoId=$productoId&sucursalId=$sucursalId"
Write-Host "Stock final: $($stockFinal.cantidad) unidades" -ForegroundColor White

# Verificar lotes finales
Write-Host "`nVerificando lotes DESPUES de la venta..." -ForegroundColor Yellow
$lotesResult = & $psql -h localhost -U postgres -d sincopos -t -A -q -c "SELECT cantidad_inicial, cantidad_disponible FROM public.lotes_inventario WHERE producto_id = '$productoId' AND sucursal_id = $sucursalId;"
$lotesDespues = $lotesResult -split "`n" | Where-Object { $_ -ne "" }
foreach ($lote in $lotesDespues) {
    $parts = $lote -split "\|"
    Write-Host "  Lote: Inicial=$($parts[0]), Disponible=$($parts[1])" -ForegroundColor White
}
Write-Host ""

# VALIDACIONES
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "VALIDACIONES" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$todoOk = $true

# 1. Stock debe ser 70 (100 - 30), NO 40 (que seria doble consumo)
$stockEsperado = 70
$stockObtenido = [math]::Round($stockFinal.cantidad, 0)

Write-Host "1. Stock despues de venta:" -ForegroundColor Yellow
Write-Host "   Esperado: $stockEsperado unidades (consumo simple)" -ForegroundColor White
Write-Host "   Obtenido: $stockObtenido unidades" -ForegroundColor White

if ($stockObtenido -eq $stockEsperado) {
    Write-Host "   [OK] Stock correcto - NO hay doble consumo" -ForegroundColor Green
} elseif ($stockObtenido -eq 40) {
    Write-Host "   [ERROR] Stock = 40 - HAY DOBLE CONSUMO (100 - 30 - 30)" -ForegroundColor Red
    $todoOk = $false
} else {
    Write-Host "   [ERROR] Stock inesperado: $stockObtenido" -ForegroundColor Red
    $todoOk = $false
}

# 2. Lote disponible debe ser 70 (100 - 30), NO 40
Write-Host "`n2. Lote disponible:" -ForegroundColor Yellow
if ($lotesDespues.Count -gt 0) {
    $parts = $lotesDespues[0] -split "\|"
    $loteDisponible = [decimal]$parts[1]

    Write-Host "   Esperado: 70 unidades (consumo simple)" -ForegroundColor White
    Write-Host "   Obtenido: $loteDisponible unidades" -ForegroundColor White

    if ($loteDisponible -eq 70) {
        Write-Host "   [OK] Lote correcto - NO hay doble consumo" -ForegroundColor Green
    } elseif ($loteDisponible -eq 40) {
        Write-Host "   [ERROR] Lote = 40 - HAY DOBLE CONSUMO en lotes" -ForegroundColor Red
        $todoOk = $false
    } else {
        Write-Host "   [ERROR] Lote disponible inesperado: $loteDisponible" -ForegroundColor Red
        $todoOk = $false
    }
} else {
    Write-Host "   [ERROR] No se encontraron lotes" -ForegroundColor Red
    $todoOk = $false
}

# 3. Verificar eventos en Marten
Write-Host "`n3. Eventos en Marten:" -ForegroundColor Yellow
$streamId = "inv-$productoId-$sucursalId"
$eventCountResult = & $psql -h localhost -U postgres -d sincopos -t -A -q -c "SELECT COUNT(*) FROM events.mt_events WHERE stream_id = '$streamId'::uuid;"
$eventCount = [int]$eventCountResult.Trim()

Write-Host "   Total de eventos: $eventCount" -ForegroundColor White
Write-Host "   (Debe incluir: 1 EntradaCompra + 1 SalidaVenta = 2 eventos)" -ForegroundColor Gray

if ($eventCount -ge 2) {
    Write-Host "   [OK] Eventos registrados correctamente" -ForegroundColor Green
} else {
    Write-Host "   [ADVERTENCIA] Menos eventos de los esperados" -ForegroundColor Yellow
}

# RESULTADO FINAL
Write-Host "`n========================================" -ForegroundColor Cyan
if ($todoOk) {
    Write-Host "RESULTADO: DOBLE CONSUMO RESUELTO [OK]" -ForegroundColor Green
    Write-Host "El stock se consume una sola vez" -ForegroundColor Green
} else {
    Write-Host "RESULTADO: AUN HAY DOBLE CONSUMO [ERROR]" -ForegroundColor Red
    Write-Host "El stock se esta consumiendo dos veces" -ForegroundColor Red
}
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
