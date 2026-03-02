# Script para verificar cajas existentes

$body = @{
    client_id = "pos-api"
    grant_type = "password"
    username = "admin@sincopos.com"
    password = "Admin123!"
}

$response = Invoke-RestMethod -Uri "http://localhost:8080/realms/sincopos/protocol/openid-connect/token" -Method Post -Body $body -ContentType "application/x-www-form-urlencoded"
$token = $response.access_token

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

Write-Host "Consultando cajas para sucursal 77..." -ForegroundColor Yellow

$cajas = Invoke-RestMethod -Uri "http://localhost:5086/api/Cajas?sucursalId=77" -Headers $headers

Write-Host ""
Write-Host "Total de cajas encontradas: $($cajas.Count)" -ForegroundColor Cyan
Write-Host ""

if ($cajas.Count -eq 0) {
    Write-Host "No hay cajas. Creando una..." -ForegroundColor Yellow

    $cajaNueva = @{
        nombre = "Caja Principal"
        sucursalId = 77
        activo = $true
    } | ConvertTo-Json

    try {
        $cajaCreada = Invoke-RestMethod -Uri "http://localhost:5086/api/Cajas" -Method Post -Headers $headers -Body $cajaNueva
        Write-Host "Caja creada exitosamente!" -ForegroundColor Green
        Write-Host "ID: $($cajaCreada.id)" -ForegroundColor Cyan
        Write-Host "Nombre: $($cajaCreada.nombre)" -ForegroundColor Cyan
        Write-Host "Estado: $($cajaCreada.estado)" -ForegroundColor Cyan
        Write-Host "Activo: $($cajaCreada.activo)" -ForegroundColor Cyan
    }
    catch {
        Write-Host "Error al crear caja:" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
    }
}
else {
    foreach ($caja in $cajas) {
        Write-Host "Caja ID: $($caja.id)" -ForegroundColor Cyan
        Write-Host "  Nombre: $($caja.nombre)" -ForegroundColor Gray
        Write-Host "  Estado: $($caja.estado)" -ForegroundColor Gray
        Write-Host "  Activo: $($caja.activo)" -ForegroundColor Gray
        Write-Host "  Sucursal: $($caja.sucursalId)" -ForegroundColor Gray
        Write-Host ""
    }
}
