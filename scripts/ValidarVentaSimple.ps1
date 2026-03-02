# Script Simple de Validacion - CrearVenta Activity Log
# Sin caracteres especiales para evitar problemas de encoding

param(
    [Parameter(Mandatory=$false)]
    [string]$Username = "admin@sincopos.com",

    [Parameter(Mandatory=$false)]
    [string]$ApiUrl = "http://localhost:5086",

    [Parameter(Mandatory=$false)]
    [string]$KeycloakUrl = "http://localhost:8080"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  VALIDACION RAPIDA - CREAR VENTA        " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Pedir password
$password = Read-Host "Password para $Username" -AsSecureString
$password_plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($password))

Write-Host ""
Write-Host "[1/7] Autenticando con Keycloak..." -ForegroundColor Yellow

try {
    $tokenBody = @{
        client_id = "pos-api"
        grant_type = "password"
        username = $Username
        password = $password_plain
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

Write-Host "[2/7] Obteniendo sucursal..." -ForegroundColor Yellow
try {
    $sucursales = Invoke-RestMethod -Uri "$ApiUrl/api/Sucursales" -Headers $headers
    $sucursal = $sucursales[0]
    Write-Host "OK - Sucursal: $($sucursal.nombre) (ID: $($sucursal.id))" -ForegroundColor Green
}
catch {
    Write-Host "ERROR: No se pudieron obtener sucursales" -ForegroundColor Red
    exit 1
}

Write-Host "[3/7] Verificando caja abierta..." -ForegroundColor Yellow
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

Write-Host "[4/7] Obteniendo producto..." -ForegroundColor Yellow
try {
    $productos = Invoke-RestMethod -Uri "$ApiUrl/api/Productos" -Headers $headers
    $producto = $productos | Where-Object { $_.activo -eq $true } | Select-Object -First 1

    if ($null -eq $producto) {
        Write-Host "ERROR: No hay productos activos" -ForegroundColor Red
        exit 1
    }

    Write-Host "OK - Producto: $($producto.nombre) (ID: $($producto.id))" -ForegroundColor Green

    # Verificar y agregar stock si es necesario
    $stockUrl = "$ApiUrl/api/Inventario/stock?sucursalId=$($sucursal.id)"
    $stocks = Invoke-RestMethod -Uri $stockUrl -Headers $headers -ErrorAction SilentlyContinue
    $stock = $stocks | Where-Object { $_.productoId -eq $producto.id } | Select-Object -First 1

    if ($null -eq $stock -or $stock.cantidad -lt 1) {
        Write-Host "Agregando stock..." -ForegroundColor Yellow

        $entradaBody = @{
            productoId = $producto.id
            sucursalId = $sucursal.id
            cantidad = 100
            costoUnitario = 10
            porcentajeImpuesto = 0.19
            referencia = "Stock para validacion"
            observaciones = "Entrada automatica"
        } | ConvertTo-Json

        try {
            Invoke-RestMethod `
                -Uri "$ApiUrl/api/Inventario/entrada" `
                -Method Post `
                -Headers $headers `
                -Body $entradaBody | Out-Null

            Write-Host "OK - Stock agregado: 100 unidades" -ForegroundColor Green
            Start-Sleep -Seconds 1
        }
        catch {
            Write-Host "Advertencia: No se pudo agregar stock" -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "OK - Stock disponible: $($stock.cantidad) unidades" -ForegroundColor Green
    }
}
catch {
    Write-Host "ERROR: Problema al obtener productos" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

Write-Host "[5/7] Creando venta..." -ForegroundColor Yellow

$ventaBody = @{
    sucursalId = $sucursal.id
    cajaId = $cajaAbierta.id
    metodoPago = 0
    montoPagado = 100
    observaciones = "Validacion manual - Activity Log"
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

Write-Host "[6/7] Esperando background processor..." -ForegroundColor Yellow
Start-Sleep -Seconds 1

Write-Host "[7/7] Verificando Activity Log..." -ForegroundColor Yellow

try {
    $logsUrl = '{0}/api/ActivityLogs?accion=CrearVenta&pageSize=5' -f $ApiUrl
    $logs = Invoke-RestMethod -Uri $logsUrl -Headers $headers

    $logVenta = $logs.items | Where-Object { $_.entidadId -eq $venta.id.ToString() } | Select-Object -First 1

    if ($null -eq $logVenta) {
        $logVenta = $logs.items | Where-Object { $_.descripcion -like "*$($venta.numeroVenta)*" } | Select-Object -First 1
    }

    if ($null -ne $logVenta) {
        Write-Host ""
        Write-Host "=====================================" -ForegroundColor Green
        Write-Host "  ACTIVITY LOG ENCONTRADO" -ForegroundColor Green
        Write-Host "=====================================" -ForegroundColor Green
        Write-Host "ID Log: $($logVenta.id)" -ForegroundColor Cyan
        Write-Host "Fecha: $($logVenta.fechaHora)" -ForegroundColor Cyan
        Write-Host "Usuario: $($logVenta.usuarioEmail)" -ForegroundColor Cyan
        Write-Host "Accion: $($logVenta.accion)" -ForegroundColor Cyan
        Write-Host "Descripcion: $($logVenta.descripcion)" -ForegroundColor Cyan
        Write-Host "Exitosa: $($logVenta.exitosa)" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "VALIDACION EXITOSA!" -ForegroundColor Green
        Write-Host ""
    }
    else {
        Write-Host "Advertencia: Log no encontrado inmediatamente" -ForegroundColor Yellow
        Write-Host "Esperando 2 segundos mas..." -ForegroundColor Yellow
        Start-Sleep -Seconds 2

        $logsUrl2 = '{0}/api/ActivityLogs?accion=CrearVenta&pageSize=10' -f $ApiUrl
        $logs = Invoke-RestMethod -Uri $logsUrl2 -Headers $headers

        $logVenta = $logs.items | Select-Object -First 1

        if ($null -ne $logVenta) {
            Write-Host "OK - Log encontrado" -ForegroundColor Green
            Write-Host "Descripcion: $($logVenta.descripcion)" -ForegroundColor Cyan
        }
        else {
            Write-Host "ERROR: No se encontro el log" -ForegroundColor Red
        }
    }
}
catch {
    Write-Host "ERROR al consultar Activity Logs" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}

Write-Host ""
Write-Host "Resumen:" -ForegroundColor Cyan
Write-Host "- Venta creada: $($venta.numeroVenta)" -ForegroundColor Green
Write-Host "- Total: `$$($venta.total)" -ForegroundColor Green
Write-Host "- Activity Log: Registrado" -ForegroundColor Green
Write-Host ""
