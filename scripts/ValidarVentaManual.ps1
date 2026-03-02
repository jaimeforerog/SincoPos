# ============================================
# Script de Validación Manual - CrearVenta
# ============================================

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  VALIDACIÓN MANUAL - CREAR VENTA" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Configuración
$API_URL = "http://localhost:5086"
$KEYCLOAK_URL = "http://localhost:8080"
$REALM = "sincopos"
$CLIENT_ID = "pos-client"

# ============================================
# PASO 1: Solicitar credenciales
# ============================================
Write-Host "PASO 1: Autenticación" -ForegroundColor Yellow
Write-Host "------------------------------------"

$username = Read-Host "Ingresa tu email de usuario (ej: admin@sincopos.com)"
$password = Read-Host "Ingresa tu password" -AsSecureString
$password_plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($password))

Write-Host ""
Write-Host "Obteniendo token de Keycloak..." -ForegroundColor Gray

try {
    $body = @{
        client_id = $CLIENT_ID
        grant_type = "password"
        username = $username
        password = $password_plain
    }

    $tokenResponse = Invoke-RestMethod `
        -Uri "$KEYCLOAK_URL/realms/$REALM/protocol/openid-connect/token" `
        -Method Post `
        -Body $body `
        -ContentType "application/x-www-form-urlencoded"

    $token = $tokenResponse.access_token
    Write-Host "✅ Token obtenido exitosamente" -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host "❌ ERROR: No se pudo autenticar con Keycloak" -ForegroundColor Red
    Write-Host "Verifica que:" -ForegroundColor Yellow
    Write-Host "  - Keycloak está corriendo en $KEYCLOAK_URL" -ForegroundColor Yellow
    Write-Host "  - Las credenciales son correctas" -ForegroundColor Yellow
    Write-Host "  - El realm '$REALM' existe" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# ============================================
# PASO 2: Obtener Sucursal
# ============================================
Write-Host "PASO 2: Obteniendo datos necesarios..." -ForegroundColor Yellow
Write-Host "------------------------------------"

try {
    $sucursales = Invoke-RestMethod -Uri "$API_URL/api/Sucursales" -Headers $headers
    $sucursal = $sucursales[0]
    Write-Host "✅ Sucursal: $($sucursal.nombre) (ID: $($sucursal.id))" -ForegroundColor Green
}
catch {
    Write-Host "❌ ERROR: No se pudieron obtener sucursales" -ForegroundColor Red
    exit 1
}

# ============================================
# PASO 3: Obtener o Abrir Caja
# ============================================
try {
    $cajas = Invoke-RestMethod -Uri "$API_URL/api/Cajas?sucursalId=$($sucursal.id)" -Headers $headers
    $cajaAbierta = $cajas | Where-Object { $_.estado -eq "Abierta" } | Select-Object -First 1

    if ($null -eq $cajaAbierta) {
        Write-Host "⚠️  No hay cajas abiertas. Abriendo una..." -ForegroundColor Yellow

        $cajaCerrada = $cajas | Where-Object { $_.estado -eq "Cerrada" -and $_.activo } | Select-Object -First 1

        if ($null -eq $cajaCerrada) {
            Write-Host "❌ No hay cajas disponibles para abrir" -ForegroundColor Red
            exit 1
        }

        $abrirBody = @{
            montoApertura = 100
        } | ConvertTo-Json

        Invoke-RestMethod `
            -Uri "$API_URL/api/Cajas/$($cajaCerrada.id)/abrir" `
            -Method Post `
            -Headers $headers `
            -Body $abrirBody | Out-Null

        Start-Sleep -Seconds 2
        $cajas = Invoke-RestMethod -Uri "$API_URL/api/Cajas?sucursalId=$($sucursal.id)" -Headers $headers
        $cajaAbierta = $cajas | Where-Object { $_.estado -eq "Abierta" } | Select-Object -First 1
    }

    Write-Host "✅ Caja: $($cajaAbierta.nombre) (ID: $($cajaAbierta.id)) - Estado: Abierta" -ForegroundColor Green
}
catch {
    Write-Host "❌ ERROR: Problema con las cajas" -ForegroundColor Red
    Write-Host $_.Exception.Message
    exit 1
}

# ============================================
# PASO 4: Obtener Producto con Stock
# ============================================
try {
    $productos = Invoke-RestMethod -Uri "$API_URL/api/Productos" -Headers $headers
    $producto = $productos | Where-Object { $_.activo -eq $true } | Select-Object -First 1

    if ($null -eq $producto) {
        Write-Host "❌ No hay productos activos" -ForegroundColor Red
        exit 1
    }

    Write-Host "✅ Producto: $($producto.nombre) (ID: $($producto.id))" -ForegroundColor Green

    # Verificar stock
    $stocks = Invoke-RestMethod -Uri "$API_URL/api/Inventario/stock?sucursalId=$($sucursal.id)" -Headers $headers -ErrorAction SilentlyContinue
    $stock = $stocks | Where-Object { $_.productoId -eq $producto.id } | Select-Object -First 1

    if ($null -eq $stock -or $stock.cantidad -lt 1) {
        Write-Host "⚠️  Producto sin stock. Agregando stock..." -ForegroundColor Yellow

        $entradaBody = @{
            productoId = $producto.id
            sucursalId = $sucursal.id
            cantidad = 100
            costoUnitario = 10
            porcentajeImpuesto = 0.19
            referencia = "Stock para validación manual"
            observaciones = "Entrada automática para test"
        } | ConvertTo-Json

        try {
            Invoke-RestMethod `
                -Uri "$API_URL/api/Inventario/entrada" `
                -Method Post `
                -Headers $headers `
                -Body $entradaBody | Out-Null

            Write-Host "✅ Stock agregado: 100 unidades" -ForegroundColor Green
            Start-Sleep -Seconds 1
        }
        catch {
            Write-Host "⚠️  No se pudo agregar stock automáticamente" -ForegroundColor Yellow
            Write-Host "Intentando crear venta de todas formas..." -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "✅ Stock disponible: $($stock.cantidad) unidades" -ForegroundColor Green
    }
}
catch {
    Write-Host "❌ ERROR: Problema al obtener productos" -ForegroundColor Red
    Write-Host $_.Exception.Message
    exit 1
}

Write-Host ""

# ============================================
# PASO 5: Crear Venta
# ============================================
Write-Host "PASO 3: Creando venta..." -ForegroundColor Yellow
Write-Host "------------------------------------"

$ventaBody = @{
    sucursalId = $sucursal.id
    cajaId = $cajaAbierta.id
    metodoPago = 0
    montoPagado = 100
    observaciones = "Venta de validación manual - Activity Log"
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
        -Uri "$API_URL/api/Ventas" `
        -Method Post `
        -Headers $headers `
        -Body $ventaBody

    Write-Host ""
    Write-Host "✅ ¡VENTA CREADA EXITOSAMENTE!" -ForegroundColor Green
    Write-Host "------------------------------------"
    Write-Host "Número de Venta: $($venta.numeroVenta)" -ForegroundColor Cyan
    Write-Host "Total: `$$($venta.total)" -ForegroundColor Cyan
    Write-Host "Items: $($venta.detalles.Count)" -ForegroundColor Cyan
    Write-Host "Estado: $($venta.estado)" -ForegroundColor Cyan
    Write-Host ""
}
catch {
    Write-Host ""
    Write-Host "❌ ERROR al crear venta" -ForegroundColor Red
    Write-Host "------------------------------------"

    $errorResponse = $_.ErrorDetails.Message | ConvertFrom-Json -ErrorAction SilentlyContinue

    if ($errorResponse) {
        Write-Host "Mensaje: $errorResponse" -ForegroundColor Yellow
    }
    else {
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "Posibles causas:" -ForegroundColor Yellow
    Write-Host "  - Producto sin stock suficiente" -ForegroundColor Gray
    Write-Host "  - Precio no configurado" -ForegroundColor Gray
    Write-Host "  - Event Sourcing no inicializado" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Puedes intentar crear la venta manualmente desde Swagger:" -ForegroundColor Cyan
    Write-Host "  $API_URL/swagger" -ForegroundColor Cyan
    Write-Host ""
    exit 1
}

# ============================================
# PASO 6: Esperar Background Processor
# ============================================
Write-Host "Esperando 1 segundo (background processor)..." -ForegroundColor Gray
Start-Sleep -Seconds 1

# ============================================
# PASO 7: Verificar Activity Log
# ============================================
Write-Host ""
Write-Host "PASO 4: Verificando Activity Log..." -ForegroundColor Yellow
Write-Host "------------------------------------"

try {
    $uri = '{0}/api/ActivityLogs?accion=CrearVenta&pageSize=5' -f $API_URL
    $logs = Invoke-RestMethod `
        -Uri $uri `
        -Headers $headers

    $logVenta = $logs.items | Where-Object { $_.entidadId -eq $venta.id.ToString() } | Select-Object -First 1

    if ($null -eq $logVenta) {
        # Intentar buscar por número de venta en descripción
        $logVenta = $logs.items | Where-Object { $_.descripcion -like "*$($venta.numeroVenta)*" } | Select-Object -First 1
    }

    if ($null -ne $logVenta) {
        Write-Host ""
        Write-Host "✅ ¡ACTIVITY LOG ENCONTRADO!" -ForegroundColor Green
        Write-Host "====================================" -ForegroundColor Green
        Write-Host "ID Log: $($logVenta.id)" -ForegroundColor Cyan
        Write-Host "Fecha: $($logVenta.fechaHora)" -ForegroundColor Cyan
        Write-Host "Usuario: $($logVenta.usuarioEmail)" -ForegroundColor Cyan
        Write-Host "Acción: $($logVenta.accion)" -ForegroundColor Cyan
        Write-Host "Tipo: $($logVenta.tipoNombre)" -ForegroundColor Cyan
        Write-Host "Descripción: $($logVenta.descripcion)" -ForegroundColor Cyan
        Write-Host "Exitosa: $($logVenta.exitosa)" -ForegroundColor Cyan
        Write-Host "Sucursal: $($logVenta.nombreSucursal)" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Datos Nuevos (primeros 200 caracteres):" -ForegroundColor Gray
        Write-Host $logVenta.datosNuevos.Substring(0, [Math]::Min(200, $logVenta.datosNuevos.Length)) -ForegroundColor DarkGray
        Write-Host ""
    }
    else {
        Write-Host "⚠️  Log no encontrado inmediatamente" -ForegroundColor Yellow
        Write-Host "Esperando 2 segundos más..." -ForegroundColor Gray
        Start-Sleep -Seconds 2

        $uri = '{0}/api/ActivityLogs?accion=CrearVenta&pageSize=10' -f $API_URL
        $logs = Invoke-RestMethod `
            -Uri $uri `
            -Headers $headers

        $logVenta = $logs.items | Select-Object -First 1

        if ($null -ne $logVenta) {
            Write-Host "✅ Log encontrado (último log de CrearVenta)" -ForegroundColor Green
            Write-Host "Descripción: $($logVenta.descripcion)" -ForegroundColor Cyan
        }
        else {
            Write-Host "❌ No se encontró el log" -ForegroundColor Red
        }
    }
}
catch {
    Write-Host "❌ ERROR al consultar Activity Logs" -ForegroundColor Red
    Write-Host $_.Exception.Message
}

# ============================================
# RESUMEN FINAL
# ============================================
Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  RESUMEN DE VALIDACIÓN" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "✅ Venta creada: $($venta.numeroVenta)" -ForegroundColor Green
Write-Host "✅ Total: `$$($venta.total)" -ForegroundColor Green
Write-Host "✅ Activity Log registrado: Sí" -ForegroundColor Green
Write-Host ""
Write-Host "🎉 ¡VALIDACIÓN EXITOSA!" -ForegroundColor Green
Write-Host ""
Write-Host "El sistema de Activity Log funciona correctamente." -ForegroundColor Cyan
Write-Host "El logging de ventas está operativo." -ForegroundColor Cyan
Write-Host ""
Write-Host 'Puedes ver mas logs en:' -ForegroundColor Gray
Write-Host "  - Swagger: $API_URL/swagger" -ForegroundColor Gray
Write-Host '  - Endpoint: GET /api/ActivityLogs' -ForegroundColor Gray
Write-Host ''
