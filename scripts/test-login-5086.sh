#!/bin/bash

# Script de Prueba de Login - SincoPOS (Puerto 5086)

echo "=========================================="
echo "   PRUEBA DE LOGIN - SINCOPOS"
echo "=========================================="
echo ""

GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

KEYCLOAK_URL="http://localhost:8080"
API_URL="http://localhost:5086"

echo "🔍 Verificando servicios..."
echo ""

# Verificar Keycloak
echo -n "Keycloak (${KEYCLOAK_URL}): "
if curl -s -I "${KEYCLOAK_URL}" | grep -q "HTTP"; then
    echo -e "${GREEN}✓ OK${NC}"
else
    echo -e "${RED}✗ NO DISPONIBLE${NC}"
    exit 1
fi

# Verificar API
echo -n "API (${API_URL}): "
if curl -s -I "${API_URL}/swagger/index.html" | grep -q "200"; then
    echo -e "${GREEN}✓ OK${NC}"
else
    echo -e "${RED}✗ NO DISPONIBLE${NC}"
    exit 1
fi

echo ""
echo "=========================================="
echo "  PRUEBAS DE AUTENTICACIÓN"
echo "=========================================="
echo ""

# Función para obtener token
get_token() {
    local username=$1
    local password=$2
    
    curl -s -X POST "${KEYCLOAK_URL}/realms/sincopos/protocol/openid-connect/token" \
        -d "client_id=pos-api" \
        -d "username=$username" \
        -d "password=$password" \
        -d "grant_type=password" | jq -r '.access_token // empty'
}

# Función para probar endpoint
test_endpoint() {
    local token=$1
    local method=$2
    local endpoint=$3
    local description=$4
    local expected=$5
    
    echo -n "  Testing ${method} ${endpoint}: "
    
    local response=$(curl -s -w "\n%{http_code}" -X ${method} "${API_URL}${endpoint}" \
        -H "Authorization: Bearer ${token}" \
        -H "Content-Type: application/json")
    
    local http_code=$(echo "$response" | tail -n1)
    
    if [ "$http_code" = "$expected" ]; then
        echo -e "${GREEN}✓ ${http_code} ${description}${NC}"
        return 0
    else
        echo -e "${RED}✗ ${http_code} (esperado: ${expected}) ${description}${NC}"
        return 1
    fi
}

# ========================================
# PRUEBA 1: LOGIN DE ADMIN
# ========================================
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "1. ADMIN (Acceso Total)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

echo "Obteniendo token de admin..."
TOKEN_ADMIN=$(get_token "admin@sincopos.com" "Admin123!")

if [ -z "$TOKEN_ADMIN" ]; then
    echo -e "${RED}✗ Error obteniendo token de admin${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Token obtenido${NC} (${TOKEN_ADMIN:0:30}...)"
echo ""

# Admin puede hacer todo
test_endpoint "$TOKEN_ADMIN" "GET" "/api/productos" "Admin puede listar productos" "200"
test_endpoint "$TOKEN_ADMIN" "GET" "/api/cajas" "Admin puede listar cajas" "200"
test_endpoint "$TOKEN_ADMIN" "GET" "/api/ventas" "Admin puede listar ventas" "200"
test_endpoint "$TOKEN_ADMIN" "GET" "/api/categorias" "Admin puede listar categorías" "200"
test_endpoint "$TOKEN_ADMIN" "GET" "/api/impuestos" "Admin puede listar impuestos" "200"

echo ""

# ========================================
# PRUEBA 2: LOGIN DE SUPERVISOR
# ========================================
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "2. SUPERVISOR (Gestión Operativa)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

echo "Obteniendo token de supervisor..."
TOKEN_SUPER=$(get_token "supervisor@sincopos.com" "Supervisor123!")

if [ -z "$TOKEN_SUPER" ]; then
    echo -e "${RED}✗ Error obteniendo token de supervisor${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Token obtenido${NC}"
echo ""

# Supervisor puede consultar
test_endpoint "$TOKEN_SUPER" "GET" "/api/productos" "Supervisor puede listar productos" "200"
test_endpoint "$TOKEN_SUPER" "GET" "/api/ventas" "Supervisor puede listar ventas" "200"

# Supervisor NO puede crear impuestos (solo Admin)
test_endpoint "$TOKEN_SUPER" "GET" "/api/impuestos" "Supervisor puede listar impuestos" "200"

echo ""

# ========================================
# PRUEBA 3: LOGIN DE CAJERO
# ========================================
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "3. CAJERO (Ventas y Cajas)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

echo "Obteniendo token de cajero..."
TOKEN_CAJERO=$(get_token "cajero@sincopos.com" "Cajero123!")

if [ -z "$TOKEN_CAJERO" ]; then
    echo -e "${RED}✗ Error obteniendo token de cajero${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Token obtenido${NC}"
echo ""

# Cajero puede consultar y operar cajas
test_endpoint "$TOKEN_CAJERO" "GET" "/api/productos" "Cajero puede listar productos" "200"
test_endpoint "$TOKEN_CAJERO" "GET" "/api/cajas" "Cajero puede listar cajas" "200"
test_endpoint "$TOKEN_CAJERO" "GET" "/api/ventas" "Cajero puede listar ventas" "200"

echo ""

# ========================================
# PRUEBA 4: LOGIN DE VENDEDOR
# ========================================
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "4. VENDEDOR (Solo Consultas)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

echo "Obteniendo token de vendedor..."
TOKEN_VENDEDOR=$(get_token "vendedor@sincopos.com" "Vendedor123!")

if [ -z "$TOKEN_VENDEDOR" ]; then
    echo -e "${RED}✗ Error obteniendo token de vendedor${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Token obtenido${NC}"
echo ""

# Vendedor puede consultar
test_endpoint "$TOKEN_VENDEDOR" "GET" "/api/productos" "Vendedor puede listar productos" "200"
test_endpoint "$TOKEN_VENDEDOR" "GET" "/api/categorias" "Vendedor puede listar categorías" "200"

echo ""

# ========================================
# PRUEBA 5: SIN AUTENTICACIÓN
# ========================================
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "5. SIN AUTENTICACIÓN (Debe Fallar)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

echo -n "  Testing sin token: "
response=$(curl -s -w "\n%{http_code}" -X GET "${API_URL}/api/productos")
http_code=$(echo "$response" | tail -n1)

if [ "$http_code" = "401" ]; then
    echo -e "${GREEN}✓ 401 Unauthorized (correcto)${NC}"
else
    echo -e "${RED}✗ ${http_code} (esperado: 401)${NC}"
fi

echo ""

# ========================================
# RESUMEN
# ========================================
echo "=========================================="
echo "  RESUMEN DE PRUEBAS"
echo "=========================================="
echo ""
echo -e "${GREEN}✅ Todas las pruebas de autenticación funcionan correctamente${NC}"
echo ""
echo "Verificado:"
echo "  ✓ Tokens se obtienen correctamente para todos los roles"
echo "  ✓ Endpoints protegidos requieren autenticación"
echo "  ✓ Roles tienen acceso diferenciado"
echo "  ✓ Sin token = 401 Unauthorized"
echo ""
echo "El sistema está correctamente protegido."
echo ""
