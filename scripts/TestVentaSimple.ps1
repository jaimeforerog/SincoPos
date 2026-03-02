# Test de venta SIN autenticación
$ErrorActionPreference = "Stop"

$ApiUrl = "http://localhost:5086"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  TEST VENTA - SIN AUTENTICACION        " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

$headers = @{
    "Content-Type" = "application/json"
}

Write-Host "[1/5] Obteniendo sucursal..." -ForegroundColor Yellow
try {
    $sucursales = Invoke-RestMethod -Uri "$ApiUrl/api/Sucursales" -Headers $headers
    if ($sucursales.Count -eq 0) {
        Write-Host "ERROR: No hay sucursales" -ForegroundColor Red
        exit 1
    }
    $sucursal = $sucursales[0]
    Write-Host "OK - Sucursal: $($sucursal.nombre) (ID: $($sucursal.id))" -ForegroundColor Green
}
catch {
    Write-Host "ERROR: No se pudieron obtener sucursales" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

Write-Host "[2/5] Verificando caja..." -ForegroundColor Yellow
try {
    $cajasUrl = "$ApiUrl/api/Cajas?sucursalId=$($sucursal.id)"
    $cajas = Invoke-RestMethod -Uri $cajasUrl -Headers $headers
    $cajaAbierta = $cajas | Where-Object { $_.estado -eq "Abierta" } | Select-Object -First 1

    if ($null -eq $cajaAbierta) {
        Write-Host "Abriendo caja..." -ForegroundColor Yellow
        $cajaCerrada = $cajas | Where-Object { $_.estado -eq "Cerrada" -and $_.activa } | Select-Object -First 1

        if ($null -eq $cajaCerrada) {
            Write-Host "ERROR: No hay cajas disponibles" -ForegroundColor Red
            exit 1
        }

        $abrirBody = @{ montoApertura = 100 } | ConvertTo-Json
        Invoke-RestMethod `
            -Uri "$ApiUrl/api/Cajas/$($cajaCerrada.id)/abrir" `
            -Method Post `
            -Headers $headers `
            -Body $abrirBody | Out-Null

        Start-Sleep -Seconds 1
        $cajas = Invoke-RestMethod -Uri $cajasUrl -Headers $headers
        $cajaAbierta = $cajas | Where-Object { $_.estado -eq "Abierta" } | Select-Object -First 1
    }

    Write-Host "OK - Caja: $($cajaAbierta.nombre) (ID: $($cajaAbierta.id), Estado: $($cajaAbierta.estado))" -ForegroundColor Green
}
catch {
    Write-Host "ERROR: Problema con las cajas" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

Write-Host "[3/5] Obteniendo producto..." -ForegroundColor Yellow
try {
    $productos = Invoke-RestMethod -Uri "$ApiUrl/api/Productos" -Headers $headers
    $producto = $productos | Where-Object { $_.activo -eq $true } | Select-Object -First 1

    if ($null -eq $producto) {
        Write-Host "ERROR: No hay productos activos" -ForegroundColor Red
        exit 1
    }

    Write-Host "OK - Producto: $($producto.nombre)" -ForegroundColor Green
    Write-Host "     ID: $($producto.id)" -ForegroundColor Cyan
    Write-Host "     Precio: `$$($producto.precioVenta)" -ForegroundColor Cyan
}
catch {
    Write-Host "ERROR: Problema al obtener productos" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

Write-Host "[4/5] Creando venta..." -ForegroundColor Yellow
$ventaBody = @{
    sucursalId = $sucursal.id
    cajaId = $cajaAbierta.id
    metodoPago = 0
    montoPagado = $producto.precioVenta
    observaciones = "Venta de prueba - PostgreSQL Local"
    lineas = @(
        @{
            productoId = $producto.id
            cantidad = 1
            descuento = 0
        }
    )
} | ConvertTo-Json -Depth 5

try {
    $venta = Invoke-RestMethod `
        -Uri "$ApiUrl/api/Ventas" `
        -Method Post `
        -Headers $headers `
        -Body $ventaBody

    Write-Host ""
    Write-Host "=====================================" -ForegroundColor Green
    Write-Host "  VENTA CREADA EXITOSAMENTE          " -ForegroundColor Green
    Write-Host "=====================================" -ForegroundColor Green
    Write-Host "Numero: $($venta.numeroVenta)" -ForegroundColor Cyan
    Write-Host "Total: `$$($venta.total)" -ForegroundColor Cyan
    Write-Host "Estado: $($venta.estado)" -ForegroundColor Cyan
    Write-Host ""
}
catch {
    Write-Host "ERROR al crear venta" -ForegroundColor Red
    $errorResponse = $_.ErrorDetails.Message | ConvertFrom-Json -ErrorAction SilentlyContinue
    if ($errorResponse) {
        Write-Host "Mensaje: $errorResponse" -ForegroundColor Yellow
    }
    else {
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Yellow
    }
    exit 1
}

Write-Host "[5/5] Verificando Activity Log..." -ForegroundColor Yellow
Start-Sleep -Seconds 2

try {
    $logsUrl = "$ApiUrl/api/ActivityLogs?accion=CrearVenta&pageSize=5"
    $logs = Invoke-RestMethod -Uri $logsUrl -Headers $headers

    $logVenta = $logs.items | Where-Object { $_.descripcion -like "*$($venta.numeroVenta)*" } | Select-Object -First 1

    if ($null -ne $logVenta) {
        Write-Host "OK - Activity Log registrado" -ForegroundColor Green
        Write-Host "     Descripcion: $($logVenta.descripcion)" -ForegroundColor Cyan
    }
    else {
        Write-Host "ADVERTENCIA: Log no encontrado" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "ADVERTENCIA: Error al consultar logs" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Green
Write-Host "  PRUEBA COMPLETADA                  " -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host "PostgreSQL Local: FUNCIONANDO" -ForegroundColor Green
Write-Host "API: FUNCIONANDO" -ForegroundColor Green
Write-Host ""
