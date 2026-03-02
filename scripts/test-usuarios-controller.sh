#!/bin/bash

# Script de Prueba de UsuariosController - SincoPOS

echo "=========================================="
echo "   PRUEBA DE USUARIOS CONTROLLER"
echo "=========================================="
echo ""

API_URL="http://localhost:5086"
KEYCLOAK_URL="http://localhost:8080"
REALM="sincopos"
CLIENT_ID="pos-api"

# Colores
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

PASSED=0
FAILED=0

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  1. OBTENER TOKENS"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

echo "Obteniendo token de Admin..."
TOKEN_ADMIN=$(curl -s -X POST "$KEYCLOAK_URL/realms/$REALM/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "client_id=$CLIENT_ID" \
    -d "username=admin@sincopos.com" \
    -d "password=admin123" \
    -d "grant_type=password" | grep -o '"access_token":"[^"]*"' | cut -d'"' -f4)

if [ -n "$TOKEN_ADMIN" ]; then
    echo -e "${GREEN}✓${NC} Token Admin obtenido"
else
    echo -e "${RED}✗${NC} No se pudo obtener token de Admin"
    exit 1
fi

echo "Obteniendo token de Supervisor..."
TOKEN_SUPERVISOR=$(curl -s -X POST "$KEYCLOAK_URL/realms/$REALM/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "client_id=$CLIENT_ID" \
    -d "username=supervisor@sincopos.com" \
    -d "password=super123" \
    -d "grant_type=password" | grep -o '"access_token":"[^"]*"' | cut -d'"' -f4)

if [ -n "$TOKEN_SUPERVISOR" ]; then
    echo -e "${GREEN}✓${NC} Token Supervisor obtenido"
else
    echo -e "${RED}✗${NC} No se pudo obtener token de Supervisor"
    exit 1
fi

echo "Obteniendo token de Vendedor..."
TOKEN_VENDEDOR=$(curl -s -X POST "$KEYCLOAK_URL/realms/$REALM/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "client_id=$CLIENT_ID" \
    -d "username=vendedor@sincopos.com" \
    -d "password=vendedor123" \
    -d "grant_type=password" | grep -o '"access_token":"[^"]*"' | cut -d'"' -f4)

if [ -n "$TOKEN_VENDEDOR" ]; then
    echo -e "${GREEN}✓${NC} Token Vendedor obtenido"
else
    echo -e "${RED}✗${NC} No se pudo obtener token de Vendedor"
    exit 1
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  2. GET /api/usuarios/me - Perfil del usuario"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

echo "Admin obtiene su perfil..."
RESPONSE=$(curl -s -X GET "$API_URL/api/usuarios/me" \
    -H "Authorization: Bearer $TOKEN_ADMIN")

echo "$RESPONSE" | head -20

if echo "$RESPONSE" | grep -q "admin@sincopos.com"; then
    echo -e "${GREEN}✓ PASS${NC}: Admin obtuvo su perfil correctamente"
    ((PASSED++))
else
    echo -e "${RED}✗ FAIL${NC}: No se pudo obtener perfil de Admin"
    ((FAILED++))
fi

echo ""
echo "Supervisor obtiene su perfil..."
RESPONSE=$(curl -s -X GET "$API_URL/api/usuarios/me" \
    -H "Authorization: Bearer $TOKEN_SUPERVISOR")

if echo "$RESPONSE" | grep -q "supervisor@sincopos.com"; then
    echo -e "${GREEN}✓ PASS${NC}: Supervisor obtuvo su perfil correctamente"
    ((PASSED++))
else
    echo -e "${RED}✗ FAIL${NC}: No se pudo obtener perfil de Supervisor"
    ((FAILED++))
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  3. GET /api/usuarios - Listar usuarios"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

echo "Admin lista todos los usuarios..."
RESPONSE=$(curl -s -X GET "$API_URL/api/usuarios" \
    -H "Authorization: Bearer $TOKEN_ADMIN")

echo "$RESPONSE" | head -20

if echo "$RESPONSE" | grep -q "admin@sincopos.com"; then
    echo -e "${GREEN}✓ PASS${NC}: Admin puede listar usuarios"
    ((PASSED++))
else
    echo -e "${RED}✗ FAIL${NC}: Admin no puede listar usuarios"
    ((FAILED++))
fi

echo ""
echo "Supervisor lista todos los usuarios..."
RESPONSE=$(curl -s -X GET "$API_URL/api/usuarios" \
    -H "Authorization: Bearer $TOKEN_SUPERVISOR")

if echo "$RESPONSE" | grep -q "admin@sincopos.com"; then
    echo -e "${GREEN}✓ PASS${NC}: Supervisor puede listar usuarios"
    ((PASSED++))
else
    echo -e "${RED}✗ FAIL${NC}: Supervisor no puede listar usuarios"
    ((FAILED++))
fi

echo ""
echo "Vendedor intenta listar usuarios (debe fallar)..."
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X GET "$API_URL/api/usuarios" \
    -H "Authorization: Bearer $TOKEN_VENDEDOR")

if [ "$HTTP_CODE" == "403" ]; then
    echo -e "${GREEN}✓ PASS${NC}: Vendedor correctamente rechazado (403)"
    ((PASSED++))
else
    echo -e "${RED}✗ FAIL${NC}: Vendedor debería recibir 403, recibió $HTTP_CODE"
    ((FAILED++))
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  4. GET /api/usuarios/estadisticas"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

echo "Admin obtiene estadísticas..."
RESPONSE=$(curl -s -X GET "$API_URL/api/usuarios/estadisticas" \
    -H "Authorization: Bearer $TOKEN_ADMIN")

echo "$RESPONSE"

if echo "$RESPONSE" | grep -q "totalUsuarios"; then
    echo -e "${GREEN}✓ PASS${NC}: Admin obtuvo estadísticas correctamente"
    ((PASSED++))
else
    echo -e "${RED}✗ FAIL${NC}: No se pudieron obtener estadísticas"
    ((FAILED++))
fi

echo ""
echo "Supervisor intenta obtener estadísticas (debe fallar)..."
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X GET "$API_URL/api/usuarios/estadisticas" \
    -H "Authorization: Bearer $TOKEN_SUPERVISOR")

if [ "$HTTP_CODE" == "403" ]; then
    echo -e "${GREEN}✓ PASS${NC}: Supervisor correctamente rechazado (403)"
    ((PASSED++))
else
    echo -e "${RED}✗ FAIL${NC}: Supervisor debería recibir 403, recibió $HTTP_CODE"
    ((FAILED++))
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  5. GET /api/usuarios con filtros"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

echo "Filtrar usuarios por rol 'admin'..."
RESPONSE=$(curl -s -X GET "$API_URL/api/usuarios?rol=admin" \
    -H "Authorization: Bearer $TOKEN_ADMIN")

if echo "$RESPONSE" | grep -q "admin@sincopos.com"; then
    echo -e "${GREEN}✓ PASS${NC}: Filtro por rol funciona"
    ((PASSED++))
else
    echo -e "${RED}✗ FAIL${NC}: Filtro por rol no funciona"
    ((FAILED++))
fi

echo ""
echo "Buscar usuarios por email..."
RESPONSE=$(curl -s -X GET "$API_URL/api/usuarios?busqueda=admin" \
    -H "Authorization: Bearer $TOKEN_ADMIN")

if echo "$RESPONSE" | grep -q "admin@sincopos.com"; then
    echo -e "${GREEN}✓ PASS${NC}: Búsqueda por texto funciona"
    ((PASSED++))
else
    echo -e "${RED}✗ FAIL${NC}: Búsqueda por texto no funciona"
    ((FAILED++))
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  RESUMEN"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

echo -e "${GREEN}✓ Pruebas pasadas:${NC} $PASSED"
echo -e "${RED}✗ Pruebas fallidas:${NC} $FAILED"

echo ""

if [ $FAILED -eq 0 ]; then
    echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${GREEN}  ✅ TODAS LAS PRUEBAS PASARON${NC}"
    echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo ""
    echo "🎉 UsuariosController está funcionando correctamente!"
    echo ""
    exit 0
else
    echo -e "${RED}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${RED}  ⚠️  ALGUNAS PRUEBAS FALLARON${NC}"
    echo -e "${RED}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo ""
    exit 1
fi
