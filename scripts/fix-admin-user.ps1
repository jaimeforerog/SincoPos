$base = "http://localhost:8080"
$body = "client_id=admin-cli&username=admin&password=admin&grant_type=password"
$token = (Invoke-RestMethod -Uri "$base/realms/master/protocol/openid-connect/token" -Method Post -ContentType "application/x-www-form-urlencoded" -Body $body).access_token
$headers = @{ Authorization = "Bearer $token"; "Content-Type" = "application/json" }

# Buscar el usuario admin por username
$adminUser = (Invoke-RestMethod -Uri "$base/admin/realms/sincopos/users?username=admin&exact=true" -Headers $headers)[0]
Write-Host "Usuario admin encontrado: $($adminUser.id) | $($adminUser.email)"

# Asignar email y habilitarlo
$update = @{ email = "admin@sincopos.com"; emailVerified = $true; enabled = $true } | ConvertTo-Json
Invoke-RestMethod -Uri "$base/admin/realms/sincopos/users/$($adminUser.id)" -Method Put -Headers $headers -Body $update | Out-Null

# Establecer password
$pwd = @{ type = "password"; value = "Admin123!"; temporary = $false } | ConvertTo-Json
Invoke-RestMethod -Uri "$base/admin/realms/sincopos/users/$($adminUser.id)/reset-password" -Method Put -Headers $headers -Body $pwd | Out-Null
Write-Host "Password establecida: Admin123!" -ForegroundColor Green

# Asignar rol admin
$roleObj = Invoke-RestMethod -Uri "$base/admin/realms/sincopos/roles/admin" -Headers $headers
$roleArray = ConvertTo-Json @($roleObj) -Compress
try {
    Invoke-RestMethod -Uri "$base/admin/realms/sincopos/users/$($adminUser.id)/role-mappings/realm" -Method Post -Headers $headers -Body $roleArray | Out-Null
    Write-Host "Rol 'admin' asignado" -ForegroundColor Green
} catch {
    Write-Host "Rol admin ya asignado (OK)" -ForegroundColor Yellow
}

Write-Host "`n=== CONFIGURACION COMPLETA ===" -ForegroundColor Green
Write-Host "URL Keycloak:  http://localhost:8080" -ForegroundColor Cyan
Write-Host "Realm:         sincopos" -ForegroundColor Cyan
Write-Host "Cliente:       sincopos-frontend" -ForegroundColor Cyan
Write-Host ""
Write-Host "Usuarios de prueba:" -ForegroundColor White
Write-Host "  admin@sincopos.com      / Admin123!" -ForegroundColor Yellow
Write-Host "  supervisor@sincopos.com / Supervisor123!" -ForegroundColor Yellow
Write-Host "  cajero@sincopos.com     / Cajero123!" -ForegroundColor Yellow
Write-Host "  vendedor@sincopos.com   / Vendedor123!" -ForegroundColor Yellow
