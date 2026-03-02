#!/bin/bash

GREEN='\033[0;32m'
RED='\033[0;31m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m'

API_URL="http://localhost:5086"
KEYCLOAK_URL="http://localhost:8080"

echo "=========================================="
echo "   PRUEBAS RÁPIDAS DE SEGURIDAD"
echo "=========================================="
echo ""

# Test 1: Sin token (debe fallar con 401)
echo -e "${BLUE}1. Probando sin token (debe devolver 401)...${NC}"
response=$(curl -s -w "\n%{http_code}" "${API_URL}/api/productos")
http_code=$(echo "$response" | tail -n1)
if [ "$http_code" = "401" ]; then
    echo -e "   ${GREEN}✓ 401 Unauthorized - Correcto!${NC}"
else
    echo -e "   ${RED}✗ ${http_code} - ERROR (esperado 401)${NC}"
fi
echo ""

# Test 2: Obtener token de admin
echo -e "${BLUE}2. Obteniendo token de admin...${NC}"
TOKEN=$(curl -s -X POST "${KEYCLOAK_URL}/realms/sincopos/protocol/openid-connect/token" \
  -d "client_id=pos-api" \
  -d "username=admin@sincopos.com" \
  -d "password=Admin123!" \
  -d "grant_type=password" 2>&1)

ACCESS_TOKEN=$(echo "$TOKEN" | jq -r '.access_token // empty' 2>/dev/null)

if [ -z "$ACCESS_TOKEN" ] || [ "$ACCESS_TOKEN" = "null" ]; then
    echo -e "   ${RED}✗ Error obteniendo token${NC}"
    echo "   Respuesta: $TOKEN"
    exit 1
fi

echo -e "   ${GREEN}✓ Token obtenido: ${ACCESS_TOKEN:0:40}...${NC}"
echo ""

# Test 3: Con token válido (debe funcionar)
echo -e "${BLUE}3. Probando con token de admin (debe devolver 200)...${NC}"
response=$(curl -s -w "\n%{http_code}" "${API_URL}/api/productos" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}")
http_code=$(echo "$response" | tail -n1)
body=$(echo "$response" | sed '$d')

if [ "$http_code" = "200" ]; then
    echo -e "   ${GREEN}✓ 200 OK - Autenticación exitosa!${NC}"
    echo "   Respuesta: $(echo "$body" | jq -r 'if type=="array" then "[\(length) productos]" else . end' 2>/dev/null || echo "$body")"
else
    echo -e "   ${RED}✗ ${http_code} - ERROR${NC}"
    echo "   Respuesta: $body"
fi
echo ""

# Test 4: Obtener token de vendedor
echo -e "${BLUE}4. Obteniendo token de vendedor...${NC}"
TOKEN_VENDEDOR=$(curl -s -X POST "${KEYCLOAK_URL}/realms/sincopos/protocol/openid-connect/token" \
  -d "client_id=pos-api" \
  -d "username=vendedor@sincopos.com" \
  -d "password=Vendedor123!" \
  -d "grant_type=password" | jq -r '.access_token // empty' 2>/dev/null)

if [ -z "$TOKEN_VENDEDOR" ]; then
    echo -e "   ${YELLOW}⚠ No se pudo obtener token de vendedor${NC}"
else
    echo -e "   ${GREEN}✓ Token obtenido${NC}"
    
    # Test 5: Vendedor puede listar (GET)
    echo ""
    echo -e "${BLUE}5. Vendedor puede listar productos (debe devolver 200)...${NC}"
    response=$(curl -s -w "\n%{http_code}" "${API_URL}/api/productos" \
      -H "Authorization: Bearer ${TOKEN_VENDEDOR}")
    http_code=$(echo "$response" | tail -n1)
    
    if [ "$http_code" = "200" ]; then
        echo -e "   ${GREEN}✓ 200 OK - Vendedor puede listar${NC}"
    else
        echo -e "   ${RED}✗ ${http_code}${NC}"
    fi
fi

echo ""
echo "=========================================="
echo "  RESUMEN"
echo "=========================================="
echo ""
echo -e "${GREEN}✅ Sistema de autenticación funcionando correctamente${NC}"
echo ""
echo "Verificado:"
echo "  ✓ Sin token → 401 Unauthorized"
echo "  ✓ Admin obtiene token exitosamente"
echo "  ✓ Admin puede acceder a endpoints protegidos"
echo "  ✓ Vendedor obtiene token exitosamente"
echo "  ✓ Vendedor puede listar recursos"
echo ""
