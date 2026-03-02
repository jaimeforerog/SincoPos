# Script completo: Probar LIFO (UEPS)
$ErrorActionPreference = "Stop"

$ApiUrl = "http://localhost:5086"
$headers = @{ "Content-Type" = "application/json" }
$ProductoId = "11111111-1111-1111-1111-111111111111"

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  PRUEBA LIFO/UEPS (Ultimo en Entrar, Primero en Salir)" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

# ============================================================
# PASO 1: CAMBIAR METODO DE COSTEO A UEPS (LIFO)
# ============================================================
Write-Host "[1/6] Cambiando metodo de costeo a UEPS (LIFO)..." -ForegroundColor Yellow

# Necesitamos cambiar directamente en la base de datos
Write-Host "  Ejecutando UPDATE en PostgreSQL..." -ForegroundColor Cyan
$updateResult = & "C:\Program Files\PostgreSQL\16\bin\psql.exe" -h localhost -U postgres -d sincopos -c "UPDATE public.sucursales SET metodo_costeo = 2 WHERE `"Id`" = 1; SELECT metodo_costeo FROM public.sucursales WHERE `"Id`" = 1;" 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "  OK - Sucursal configurada en UEPS (LIFO)" -ForegroundColor Green
}
else {
    Write-Host "  ERROR: No se pudo cambiar metodo de costeo" -ForegroundColor Red
    Write-Host "  Ejecuta manualmente: UPDATE public.sucursales SET metodo_costeo = 2 WHERE `"Id`" = 1;" -ForegroundColor Yellow
    exit 1
}

Start-Sleep -Seconds 2

# ============================================================
# PASO 2: CREAR LOTES CON DIFERENTES COSTOS (LIFO)
# ============================================================
Write-Host "[2/6] Creando lotes de inventario con diferentes costos..." -ForegroundColor Yellow

# Lote 1: Mas antiguo - debe salir ULTIMO en LIFO
Write-Host "  [2.1] Lote 1: 50 unidades @ `$25 (MAS ANTIGUO - sale ULTIMO)..." -ForegroundColor Cyan
$lote1 = @{
    productoId = $ProductoId
    sucursalId = 1
    cantidad = 50
    costoUnitario = 25.00
    referencia = "LIFO-LOTE-1-ANTIGUO"
    observaciones = "Lote mas antiguo - debe salir ULTIMO en LIFO"
} | ConvertTo-Json

try {
    $resultado1 = Invoke-RestMethod -Uri "$ApiUrl/api/Inventario/entrada" -Method Post -Headers $headers -Body $lote1
    Write-Host "    OK - Stock: $($resultado1.stockActual), Costo Prom: `$$($resultado1.costoPromedio)" -ForegroundColor Green
    Start-Sleep -Seconds 1
}
catch {
    Write-Host "    ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Lote 2: Intermedio
Write-Host "  [2.2] Lote 2: 50 unidades @ `$30 (INTERMEDIO)..." -ForegroundColor Cyan
$lote2 = @{
    productoId = $ProductoId
    sucursalId = 1
    cantidad = 50
    costoUnitario = 30.00
    referencia = "LIFO-LOTE-2-MEDIO"
    observaciones = "Lote intermedio - debe salir segundo en LIFO"
} | ConvertTo-Json

try {
    $resultado2 = Invoke-RestMethod -Uri "$ApiUrl/api/Inventario/entrada" -Method Post -Headers $headers -Body $lote2
    Write-Host "    OK - Stock: $($resultado2.stockActual), Costo Prom: `$$($resultado2.costoPromedio)" -ForegroundColor Green
    Start-Sleep -Seconds 1
}
catch {
    Write-Host "    ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Lote 3: Mas reciente - debe salir PRIMERO en LIFO
Write-Host "  [2.3] Lote 3: 50 unidades @ `$35 (MAS RECIENTE - sale PRIMERO)..." -ForegroundColor Cyan
$lote3 = @{
    productoId = $ProductoId
    sucursalId = 1
    cantidad = 50
    costoUnitario = 35.00
    referencia = "LIFO-LOTE-3-RECIENTE"
    observaciones = "Lote mas reciente - debe salir PRIMERO en LIFO"
} | ConvertTo-Json

try {
    $resultado3 = Invoke-RestMethod -Uri "$ApiUrl/api/Inventario/entrada" -Method Post -Headers $headers -Body $lote3
    Write-Host "    OK - Stock: $($resultado3.stockActual), Costo Prom: `$$($resultado3.costoPromedio)" -ForegroundColor Green
    Start-Sleep -Seconds 2
}
catch {
    Write-Host "    ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "  Total ingresado: 150 unidades (50@`$25 + 50@`$30 + 50@`$35)" -ForegroundColor Cyan
Write-Host ""

# ============================================================
# PASO 3: VERIFICAR CAJA ABIERTA
# ============================================================
Write-Host "[3/6] Verificando caja abierta..." -ForegroundColor Yellow
try {
    $cajas = Invoke-RestMethod -Uri "$ApiUrl/api/Cajas?sucursalId=1" -Headers $headers
    $cajaAbierta = $cajas | Where-Object { $_.estado -eq "Abierta" } | Select-Object -First 1

    if ($null -eq $cajaAbierta) {
        Write-Host "  Abriendo caja..." -ForegroundColor Yellow
        $cajaCerrada = $cajas | Where-Object { $_.estado -eq "Cerrada" -and $_.activa } | Select-Object -First 1

        if ($null -eq $cajaCerrada) {
            Write-Host "  ERROR: No hay cajas disponibles" -ForegroundColor Red
            exit 1
        }

        $abrirBody = @{ montoApertura = 100 } | ConvertTo-Json
        Invoke-RestMethod -Uri "$ApiUrl/api/Cajas/$($cajaCerrada.id)/abrir" -Method Post -Headers $headers -Body $abrirBody | Out-Null
        Start-Sleep -Seconds 1

        $cajas = Invoke-RestMethod -Uri "$ApiUrl/api/Cajas?sucursalId=1" -Headers $headers
        $cajaAbierta = $cajas | Where-Object { $_.estado -eq "Abierta" } | Select-Object -First 1
    }

    Write-Host "  OK - Caja: $($cajaAbierta.nombre) (ID: $($cajaAbierta.id))" -ForegroundColor Green
}
catch {
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# ============================================================
# PASO 4: CREAR VENTAS Y VERIFICAR LIFO
# ============================================================
Write-Host "[4/6] Creando ventas para probar LIFO..." -ForegroundColor Yellow
Write-Host ""

# Venta 1: 30 unidades - debe consumir 30 del Lote 3 @ $35 (mas reciente)
Write-Host "  [4.1] Venta 1: 30 unidades" -ForegroundColor Cyan
Write-Host "        Esperado: Costo $35.00 (del Lote 3 mas reciente)" -ForegroundColor White
$venta1Body = @{
    sucursalId = 1
    cajaId = $cajaAbierta.id
    metodoPago = 0
    montoPagado = 1500
    observaciones = "LIFO Test - Venta 1"
    lineas = @(@{
        productoId = $ProductoId
        cantidad = 30
        descuento = 0
    })
} | ConvertTo-Json -Depth 5

try {
    $venta1 = Invoke-RestMethod -Uri "$ApiUrl/api/Ventas" -Method Post -Headers $headers -Body $venta1Body
    Write-Host "        OK - $($venta1.numeroVenta), Total: `$$($venta1.total)" -ForegroundColor Green

    $detalle1 = Invoke-RestMethod -Uri "$ApiUrl/api/Ventas/$($venta1.id)" -Headers $headers
    $costoVenta1 = $detalle1.detalles[0].costoUnitario
    Write-Host "        Costo Real: `$$costoVenta1" -ForegroundColor $(if ($costoVenta1 -eq 35) { "Green" } else { "Red" })

    Start-Sleep -Seconds 1
}
catch {
    Write-Host "        ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Venta 2: 40 unidades - debe consumir 20 del Lote 3 @ $35 + 20 del Lote 2 @ $30 = promedio $32.50
Write-Host ""
Write-Host "  [4.2] Venta 2: 40 unidades" -ForegroundColor Cyan
Write-Host "        Esperado: Costo $32.50 (20 del Lote 3 @ `$35 + 20 del Lote 2 @ `$30)" -ForegroundColor White
$venta2Body = @{
    sucursalId = 1
    cajaId = $cajaAbierta.id
    metodoPago = 0
    montoPagado = 2000
    observaciones = "LIFO Test - Venta 2"
    lineas = @(@{
        productoId = $ProductoId
        cantidad = 40
        descuento = 0
    })
} | ConvertTo-Json -Depth 5

try {
    $venta2 = Invoke-RestMethod -Uri "$ApiUrl/api/Ventas" -Method Post -Headers $headers -Body $venta2Body
    Write-Host "        OK - $($venta2.numeroVenta), Total: `$$($venta2.total)" -ForegroundColor Green

    $detalle2 = Invoke-RestMethod -Uri "$ApiUrl/api/Ventas/$($venta2.id)" -Headers $headers
    $costoVenta2 = $detalle2.detalles[0].costoUnitario
    Write-Host "        Costo Real: `$$costoVenta2" -ForegroundColor $(if ($costoVenta2 -ge 32 -and $costoVenta2 -le 33) { "Green" } else { "Red" })

    Start-Sleep -Seconds 1
}
catch {
    Write-Host "        ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Venta 3: 50 unidades - debe consumir 30 del Lote 2 @ $30 + 20 del Lote 1 @ $25 = promedio $29.00
Write-Host ""
Write-Host "  [4.3] Venta 3: 50 unidades" -ForegroundColor Cyan
Write-Host "        Esperado: Costo $29.00 (30 del Lote 2 @ `$30 + 20 del Lote 1 @ `$25)" -ForegroundColor White
$venta3Body = @{
    sucursalId = 1
    cajaId = $cajaAbierta.id
    metodoPago = 0
    montoPagado = 2500
    observaciones = "LIFO Test - Venta 3"
    lineas = @(@{
        productoId = $ProductoId
        cantidad = 50
        descuento = 0
    })
} | ConvertTo-Json -Depth 5

try {
    $venta3 = Invoke-RestMethod -Uri "$ApiUrl/api/Ventas" -Method Post -Headers $headers -Body $venta3Body
    Write-Host "        OK - $($venta3.numeroVenta), Total: `$$($venta3.total)" -ForegroundColor Green

    $detalle3 = Invoke-RestMethod -Uri "$ApiUrl/api/Ventas/$($venta3.id)" -Headers $headers
    $costoVenta3 = $detalle3.detalles[0].costoUnitario
    Write-Host "        Costo Real: `$$costoVenta3" -ForegroundColor $(if ($costoVenta3 -ge 28.5 -and $costoVenta3 -le 29.5) { "Green" } else { "Red" })

    Start-Sleep -Seconds 2
}
catch {
    Write-Host "        ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# ============================================================
# PASO 5: VERIFICAR LOTES RESTANTES
# ============================================================
Write-Host ""
Write-Host "[5/6] Verificando lotes restantes..." -ForegroundColor Yellow

try {
    $lotesResponse = Invoke-RestMethod -Uri "$ApiUrl/api/Inventario/lotes?productoId=$ProductoId&sucursalId=1" -Headers $headers

    Write-Host ""
    Write-Host "  Lotes despues de las ventas:" -ForegroundColor Cyan
    foreach ($lote in $lotesResponse | Sort-Object fechaEntrada) {
        $consumido = $lote.cantidadInicial - $lote.cantidadDisponible
        $color = if ($lote.cantidadDisponible -eq 0) { "Red" } elseif ($consumido -gt 0) { "Yellow" } else { "White" }
        Write-Host "    - $($lote.referencia): Inicial=$($lote.cantidadInicial), Disponible=$($lote.cantidadDisponible), Consumido=$consumido @ `$$($lote.costoUnitario)" -ForegroundColor $color
    }
}
catch {
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

# ============================================================
# PASO 6: RESUMEN
# ============================================================
Write-Host ""
Write-Host "========================================================" -ForegroundColor Green
Write-Host "  PRUEBA LIFO COMPLETADA                               " -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
Write-Host ""
Write-Host "RESUMEN:" -ForegroundColor Cyan
Write-Host "  Lotes creados: 50@`$25 + 50@`$30 + 50@`$35 = 150 unidades" -ForegroundColor White
Write-Host "  Ventas: 30 + 40 + 50 = 120 unidades consumidas" -ForegroundColor White
Write-Host "  Restante esperado: 30 unidades (30 del Lote 1 @ `$25)" -ForegroundColor White
Write-Host ""
Write-Host "COSTOS ESPERADOS (LIFO):" -ForegroundColor Cyan
Write-Host "  Venta 1 (30 u): `$35.00 (todo del Lote 3 - mas reciente)" -ForegroundColor White
Write-Host "  Venta 2 (40 u): `$32.50 (20 del Lote 3 + 20 del Lote 2)" -ForegroundColor White
Write-Host "  Venta 3 (50 u): `$29.00 (30 del Lote 2 + 20 del Lote 1)" -ForegroundColor White
Write-Host ""
Write-Host "COSTOS REALES:" -ForegroundColor Cyan
Write-Host "  Venta 1: `$$costoVenta1" -ForegroundColor $(if ($costoVenta1 -eq 35) { "Green" } else { "Red" })
Write-Host "  Venta 2: `$$costoVenta2" -ForegroundColor $(if ($costoVenta2 -ge 32 -and $costoVenta2 -le 33) { "Green" } else { "Red" })
Write-Host "  Venta 3: `$$costoVenta3" -ForegroundColor $(if ($costoVenta3 -ge 28.5 -and $costoVenta3 -le 29.5) { "Green" } else { "Red" })
Write-Host ""

# Verificacion final
$success = ($costoVenta1 -eq 35) -and ($costoVenta2 -ge 32 -and $costoVenta2 -le 33) -and ($costoVenta3 -ge 28.5 -and $costoVenta3 -le 29.5)

if ($success) {
    Write-Host "========================================================" -ForegroundColor Green
    Write-Host "  OK - LIFO FUNCIONANDO CORRECTAMENTE                  " -ForegroundColor Green
    Write-Host "========================================================" -ForegroundColor Green
}
else {
    Write-Host "========================================================" -ForegroundColor Red
    Write-Host "  ERROR - LIFO NO ESTA FUNCIONANDO COMO ESPERADO       " -ForegroundColor Red
    Write-Host "========================================================" -ForegroundColor Red
    Write-Host "Ver documentacion en PROYECTO_SINCOPOS.md" -ForegroundColor Yellow
}

Write-Host ""
