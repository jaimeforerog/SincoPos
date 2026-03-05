# Verificar estado del sistema SincoPos
# Ejecutar con: .\verificar-sistema.ps1

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Verificación del Sistema SincoPos" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Verificar si el puerto 5086 está en uso
Write-Host "[1/5] Verificando puerto 5086..." -ForegroundColor Yellow
$portTest = Test-NetConnection -ComputerName localhost -Port 5086 -WarningAction SilentlyContinue

if ($portTest.TcpTestSucceeded) {
    Write-Host "✓ Backend está corriendo en puerto 5086" -ForegroundColor Green
} else {
    Write-Host "✗ Backend NO está corriendo" -ForegroundColor Red
    Write-Host "  → Ejecuta: cd POS.Api; dotnet run" -ForegroundColor Yellow
    exit 1
}

# 2. Verificar endpoint de sucursales
Write-Host "[2/5] Probando endpoint /api/sucursales..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "http://localhost:5086/api/sucursales" -Headers @{"Authorization"="Bearer dev-token-mock"} -ErrorAction Stop
    Write-Host "✓ Endpoint responde correctamente" -ForegroundColor Green
    Write-Host "  → Sucursales encontradas: $($response.Count)" -ForegroundColor Cyan

    if ($response.Count -gt 0) {
        Write-Host ""
        Write-Host "  Sucursales:" -ForegroundColor White
        foreach ($suc in $response) {
            Write-Host "    - ID: $($suc.id) | Nombre: $($suc.nombre) | Activo: $($suc.activo)" -ForegroundColor Gray
        }
    }
} catch {
    Write-Host "✗ Error al conectar con el endpoint" -ForegroundColor Red
    Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# 3. Verificar endpoint de cajas
Write-Host ""
Write-Host "[3/5] Probando endpoint /api/cajas..." -ForegroundColor Yellow
try {
    $cajas = Invoke-RestMethod -Uri "http://localhost:5086/api/cajas" -Headers @{"Authorization"="Bearer dev-token-mock"} -ErrorAction Stop
    Write-Host "✓ Endpoint de cajas responde" -ForegroundColor Green
    Write-Host "  → Cajas encontradas: $($cajas.Count)" -ForegroundColor Cyan
} catch {
    Write-Host "✗ Error al conectar con endpoint de cajas" -ForegroundColor Red
    Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
}

# 4. Verificar endpoint de productos
Write-Host ""
Write-Host "[4/5] Probando endpoint /api/productos..." -ForegroundColor Yellow
try {
    $productos = Invoke-RestMethod -Uri "http://localhost:5086/api/productos" -Headers @{"Authorization"="Bearer dev-token-mock"} -ErrorAction Stop
    Write-Host "✓ Endpoint de productos responde" -ForegroundColor Green
    Write-Host "  → Productos encontrados: $($productos.Count)" -ForegroundColor Cyan
} catch {
    Write-Host "✗ Error al conectar con endpoint de productos" -ForegroundColor Red
    Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
}

# 5. Verificar si el frontend está corriendo
Write-Host ""
Write-Host "[5/5] Verificando frontend (puerto 5173)..." -ForegroundColor Yellow
$frontendTest = Test-NetConnection -ComputerName localhost -Port 5173 -WarningAction SilentlyContinue

if ($frontendTest.TcpTestSucceeded) {
    Write-Host "✓ Frontend está corriendo en puerto 5173" -ForegroundColor Green
    Write-Host "  → URL: http://localhost:5173" -ForegroundColor Cyan
} else {
    Write-Host "⚠ Frontend NO está corriendo" -ForegroundColor Yellow
    Write-Host "  → Para iniciarlo: cd frontend; npm run dev" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Resumen" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($response.Count -eq 0) {
    Write-Host ""
    Write-Host "⚠ No hay sucursales creadas" -ForegroundColor Yellow
    Write-Host "  → Ejecuta: .\crear-sucursal.ps1" -ForegroundColor White
} else {
    Write-Host ""
    Write-Host "✓ Sistema listo para usar" -ForegroundColor Green
}

Write-Host ""
