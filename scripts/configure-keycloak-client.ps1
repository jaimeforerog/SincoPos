$base = "http://localhost:8080"

# 1. Token admin
$body = "client_id=admin-cli&username=admin&password=admin&grant_type=password"
$token = (Invoke-RestMethod -Uri "$base/realms/master/protocol/openid-connect/token" -Method Post -ContentType "application/x-www-form-urlencoded" -Body $body).access_token
$headers = @{ Authorization = "Bearer $token"; "Content-Type" = "application/json" }
Write-Host "Token OK" -ForegroundColor Green

# 2. Crear (o actualizar) cliente sincopos-frontend
$clients = Invoke-RestMethod -Uri "$base/admin/realms/sincopos/clients?max=50" -Headers $headers
$existing = $clients | Where-Object { $_.clientId -eq "sincopos-frontend" } | Select-Object -First 1

$clientDef = @{
    clientId = "sincopos-frontend"
    name = "SincoPos Frontend"
    enabled = $true
    publicClient = $true
    standardFlowEnabled = $true
    implicitFlowEnabled = $false
    directAccessGrantsEnabled = $false
    serviceAccountsEnabled = $false
    redirectUris = @(
        "http://localhost:5173/*",
        "http://localhost:5174/*",
        "http://localhost:5175/*",
        "http://localhost:5176/*",
        "http://localhost:5177/*"
    )
    webOrigins = @(
        "http://localhost:5173",
        "http://localhost:5174",
        "http://localhost:5175",
        "http://localhost:5176",
        "http://localhost:5177"
    )
    attributes = @{ "pkce.code.challenge.method" = "S256" }
} | ConvertTo-Json -Depth 5

if ($existing) {
    Write-Host "Actualizando sincopos-frontend (id=$($existing.id))..." -ForegroundColor Yellow
    Invoke-RestMethod -Uri "$base/admin/realms/sincopos/clients/$($existing.id)" -Method Put -Headers $headers -Body $clientDef
    Write-Host "Actualizado OK" -ForegroundColor Green
} else {
    Write-Host "Creando sincopos-frontend..." -ForegroundColor Yellow
    Invoke-RestMethod -Uri "$base/admin/realms/sincopos/clients" -Method Post -Headers $headers -Body $clientDef
    Write-Host "Creado OK" -ForegroundColor Green
}

# 3. Verificar usuarios disponibles en sincopos
Write-Host "`n=== Usuarios en realm sincopos ===" -ForegroundColor Cyan
$users = Invoke-RestMethod -Uri "$base/admin/realms/sincopos/users?max=20" -Headers $headers
$users | ForEach-Object { Write-Host " - $($_.username) | enabled=$($_.enabled)" }

# 4. Verificar roles en sincopos
Write-Host "`n=== Roles en realm sincopos ===" -ForegroundColor Cyan
$roles = Invoke-RestMethod -Uri "$base/admin/realms/sincopos/roles" -Headers $headers
$roles | ForEach-Object { Write-Host " - $($_.name)" }
