# Crear usuario de prueba en Keycloak
Write-Host "Creando usuario de prueba..." -ForegroundColor Cyan

# Configurar credenciales
docker exec sincopos-keycloak /opt/keycloak/bin/kcadm.sh config credentials --server http://localhost:8080 --realm master --user admin --password admin

# Crear usuario
Write-Host "1. Creando usuario testadmin..." -ForegroundColor Yellow
docker exec sincopos-keycloak /opt/keycloak/bin/kcadm.sh create users -r sincopos `
  -s username=testadmin `
  -s enabled=true `
  -s email=testadmin@sincopos.com `
  -s emailVerified=true

# Establecer contraseña
Write-Host "2. Estableciendo contraseña..." -ForegroundColor Yellow
docker exec sincopos-keycloak /opt/keycloak/bin/kcadm.sh set-password -r sincopos `
  --username testadmin `
  --new-password Test123

# Asignar rol
Write-Host "3. Asignando rol admin..." -ForegroundColor Yellow
docker exec sincopos-keycloak /opt/keycloak/bin/kcadm.sh add-roles -r sincopos `
  --uusername testadmin `
  --rolename admin

Write-Host ""
Write-Host "Usuario creado exitosamente!" -ForegroundColor Green
Write-Host "Username: testadmin" -ForegroundColor Cyan
Write-Host "Password: Test123" -ForegroundColor Cyan
Write-Host ""

# Probar autenticación
Write-Host "Probando autenticación..." -ForegroundColor Yellow
$response = curl -s -X POST "http://localhost:8080/realms/sincopos/protocol/openid-connect/token" `
  -d "client_id=pos-api" `
  -d "grant_type=password" `
  -d "username=testadmin" `
  -d "password=Test123"

if ($response -like "*access_token*") {
    Write-Host "Autenticación exitosa!" -ForegroundColor Green
} else {
    Write-Host "Error en autenticación:" -ForegroundColor Red
    Write-Host $response -ForegroundColor Yellow
}
