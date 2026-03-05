# Script de inicio para SincoPos
# Ejecutar con: .\iniciar-sincopos.ps1

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "   Iniciando SincoPos" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# 1. Iniciar Backend en una nueva ventana
Write-Host "[1/3] Iniciando Backend..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PSScriptRoot\POS.Api'; dotnet run"

# Esperar a que el backend esté listo
Write-Host "[2/3] Esperando a que el backend inicie..." -ForegroundColor Yellow
Start-Sleep -Seconds 8

# Verificar si el backend está corriendo
try {
    $response = Invoke-RestMethod -Uri "http://localhost:5086/api/sucursales" -Headers @{"Authorization"="Bearer dev-token-mock"} -ErrorAction Stop

    Write-Host "✓ Backend iniciado correctamente" -ForegroundColor Green

    # Verificar si hay sucursales
    if ($response.Count -eq 0) {
        Write-Host "[3/3] Creando sucursal de prueba..." -ForegroundColor Yellow

        $bodyData = @{
            nombre = "Sucursal Principal"
            direccion = "Calle 123 # 45-67"
            codigoPais = "CO"
            nombrePais = "Colombia"
            ciudad = "Bogotá"
            telefono = "3001234567"
            metodoCosteo = "PromedioPonderado"
        }

        $jsonBody = $bodyData | ConvertTo-Json

        $sucursal = Invoke-RestMethod -Uri "http://localhost:5086/api/sucursales" -Method Post -Headers @{"Content-Type"="application/json"; "Authorization"="Bearer dev-token-mock"} -Body $jsonBody

        Write-Host "✓ Sucursal creada: $($sucursal.nombre) (ID: $($sucursal.id))" -ForegroundColor Green
    }
    else {
        Write-Host "✓ Ya existen $($response.Count) sucursal(es)" -ForegroundColor Green
    }
}
catch {
    Write-Host "✗ Error: No se pudo conectar al backend" -ForegroundColor Red
    Write-Host "  Asegúrate de que el backend esté corriendo" -ForegroundColor Red
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "   SincoPos Backend Listo!" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Backend: http://localhost:5086" -ForegroundColor White
Write-Host "Frontend: Ejecuta 'npm run dev' en la carpeta frontend" -ForegroundColor White
Write-Host ""
Write-Host "Para iniciar el frontend:" -ForegroundColor Yellow
Write-Host "  cd frontend" -ForegroundColor White
Write-Host "  npm run dev" -ForegroundColor White
Write-Host ""
