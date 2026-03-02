# Script para consultar lotes de inventario
$ErrorActionPreference = "Stop"

Write-Host "===========================================" -ForegroundColor Cyan
Write-Host "  CONSULTA DE LOTES - FIFO VERIFICATION   " -ForegroundColor Cyan
Write-Host "===========================================" -ForegroundColor Cyan
Write-Host ""

$ApiUrl = "http://localhost:5086"
$headers = @{ "Content-Type" = "application/json" }

# Consultar el stock actual
Write-Host "[1] Stock actual:" -ForegroundColor Yellow
try {
    $stock = Invoke-RestMethod -Uri "$ApiUrl/api/Inventario/stock?sucursalId=1" -Headers $headers
    $producto = $stock | Where-Object { $_.productoId -eq "11111111-1111-1111-1111-111111111111" } | Select-Object -First 1

    if ($producto) {
        Write-Host "  Cantidad disponible: $($producto.cantidad)" -ForegroundColor White
        Write-Host "  Costo promedio: `$$($producto.costoPromedio)" -ForegroundColor White
    }
}
catch {
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "[2] Detalles de ventas (costos reales):" -ForegroundColor Yellow
try {
    $ventas = Invoke-RestMethod -Uri "$ApiUrl/api/Ventas?sucursalId=1&pageSize=10" -Headers $headers

    foreach ($venta in $ventas.items | Sort-Object numeroVenta) {
        Write-Host "  $($venta.numeroVenta) - Total: `$$($venta.total)" -ForegroundColor Cyan

        # Obtener detalles
        $ventaDetalle = Invoke-RestMethod -Uri "$ApiUrl/api/Ventas/$($venta.id)" -Headers $headers
        foreach ($detalle in $ventaDetalle.detalles) {
            if ($detalle.productoId -eq "11111111-1111-1111-1111-111111111111") {
                Write-Host "    - Cantidad: $($detalle.cantidad), Costo Unit: `$$($detalle.costoUnitario), Costo Total: `$$($detalle.cantidad * $detalle.costoUnitario)" -ForegroundColor White
            }
        }
    }
}
catch {
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "===========================================" -ForegroundColor Green
Write-Host "  CONSULTA COMPLETADA                     " -ForegroundColor Green
Write-Host "===========================================" -ForegroundColor Green
