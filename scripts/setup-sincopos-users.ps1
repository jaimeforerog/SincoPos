$base = "http://localhost:8080"

$body = "client_id=admin-cli&username=admin&password=admin&grant_type=password"
$token = (Invoke-RestMethod -Uri "$base/realms/master/protocol/openid-connect/token" -Method Post -ContentType "application/x-www-form-urlencoded" -Body $body).access_token
$headers = @{ Authorization = "Bearer $token"; "Content-Type" = "application/json" }

function Create-Or-Update-User($username, $email, $password, $role) {
    $existing = Invoke-RestMethod -Uri "$base/admin/realms/sincopos/users?username=$username&exact=true" -Headers $headers

    $userDef = @{
        username = $username
        email = $email
        firstName = $role
        lastName = "SincoPos"
        enabled = $true
        emailVerified = $true
    } | ConvertTo-Json

    if ($existing.Count -gt 0) {
        $userId = $existing[0].id
        Write-Host "  Actualizando usuario $username..." -NoNewline
        Invoke-RestMethod -Uri "$base/admin/realms/sincopos/users/$userId" -Method Put -Headers $headers -Body $userDef | Out-Null
    } else {
        Write-Host "  Creando usuario $username..." -NoNewline
        $response = Invoke-WebRequest -Uri "$base/admin/realms/sincopos/users" -Method Post -Headers $headers -Body $userDef -UseBasicParsing
        $location = $response.Headers.Location
        $userId = $location.ToString().Split("/")[-1]
    }

    # Establecer password
    $pwd = @{ type = "password"; value = $password; temporary = $false } | ConvertTo-Json
    Invoke-RestMethod -Uri "$base/admin/realms/sincopos/users/$userId/reset-password" -Method Put -Headers $headers -Body $pwd | Out-Null

    # Asignar rol
    $roleObj = Invoke-RestMethod -Uri "$base/admin/realms/sincopos/roles/$role" -Headers $headers
    $roleArray = ConvertTo-Json @($roleObj) -Compress
    Invoke-RestMethod -Uri "$base/admin/realms/sincopos/users/$userId/role-mappings/realm" -Method Post -Headers $headers -Body $roleArray | Out-Null

    Write-Host " OK (rol: $role)" -ForegroundColor Green
    return $userId
}

Write-Host "=== Configurando usuarios en realm sincopos ===" -ForegroundColor Cyan
Create-Or-Update-User "admin@sincopos.com"      "admin@sincopos.com"      "Admin123!"      "admin"
Create-Or-Update-User "supervisor@sincopos.com" "supervisor@sincopos.com" "Supervisor123!" "supervisor"
Create-Or-Update-User "cajero@sincopos.com"     "cajero@sincopos.com"     "Cajero123!"     "cajero"
Create-Or-Update-User "vendedor@sincopos.com"   "vendedor@sincopos.com"   "Vendedor123!"   "vendedor"

Write-Host "`n=== Verificando usuarios ===" -ForegroundColor Cyan
$users = Invoke-RestMethod -Uri "$base/admin/realms/sincopos/users?max=20" -Headers $headers
$users | ForEach-Object { Write-Host " - $($_.username) | $($_.email) | enabled=$($_.enabled)" }

Write-Host "`n=== LISTO ===" -ForegroundColor Green
Write-Host "Puedes loguearte con:" -ForegroundColor White
Write-Host "  admin@sincopos.com / Admin123!" -ForegroundColor Yellow
Write-Host "  cajero@sincopos.com / Cajero123!" -ForegroundColor Yellow
