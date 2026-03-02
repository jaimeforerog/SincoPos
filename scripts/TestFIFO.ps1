# Script para probar consumo FIFO
$ApiUrl = "http://localhost:5086"
$headers = @{ "Content-Type" = "application/json" }

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  PRUEBA DE CONSUMO FIFO                 " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Obtener stock inicial
$stockInicial = Invoke-RestMethod -Uri "$ApiUrl/api/Inventario/stock?sucursalId=1" -Headers $headers
$producto = $stockInicial | Where-Object { $_.productoId -eq "11111111-1111-1111-1111-111111111111" } | Select-Object -First 1

Write-Host "Stock inicial:" -ForegroundColor Yellow
Write-Host "  Cantidad: $($producto.cantidad)" -ForegroundColor White
Write-Host "  Costo promedio: `$$($producto.costoPromedio)" -ForegroundColor White
Write-Host ""

# Venta 1: 30 unidades (debería consumir del lote más antiguo @ $25)
Write-Host "[Venta 1] 30 unidades..." -ForegroundColor Yellow
$venta1Body = @{
    sucursalId = 1
    cajaId = 1
    metodoPago = 0
    montoPagado = 1500
    observaciones = "FIFO Test - Venta 1 (30 unidades)"
    lineas = @(@{
        productoId = "11111111-1111-1111-1111-111111111111"
        cantidad = 30
        descuento = 0
    })
} | ConvertTo-Json -Depth 5

try {
    $venta1 = Invoke-RestMethod -Uri "$ApiUrl/api/Ventas" -Method Post -Headers $headers -Body $venta1Body
    Write-Host "  OK - Venta: $($venta1.numeroVenta), Total: `$$($venta1.total)" -ForegroundColor Green
}
catch {
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Start-Sleep -Seconds 1

# Venta 2: 40 unidades (debería consumir 20 del lote antiguo + 20 del medio)
Write-Host "[Venta 2] 40 unidades..." -ForegroundColor Yellow
$venta2Body = @{
    sucursalId = 1
    cajaId = 1
    metodoPago = 0
    montoPagado = 2000
    observaciones = "FIFO Test - Venta 2 (40 unidades)"
    lineas = @(@{
        productoId = "11111111-1111-1111-1111-111111111111"
        cantidad = 40
        descuento = 0
    })
} | ConvertTo-Json -Depth 5

try {
    $venta2 = Invoke-RestMethod -Uri "$ApiUrl/api/Ventas" -Method Post -Headers $headers -Body $venta2Body
    Write-Host "  OK - Venta: $($venta2.numeroVenta), Total: `$$($venta2.total)" -ForegroundColor Green
}
catch {
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Start-Sleep -Seconds 1

# Venta 3: 50 unidades (debería consumir del lote medio)
Write-Host "[Venta 3] 50 unidades..." -ForegroundColor Yellow
$venta3Body = @{
    sucursalId = 1
    cajaId = 1
    metodoPago = 0
    montoPagado = 2500
    observaciones = "FIFO Test - Venta 3 (50 unidades)"
    lineas = @(@{
        productoId = "11111111-1111-1111-1111-111111111111"
        cantidad = 50
        descuento = 0
    })
} | ConvertTo-Json -Depth 5

try {
    $venta3 = Invoke-RestMethod -Uri "$ApiUrl/api/Ventas" -Method Post -Headers $headers -Body $venta3Body
    Write-Host "  OK - Venta: $($venta3.numeroVenta), Total: `$$($venta3.total)" -ForegroundColor Green
}
catch {
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Start-Sleep -Seconds 1

# Obtener stock final
Write-Host ""
Write-Host "Stock final:" -ForegroundColor Yellow
$stockFinal = Invoke-RestMethod -Uri "$ApiUrl/api/Inventario/stock?sucursalId=1" -Headers $headers
$productoFinal = $stockFinal | Where-Object { $_.productoId -eq "11111111-1111-1111-1111-111111111111" } | Select-Object -First 1

Write-Host "  Cantidad: $($productoFinal.cantidad)" -ForegroundColor White
Write-Host "  Costo promedio: `$$($productoFinal.costoPromedio)" -ForegroundColor White
Write-Host ""

Write-Host "==========================================" -ForegroundColor Green
Write-Host "  PRUEBA FIFO COMPLETADA                 " -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host "Consumidas: 120 unidades" -ForegroundColor Cyan
Write-Host "Restantes: $($productoFinal.cantidad) unidades" -ForegroundColor Cyan
Write-Host ""
