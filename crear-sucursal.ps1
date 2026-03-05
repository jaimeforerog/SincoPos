# Crear sucursal de prueba
# Ejecutar con: .\crear-sucursal.ps1

Write-Host "Creando sucursal..." -ForegroundColor Yellow

$url = "http://localhost:5086/api/sucursales"
$headers = @{
    "Content-Type" = "application/json"
    "Authorization" = "Bearer dev-token-mock"
}

$body = @"
{
    "nombre": "Sucursal Principal",
    "direccion": "Calle 123 # 45-67",
    "codigoPais": "CO",
    "nombrePais": "Colombia",
    "ciudad": "Bogotá",
    "metodoCosteo": "PromedioPonderado"
}
"@

try {
    $resultado = Invoke-RestMethod -Uri $url -Method Post -Headers $headers -Body $body -ContentType "application/json"
    Write-Host "✓ Sucursal creada exitosamente!" -ForegroundColor Green
    Write-Host "ID: $($resultado.id)" -ForegroundColor Cyan
    Write-Host "Nombre: $($resultado.nombre)" -ForegroundColor Cyan
}
catch {
    Write-Host "✗ Error al crear sucursal" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Respuesta del servidor:" -ForegroundColor Yellow
    Write-Host $_.ErrorDetails.Message -ForegroundColor Red
}
