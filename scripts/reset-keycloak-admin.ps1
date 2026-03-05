$psql = "C:\Program Files\PostgreSQL\16\bin\psql.exe"
$env:PGPASSWORD = "postgrade"

Write-Host "=== Borrando usuario admin del master realm ===" -ForegroundColor Yellow

$deleteScript = @"
-- Borrar referencias del admin en master realm usando subquery
DELETE FROM user_role_mapping WHERE user_id IN (
  SELECT u.id::text FROM user_entity u
  JOIN realm r ON u.realm_id = r.id
  WHERE r.name = 'master' AND u.username = 'admin'
);
DELETE FROM user_required_action WHERE user_id IN (
  SELECT u.id::text FROM user_entity u
  JOIN realm r ON u.realm_id = r.id
  WHERE r.name = 'master' AND u.username = 'admin'
);
DELETE FROM user_attribute WHERE user_id IN (
  SELECT u.id::text FROM user_entity u
  JOIN realm r ON u.realm_id = r.id
  WHERE r.name = 'master' AND u.username = 'admin'
);
DELETE FROM credential WHERE user_id IN (
  SELECT u.id::text FROM user_entity u
  JOIN realm r ON u.realm_id = r.id
  WHERE r.name = 'master' AND u.username = 'admin'
);
DELETE FROM user_group_membership WHERE user_id IN (
  SELECT u.id::text FROM user_entity u
  JOIN realm r ON u.realm_id = r.id
  WHERE r.name = 'master' AND u.username = 'admin'
);
DELETE FROM user_entity WHERE id IN (
  SELECT u.id FROM user_entity u
  JOIN realm r ON u.realm_id = r.id
  WHERE r.name = 'master' AND u.username = 'admin'
);
"@

$deleteScript | & $psql -h localhost -U postgres -d keycloak 2>&1

Write-Host "=== Reiniciando Keycloak (espera 20s)... ===" -ForegroundColor Cyan
docker restart sincopos-keycloak
Start-Sleep 20

Write-Host "=== Probando login admin/admin ===" -ForegroundColor Cyan
$body = "client_id=admin-cli&username=admin&password=admin&grant_type=password"
try {
    $r = Invoke-RestMethod -Uri "http://localhost:8080/realms/master/protocol/openid-connect/token" -Method Post -ContentType "application/x-www-form-urlencoded" -Body $body
    Write-Host "LOGIN EXITOSO" -ForegroundColor Green
    Write-Host $r.access_token.Substring(0, 30)
} catch {
    Write-Host "Aun falla. Espera mas tiempo y vuelve a intentar admin/admin en http://localhost:8080/admin" -ForegroundColor Red
}
