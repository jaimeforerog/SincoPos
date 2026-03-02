#!/bin/bash

# Script para inicializar Keycloak con la configuración base de SincoPos
# Este script debe ejecutarse DESPUÉS de que Keycloak esté corriendo

set -e

KEYCLOAK_URL="http://localhost:8080"
ADMIN_USER="admin"
ADMIN_PASS="admin"
REALM_NAME="sincopos"
CLIENT_ID="pos-api"

echo "🔐 Iniciando configuración de Keycloak para SincoPos..."

# Esperar a que Keycloak esté disponible
echo "⏳ Esperando a que Keycloak esté disponible..."
until curl -sf ${KEYCLOAK_URL}/health/ready > /dev/null; do
    echo "   Keycloak aún no está listo, esperando 5 segundos..."
    sleep 5
done
echo "✅ Keycloak está disponible"

# Ejecutar comandos dentro del contenedor
docker exec sincopos-keycloak bash << 'COMMANDS'

echo "🔑 Configurando credenciales de admin..."
/opt/keycloak/bin/kcadm.sh config credentials \
  --server http://localhost:8080 \
  --realm master \
  --user admin \
  --password admin

echo "🌍 Creando realm 'sincopos'..."
/opt/keycloak/bin/kcadm.sh create realms \
  -s realm=sincopos \
  -s enabled=true \
  -s displayName="SincoPOS" \
  -s registrationAllowed=false \
  -s loginWithEmailAllowed=true \
  -s duplicateEmailsAllowed=false

echo "👥 Creando roles..."
/opt/keycloak/bin/kcadm.sh create roles -r sincopos \
  -s name=admin \
  -s description="Administrador del sistema - Acceso total"

/opt/keycloak/bin/kcadm.sh create roles -r sincopos \
  -s name=supervisor \
  -s description="Supervisor de sucursal - Gestión operativa"

/opt/keycloak/bin/kcadm.sh create roles -r sincopos \
  -s name=cajero \
  -s description="Cajero - Ventas y caja"

/opt/keycloak/bin/kcadm.sh create roles -r sincopos \
  -s name=vendedor \
  -s description="Vendedor - Solo ventas"

echo "🔧 Creando client 'pos-api'..."
/opt/keycloak/bin/kcadm.sh create clients -r sincopos \
  -s clientId=pos-api \
  -s enabled=true \
  -s publicClient=true \
  -s directAccessGrantsEnabled=true \
  -s standardFlowEnabled=true \
  -s implicitFlowEnabled=false \
  -s serviceAccountsEnabled=false \
  -s 'redirectUris=["http://localhost:5000/*","http://localhost:3000/*","https://*"]' \
  -s 'webOrigins=["*"]' \
  -s protocol=openid-connect

echo "👤 Creando usuario admin..."
/opt/keycloak/bin/kcadm.sh create users -r sincopos \
  -s username=admin@sincopos.com \
  -s email=admin@sincopos.com \
  -s emailVerified=true \
  -s enabled=true \
  -s firstName=Admin \
  -s lastName=Sistema

# Obtener ID del usuario admin
ADMIN_ID=$(/opt/keycloak/bin/kcadm.sh get users -r sincopos -q username=admin@sincopos.com --fields id --format csv --noquotes)

# Establecer contraseña
/opt/keycloak/bin/kcadm.sh set-password -r sincopos \
  --username admin@sincopos.com \
  --new-password Admin123!

# Asignar rol admin
/opt/keycloak/bin/kcadm.sh add-roles -r sincopos \
  --uusername admin@sincopos.com \
  --rolename admin

echo "👤 Creando usuario cajero..."
/opt/keycloak/bin/kcadm.sh create users -r sincopos \
  -s username=cajero@sincopos.com \
  -s email=cajero@sincopos.com \
  -s emailVerified=true \
  -s enabled=true \
  -s firstName=Juan \
  -s lastName=Cajero

/opt/keycloak/bin/kcadm.sh set-password -r sincopos \
  --username cajero@sincopos.com \
  --new-password Cajero123!

/opt/keycloak/bin/kcadm.sh add-roles -r sincopos \
  --uusername cajero@sincopos.com \
  --rolename cajero

echo "👤 Creando usuario vendedor..."
/opt/keycloak/bin/kcadm.sh create users -r sincopos \
  -s username=vendedor@sincopos.com \
  -s email=vendedor@sincopos.com \
  -s emailVerified=true \
  -s enabled=true \
  -s firstName=María \
  -s lastName=Vendedora

/opt/keycloak/bin/kcadm.sh set-password -r sincopos \
  --username vendedor@sincopos.com \
  --new-password Vendedor123!

/opt/keycloak/bin/kcadm.sh add-roles -r sincopos \
  --uusername vendedor@sincopos.com \
  --rolename vendedor

echo "👤 Creando usuario supervisor..."
/opt/keycloak/bin/kcadm.sh create users -r sincopos \
  -s username=supervisor@sincopos.com \
  -s email=supervisor@sincopos.com \
  -s emailVerified=true \
  -s enabled=true \
  -s firstName=Pedro \
  -s lastName=Supervisor

/opt/keycloak/bin/kcadm.sh set-password -r sincopos \
  --username supervisor@sincopos.com \
  --new-password Supervisor123!

/opt/keycloak/bin/kcadm.sh add-roles -r sincopos \
  --uusername supervisor@sincopos.com \
  --rolename supervisor

echo "✅ Configuración completada!"
COMMANDS

echo ""
echo "════════════════════════════════════════════════════════════"
echo "✅ Keycloak configurado exitosamente para SincoPos!"
echo "════════════════════════════════════════════════════════════"
echo ""
echo "🌐 URL: http://localhost:8080"
echo "👤 Admin Console: http://localhost:8080/admin"
echo ""
echo "📋 Realm: sincopos"
echo "🔧 Client ID: pos-api"
echo ""
echo "👥 Usuarios de prueba:"
echo "   - admin@sincopos.com / Admin123! (rol: admin)"
echo "   - supervisor@sincopos.com / Supervisor123! (rol: supervisor)"
echo "   - cajero@sincopos.com / Cajero123! (rol: cajero)"
echo "   - vendedor@sincopos.com / Vendedor123! (rol: vendedor)"
echo ""
echo "🧪 Probar obtener token:"
echo "   curl -X POST http://localhost:8080/realms/sincopos/protocol/openid-connect/token \\"
echo "     -d 'client_id=pos-api' \\"
echo "     -d 'username=admin@sincopos.com' \\"
echo "     -d 'password=Admin123!' \\"
echo "     -d 'grant_type=password'"
echo ""
echo "════════════════════════════════════════════════════════════"
