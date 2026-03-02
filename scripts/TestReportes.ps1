# Test de Reportes
# Prueba los 3 reportes principales del sistema

$baseUrl = "http://localhost:5086"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "TEST DE REPORTES" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Fechas para el reporte
$fechaDesde = (Get-Date).AddDays(-7).ToString("yyyy-MM-dd")
$fechaHasta = (Get-Date).ToString("yyyy-MM-dd")

# ─── 1. Reporte de Ventas ─────────────────────────────────────────

Write-Host "1. REPORTE DE VENTAS" -ForegroundColor Yellow
Write-Host "   Periodo: $fechaDesde a $fechaHasta`n" -ForegroundColor Gray

try {
    $reporteVentas = Invoke-RestMethod -Uri "$baseUrl/api/Reportes/ventas?fechaDesde=$fechaDesde&fechaHasta=$fechaHasta" -Method Get

    Write-Host "  RESUMEN:" -ForegroundColor Green
    Write-Host "   - Total ventas:      `$$([math]::Round($reporteVentas.TotalVentas, 2))" -ForegroundColor White
    Write-Host "   - Cantidad ventas:   $($reporteVentas.CantidadVentas)" -ForegroundColor White
    Write-Host "   - Ticket promedio:   `$$([math]::Round($reporteVentas.TicketPromedio, 2))" -ForegroundColor White
    Write-Host "   - Costo total:       `$$([math]::Round($reporteVentas.CostoTotal, 2))" -ForegroundColor White
    Write-Host "   - Utilidad total:    `$$([math]::Round($reporteVentas.UtilidadTotal, 2))" -ForegroundColor White
    Write-Host "   - Margen promedio:   $([math]::Round($reporteVentas.MargenPromedio, 2))%" -ForegroundColor White

    if ($reporteVentas.VentasPorMetodoPago.Count -gt 0) {
        Write-Host "`n  POR METODO DE PAGO:" -ForegroundColor Green
        foreach ($metodo in $reporteVentas.VentasPorMetodoPago) {
            Write-Host "   - $($metodo.Metodo): `$$([math]::Round($metodo.Total, 2)) ($($metodo.Cantidad) ventas)" -ForegroundColor White
        }
    }

    if ($reporteVentas.VentasPorDia.Count -gt 0) {
        Write-Host "`n  POR DIA (últimos 3):" -ForegroundColor Green
        $ultimos3 = $reporteVentas.VentasPorDia | Select-Object -Last 3
        foreach ($dia in $ultimos3) {
            Write-Host "   - $($dia.Fecha): `$$([math]::Round($dia.Total, 2)) ($($dia.Cantidad) ventas)" -ForegroundColor White
        }
    }

    Write-Host "`n  [OK] Reporte de ventas generado correctamente`n" -ForegroundColor Green
}
catch {
    Write-Host "  [ERROR] Error al generar reporte de ventas: $($_.Exception.Message)`n" -ForegroundColor Red
}

# ─── 2. Reporte de Inventario Valorizado ─────────────────────────────────────────

Write-Host "2. REPORTE DE INVENTARIO VALORIZADO" -ForegroundColor Yellow

