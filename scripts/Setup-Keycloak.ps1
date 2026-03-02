# Setup Keycloak para SincoPos
Write-Host "Configurando Keycloak..." -ForegroundColor Cyan

# Configurar credenciales
Write-Host "1. Configurando credenciales admin..." -ForegroundColor Yellow
docker exec sincopos-keycloak /opt/keycloak/bin/kcadm.sh config credentials `
  --server http://localhost:8080 `
  --realm master `
  --user admin `
  --password admin

# Crear realm
Write-Host "2. Creando realm 'sincopos'..." -ForegroundColor Yellow
docker exec sincopos-keycloak /opt/keycloak/bin/kcadm.sh create realms `
  -s realm=sincopos `
  -s enabled=true `
  -s displayName=SincoPOS

# Crear roles
Write-Host "3. Creando roles..." -ForegroundColor Yellow
docker exec sincopos-keycloak /opt/keycloak/bin/kcadm.sh create roles -r sincopos -s name=admin
docker exec sincopos-keycloak /opt/keycloak/bin/kcadm.sh create roles -r sincopos -s name=supervisor
docker exec sincopos-keycloak /opt/keycloak/bin/kcadm.sh create roles -r sincopos -s name=cajero
docker exec sincopos-keycloak /opt/keycloak/bin/kcadm.sh create roles -r sincopos -s name=vendedor

# Crear cliente
Write-Host "4. Creando cliente 'pos-api'..." -ForegroundColor Yellow
docker exec sincopos-keycloak /opt/keycloak/bin/kcadm.sh create clients -r sincopos `
  -s clientId=pos-api `
  -s enabled=true `
  -s publicClient=true `
  -s directAccessGrantsEnabled=true `
  -s 'redirectUris=["*"]'

# Crear usuario admin
Write-Host "5. Creando usuario admin@sincopos.com..." -ForegroundColor Yellow
docker exec sincopos-keycloak /opt/keycloak/bin/kcadm.sh create users -r sincopos `
  -s username=admin@sincopos.com `
  -s email=admin@sincopos.com `
  -s enabled=true `
  -s emailVerified=true

# Establecer contraseña
docker exec sincopos-keycloak /opt/keycloak/bin/kcadm.sh set-password -r sincopos `
  --username admin@sincopos.com `
  --new-password Admin123!

# Asignar rol admin
$userId = docker exec sincopos-keycloak /opt/keycloak/bin/kcadm.sh get users -r sincopos --fields id,username | ConvertFrom-Json | Where-Object { $_.username -eq "admin@sincopos.com" } | Select-Object -ExpandProperty id
docker exec sincopos-keycloak /opt/keycloak/bin/kcadm.sh add-roles -r sincopos --uid $userId --rolename admin

Write-Host ""
Write-Host "Keycloak configurado exitosamente!" -ForegroundColor Green
Write-Host ""
Write-Host "Credenciales de prueba:" -ForegroundColor Cyan
Write-Host "  Usuario: admin@sincopos.com" -ForegroundColor White
Write-Host "  Password: Admin123!" -ForegroundColor White
Write-Host ""
