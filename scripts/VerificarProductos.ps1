# Script para verificar productos

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

Write-Host "Consultando productos..." -ForegroundColor Yellow

try {
    $productos = Invoke-RestMethod -Uri "http://localhost:5086/api/Productos" -Headers $headers

    Write-Host ""
    Write-Host "Total de productos encontrados: $($productos.Count)" -ForegroundColor Cyan
    Write-Host ""

    if ($productos.Count -eq 0) {
        Write-Host "No hay productos. Creando uno..." -ForegroundColor Yellow

        $productoNuevo = @{
            nombre = "Producto Test Activity Log"
            codigoBarras = "TEST-AL-001"
            categoriaId = 1
            activo = $true
            gravado = $true
            porcentajeImpuesto = 0.19
        } | ConvertTo-Json

        try {
            $productoCreado = Invoke-RestMethod -Uri "http://localhost:5086/api/Productos" -Method Post -Headers $headers -Body $productoNuevo
            Write-Host "Producto creado exitosamente!" -ForegroundColor Green
            Write-Host "ID: $($productoCreado.id)" -ForegroundColor Cyan
            Write-Host "Nombre: $($productoCreado.nombre)" -ForegroundColor Cyan
        }
        catch {
            Write-Host "Error al crear producto:" -ForegroundColor Red
            Write-Host $_.Exception.Message -ForegroundColor Red
        }
    }
    else {
        foreach ($producto in $productos | Select-Object -First 5) {
            Write-Host "Producto ID: $($producto.id)" -ForegroundColor Cyan
            Write-Host "  Nombre: $($producto.nombre)" -ForegroundColor Gray
            Write-Host "  Codigo: $($producto.codigoBarras)" -ForegroundColor Gray
            Write-Host "  Activo: $($producto.activo)" -ForegroundColor Gray
            Write-Host ""
        }

        if ($productos.Count -gt 5) {
            Write-Host "... y $($productos.Count - 5) productos mas" -ForegroundColor Gray
        }
    }
}
catch {
    Write-Host "Error al consultar productos:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}
