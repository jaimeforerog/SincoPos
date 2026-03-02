# Test rápido de creación de venta
$ErrorActionPreference = "Stop"

$ApiUrl = "http://localhost:5086"
$KeycloakUrl = "http://localhost:8080"
$Username = "admin@sincopos.com"
$Password = "Admin123!"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  TEST RAPIDO - CREAR VENTA             " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "[1/6] Autenticando con Keycloak..." -ForegroundColor Yellow
try {
    $tokenBody = @{
        client_id = "pos-api"
        grant_type = "password"
        username = $Username
        password = $Password
    }

    $tokenResponse = Invoke-RestMethod `
        -Uri "$KeycloakUrl/realms/sincopos/protocol/openid-connect/token" `
        -Method Post `
        -Body $tokenBody `
        -ContentType "application/x-www-form-urlencoded"

    $token = $tokenResponse.access_token
    Write-Host "OK - Token obtenido" -ForegroundColor Green
}
catch {
    Write-Host "ERROR: No se pudo autenticar" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

Write-Host "[2/6] Obteniendo sucursal..." -ForegroundColor Yellow
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

Write-Host "[3/6] Verificando caja..." -ForegroundColor Yellow
try {
    $cajasUrl = "$ApiUrl/api/Cajas?sucursalId=$($sucursal.id)"
    $cajas = Invoke-RestMethod -Uri $cajasUrl -Headers $headers
    $cajaAbierta = $cajas | Where-Object { $_.estado -eq "Abierta" } | Select-Object -First 1

    if ($null -eq $cajaAbierta) {
        Write-Host "Abriendo caja..." -ForegroundColor Yellow
        $cajaCerrada = $cajas | Where-Object { $_.estado -eq "Cerrada" -and $_.activo } | Select-Object -First 1

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

        Start-Sleep -Seconds 2
        $cajas = Invoke-RestMethod -Uri $cajasUrl -Headers $headers
        $cajaAbierta = $cajas | Where-Object { $_.estado -eq "Abierta" } | Select-Object -First 1
    }

    Write-Host "OK - Caja: $($cajaAbierta.nombre) (ID: $($cajaAbierta.id))" -ForegroundColor Green
}
catch {
    Write-Host "ERROR: Problema con las cajas" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

Write-Host "[4/6] Obteniendo producto..." -ForegroundColor Yellow
try {
    $productos = Invoke-RestMethod -Uri "$ApiUrl/api/Productos" -Headers $headers
    $producto = $productos | Where-Object { $_.activo -eq $true } | Select-Object -First 1

    if ($null -eq $producto) {
        Write-Host "ERROR: No hay productos activos" -ForegroundColor Red
        exit 1
    }

    Write-Host "OK - Producto: $($producto.nombre) (ID: $($producto.id), Precio: $($producto.precioVenta))" -ForegroundColor Green
}
catch {
    Write-Host "ERROR: Problema al obtener productos" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

Write-Host "[5/6] Creando venta..." -ForegroundColor Yellow
$ventaBody = @{
    sucursalId = $sucursal.id
    cajaId = $cajaAbierta.id
    metodoPago = 0
    montoPagado = $producto.precioVenta
    observaciones = "Test automatico - PostgreSQL local"
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

    Write-Host "OK - Venta creada: $($venta.numeroVenta)" -ForegroundColor Green
    Write-Host "     Total: `$$($venta.total)" -ForegroundColor Cyan
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

Write-Host "[6/6] Verificando Activity Log..." -ForegroundColor Yellow
Start-Sleep -Seconds 2

try {
    $logsUrl = "$ApiUrl/api/ActivityLogs?accion=CrearVenta&pageSize=5"
    $logs = Invoke-RestMethod -Uri $logsUrl -Headers $headers

    $logVenta = $logs.items | Where-Object { $_.descripcion -like "*$($venta.numeroVenta)*" } | Select-Object -First 1

    if ($null -ne $logVenta) {
        Write-Host ""
        Write-Host "=====================================" -ForegroundColor Green
        Write-Host "  VALIDACION EXITOSA" -ForegroundColor Green
        Write-Host "=====================================" -ForegroundColor Green
        Write-Host "Venta: $($venta.numeroVenta)" -ForegroundColor Cyan
        Write-Host "Total: `$$($venta.total)" -ForegroundColor Cyan
        Write-Host "Activity Log: Registrado" -ForegroundColor Green
        Write-Host ""
        Write-Host "PostgreSQL Local: FUNCIONANDO" -ForegroundColor Green
        Write-Host "Keycloak (Docker): FUNCIONANDO" -ForegroundColor Green
        Write-Host "API: FUNCIONANDO" -ForegroundColor Green
        Write-Host ""
    }
    else {
        Write-Host "ADVERTENCIA: Log no encontrado" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "ADVERTENCIA: Error al consultar logs" -ForegroundColor Yellow
}