try {
    $reporteInventario = Invoke-RestMethod -Uri "$baseUrl/api/Reportes/inventario-valorizado?soloConStock=true" -Method Get

    Write-Host "  RESUMEN:" -ForegroundColor Green
    Write-Host "   - Total productos:      $($reporteInventario.TotalProductos)" -ForegroundColor White
    Write-Host "   - Total unidades:       $([math]::Round($reporteInventario.TotalUnidades, 2))" -ForegroundColor White
    Write-Host "   - Valor costo:          `$$([math]::Round($reporteInventario.TotalCosto, 2))" -ForegroundColor White
    Write-Host "   - Valor venta:          `$$([math]::Round($reporteInventario.TotalVenta, 2))" -ForegroundColor White
    Write-Host "   - Utilidad potencial:   `$$([math]::Round($reporteInventario.UtilidadPotencial, 2))" -ForegroundColor White

    if ($reporteInventario.Productos.Count -gt 0) {
        Write-Host "`n  TOP 5 PRODUCTOS POR VALOR:" -ForegroundColor Green
        $top5 = $reporteInventario.Productos | Select-Object -First 5
        foreach ($prod in $top5) {
            Write-Host "   - $($prod.Nombre) ($($prod.NombreSucursal))" -ForegroundColor White
            Write-Host "     Cant: $([math]::Round($prod.Cantidad, 2)) | Costo: `$$([math]::Round($prod.CostoTotal, 2)) | Venta: `$$([math]::Round($prod.ValorVenta, 2))" -ForegroundColor Gray
        }
    }

    Write-Host "`n  [OK] Reporte de inventario generado correctamente`n" -ForegroundColor Green
}
catch {
    Write-Host "  [ERROR] Error al generar reporte de inventario: $($_.Exception.Message)`n" -ForegroundColor Red
}

# ─── 3. Reporte de Caja ─────────────────────────────────────────

Write-Host "3. REPORTE DE CAJA" -ForegroundColor Yellow

try {
    $cajaId = 1
    $reporteCaja = Invoke-RestMethod -Uri "$baseUrl/api/Reportes/caja/$cajaId" -Method Get

    Write-Host "  CAJA: $($reporteCaja.NombreCaja) ($($reporteCaja.NombreSucursal))" -ForegroundColor Green
    Write-Host "   - Estado: $(if ($reporteCaja.FechaCierre) { 'Cerrada' } else { 'Abierta' })" -ForegroundColor White
    Write-Host "   - Apertura: $($reporteCaja.FechaApertura)" -ForegroundColor White
    if ($reporteCaja.FechaCierre) {
        Write-Host "   - Cierre:   $($reporteCaja.FechaCierre)" -ForegroundColor White
    }

    Write-Host "`n  MOVIMIENTOS:" -ForegroundColor Green
    Write-Host "   - Monto apertura:          `$$([math]::Round($reporteCaja.MontoApertura, 2))" -ForegroundColor White
    Write-Host "   - Total ventas efectivo:   `$$([math]::Round($reporteCaja.TotalVentasEfectivo, 2))" -ForegroundColor White
    Write-Host "   - Total ventas tarjeta:    `$$([math]::Round($reporteCaja.TotalVentasTarjeta, 2))" -ForegroundColor White
    Write-Host "   - Total ventas transferencia: `$$([math]::Round($reporteCaja.TotalVentasTransferencia, 2))" -ForegroundColor White
    Write-Host "   - TOTAL VENTAS:            `$$([math]::Round($reporteCaja.TotalVentas, 2))" -ForegroundColor Cyan

    if ($reporteCaja.MontoCierre) {
        Write-Host "`n  CIERRE:" -ForegroundColor Green
        Write-Host "   - Monto cierre:        `$$([math]::Round($reporteCaja.MontoCierre, 2))" -ForegroundColor White
        Write-Host "   - Diferencia esperada: `$$([math]::Round($reporteCaja.DiferenciaEsperado, 2))" -ForegroundColor $(if ($reporteCaja.DiferenciaEsperado -eq 0) { 'Green' } else { 'Yellow' })
    }

    Write-Host "`n  VENTAS (últimas 5):" -ForegroundColor Green
    $ultimas5 = $reporteCaja.Ventas | Select-Object -First 5
    foreach ($venta in $ultimas5) {
        Write-Host "   - $($venta.NumeroVenta): `$$([math]::Round($venta.Total, 2)) ($($venta.MetodoPago))" -ForegroundColor White
    }

    Write-Host "`n  [OK] Reporte de caja generado correctamente`n" -ForegroundColor Green
}
catch {
    Write-Host "  [ERROR] Error al generar reporte de caja: $($_.Exception.Message)`n" -ForegroundColor Red
}

# ─── RESUMEN ─────────────────────────────────────────

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "PRUEBA COMPLETADA" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan
