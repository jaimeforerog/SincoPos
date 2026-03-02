#!/bin/bash

# Test Final de Autorización - Verificar que roles funcionan correctamente
# Después de arreglar el mapeo de roles en Program.cs

echo "=========================================="
echo "   TEST FINAL DE AUTORIZACIÓN"
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

# Función para obtener token
get_token() {
    local username=$1
    local password=$2

    curl -s -X POST "$KEYCLOAK_URL/realms/$REALM/protocol/openid-connect/token" \
        -H "Content-Type: application/x-www-form-urlencoded" \
        -d "client_id=$CLIENT_ID" \
        -d "username=$username" \
        -d "password=$password" \
        -d "grant_type=password" | grep -o '"access_token":"[^"]*"' | cut -d'"' -f4
}

# Función para verificar resultado
check_result() {
    local test_name=$1
    local expected=$2
    local actual=$3

    if [ "$actual" == "$expected" ]; then
        echo -e "${GREEN}✓ PASS${NC}: $test_name (HTTP $actual)"
        ((PASSED++))
    else
        echo -e "${RED}✗ FAIL${NC}: $test_name (Esperado: $expected, Obtenido: $actual)"
        ((FAILED++))
    fi
}

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  1. OBTENER TOKENS"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

echo "Obteniendo token de Admin..."
TOKEN_ADMIN=$(get_token "admin@sincopos.com" "admin123")
if [ -n "$TOKEN_ADMIN" ] && [ "$TOKEN_ADMIN" != "null" ]; then
    echo -e "${GREEN}✓${NC} Token Admin obtenido: ${TOKEN_ADMIN:0:30}..."
else
    echo -e "${RED}✗${NC} No se pudo obtener token de Admin"
    exit 1
fi

echo "Obteniendo token de Supervisor..."
TOKEN_SUPERVISOR=$(get_token "supervisor@sincopos.com" "super123")
if [ -n "$TOKEN_SUPERVISOR" ] && [ "$TOKEN_SUPERVISOR" != "null" ]; then
    echo -e "${GREEN}✓${NC} Token Supervisor obtenido: ${TOKEN_SUPERVISOR:0:30}..."
else
    echo -e "${RED}✗${NC} No se pudo obtener token de Supervisor"
    exit 1
fi

echo "Obteniendo token de Cajero..."
TOKEN_CAJERO=$(get_token "cajero@sincopos.com" "cajero123")
if [ -n "$TOKEN_CAJERO" ] && [ "$TOKEN_CAJERO" != "null" ]; then
    echo -e "${GREEN}✓${NC} Token Cajero obtenido: ${TOKEN_CAJERO:0:30}..."
else
    echo -e "${RED}✗${NC} No se pudo obtener token de Cajero"
    exit 1
fi

echo "Obteniendo token de Vendedor..."
TOKEN_VENDEDOR=$(get_token "vendedor@sincopos.com" "vendedor123")
if [ -n "$TOKEN_VENDEDOR" ] && [ "$TOKEN_VENDEDOR" != "null" ]; then
    echo -e "${GREEN}✓${NC} Token Vendedor obtenido: ${TOKEN_VENDEDOR:0:30}..."
else
    echo -e "${RED}✗${NC} No se pudo obtener token de Vendedor"
    exit 1
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  2. VERIFICAR ACCESO GET (Todos pueden)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Admin puede hacer GET
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X GET "$API_URL/api/productos" \
    -H "Authorization: Bearer $TOKEN_ADMIN")
check_result "Admin - GET /api/productos" "200" "$HTTP_CODE"

# Supervisor puede hacer GET
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X GET "$API_URL/api/categorias" \
    -H "Authorization: Bearer $TOKEN_SUPERVISOR")
check_result "Supervisor - GET /api/categorias" "200" "$HTTP_CODE"

# Cajero puede hacer GET
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X GET "$API_URL/api/terceros" \
    -H "Authorization: Bearer $TOKEN_CAJERO")
check_result "Cajero - GET /api/terceros" "200" "$HTTP_CODE"

# Vendedor puede hacer GET
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X GET "$API_URL/api/productos" \
    -H "Authorization: Bearer $TOKEN_VENDEDOR")
check_result "Vendedor - GET /api/productos" "200" "$HTTP_CODE"

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  3. VERIFICAR ADMIN (Acceso Total)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Admin puede crear impuestos (requiere Admin)
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_URL/api/impuestos" \
    -H "Authorization: Bearer $TOKEN_ADMIN" \
    -H "Content-Type: application/json" \
    -d '{"nombre": "IVA Test", "porcentaje": 0.19}')
# Puede devolver 201 (creado) o 400 (validación), pero NO debe ser 403
if [ "$HTTP_CODE" != "403" ]; then
    echo -e "${GREEN}✓ PASS${NC}: Admin - POST /api/impuestos (HTTP $HTTP_CODE - No es 403)"
    ((PASSED++))
else
    echo -e "${RED}✗ FAIL${NC}: Admin - POST /api/impuestos (HTTP $HTTP_CODE - No debería ser 403)"
    ((FAILED++))
fi

# Admin puede crear sucursales (requiere Admin)
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_URL/api/sucursales" \
    -H "Authorization: Bearer $TOKEN_ADMIN" \
    -H "Content-Type: application/json" \
    -d '{"nombre": "Sucursal Test", "direccion": "Test", "ciudad": "Test"}')
if [ "$HTTP_CODE" != "403" ]; then
    echo -e "${GREEN}✓ PASS${NC}: Admin - POST /api/sucursales (HTTP $HTTP_CODE - No es 403)"
    ((PASSED++))
else
    echo -e "${RED}✗ FAIL${NC}: Admin - POST /api/sucursales (HTTP $HTTP_CODE - No debería ser 403)"
    ((FAILED++))
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  4. VERIFICAR SUPERVISOR (Gestión Operativa)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Supervisor puede crear categorías (requiere Supervisor)
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_URL/api/categorias" \
    -H "Authorization: Bearer $TOKEN_SUPERVISOR" \
    -H "Content-Type: application/json" \
    -d '{"nombre": "Categoria Test", "descripcion": "Test"}')
if [ "$HTTP_CODE" != "403" ]; then
    echo -e "${GREEN}✓ PASS${NC}: Supervisor - POST /api/categorias (HTTP $HTTP_CODE - No es 403)"
    ((PASSED++))
else
    echo -e "${RED}✗ FAIL${NC}: Supervisor - POST /api/categorias (HTTP $HTTP_CODE - No debería ser 403)"
    ((FAILED++))
fi

# Supervisor puede crear productos (requiere Supervisor)
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_URL/api/productos" \
    -H "Authorization: Bearer $TOKEN_SUPERVISOR" \
    -H "Content-Type: application/json" \
    -d '{"nombre": "Producto Test", "codigoBarras": "123456789"}')
if [ "$HTTP_CODE" != "403" ]; then
    echo -e "${GREEN}✓ PASS${NC}: Supervisor - POST /api/productos (HTTP $HTTP_CODE - No es 403)"
    ((PASSED++))
else
    echo -e "${RED}✗ FAIL${NC}: Supervisor - POST /api/productos (HTTP $HTTP_CODE - No debería ser 403)"
    ((FAILED++))
fi

# Supervisor NO puede crear impuestos (requiere Admin)
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_URL/api/impuestos" \
    -H "Authorization: Bearer $TOKEN_SUPERVISOR" \
    -H "Content-Type: application/json" \
    -d '{"nombre": "IVA Test", "porcentaje": 0.19}')
check_result "Supervisor - POST /api/impuestos (debe rechazar)" "403" "$HTTP_CODE"

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  5. VERIFICAR CAJERO (Operaciones de Venta)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Cajero puede crear terceros (requiere Cajero)
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_URL/api/terceros" \
    -H "Authorization: Bearer $TOKEN_CAJERO" \
    -H "Content-Type: application/json" \
    -d '{"nombre": "Cliente Test", "tipoIdentificacion": "CC", "numeroIdentificacion": "123456789", "tipoTercero": "Cliente"}')
if [ "$HTTP_CODE" != "403" ]; then
    echo -e "${GREEN}✓ PASS${NC}: Cajero - POST /api/terceros (HTTP $HTTP_CODE - No es 403)"
    ((PASSED++))
else
    echo -e "${RED}✗ FAIL${NC}: Cajero - POST /api/terceros (HTTP $HTTP_CODE - No debería ser 403)"
    ((FAILED++))
fi

# Cajero NO puede crear productos (requiere Supervisor)
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_URL/api/productos" \
    -H "Authorization: Bearer $TOKEN_CAJERO" \
    -H "Content-Type: application/json" \
    -d '{"nombre": "Producto Test"}')
check_result "Cajero - POST /api/productos (debe rechazar)" "403" "$HTTP_CODE"

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  6. VERIFICAR VENDEDOR (Solo Lectura)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Vendedor puede listar productos
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X GET "$API_URL/api/productos" \
    -H "Authorization: Bearer $TOKEN_VENDEDOR")
check_result "Vendedor - GET /api/productos" "200" "$HTTP_CODE"

# Vendedor NO puede crear productos
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_URL/api/productos" \
    -H "Authorization: Bearer $TOKEN_VENDEDOR" \
    -H "Content-Type: application/json" \
    -d '{"nombre": "Producto Test"}')
check_result "Vendedor - POST /api/productos (debe rechazar)" "403" "$HTTP_CODE"

# Vendedor NO puede crear categorías
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_URL/api/categorias" \
    -H "Authorization: Bearer $TOKEN_VENDEDOR" \
    -H "Content-Type: application/json" \
    -d '{"nombre": "Categoria Test"}')
check_result "Vendedor - POST /api/categorias (debe rechazar)" "403" "$HTTP_CODE"

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  7. VERIFICAR SIN AUTENTICACIÓN"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Sin token debe devolver 401
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X GET "$API_URL/api/productos")
check_result "Sin token - GET /api/productos (debe rechazar)" "401" "$HTTP_CODE"

HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_URL/api/productos" \
    -H "Content-Type: application/json" \
    -d '{"nombre": "Test"}')
check_result "Sin token - POST /api/productos (debe rechazar)" "401" "$HTTP_CODE"

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  RESUMEN FINAL"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

echo -e "${GREEN}✓ Pruebas pasadas:${NC} $PASSED"
echo -e "${RED}✗ Pruebas fallidas:${NC} $FAILED"

echo ""

if [ $FAILED -eq 0 ]; then
    echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${GREEN}  ✅ TODAS LAS PRUEBAS DE AUTORIZACIÓN PASARON${NC}"
    echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo ""
    echo "🎉 El sistema de seguridad está funcionando correctamente:"
    echo ""
    echo "   ✓ Todos los endpoints requieren autenticación"
    echo "   ✓ Admin tiene acceso total"
    echo "   ✓ Supervisor puede gestionar operaciones"
    echo "   ✓ Cajero puede realizar ventas"
    echo "   ✓ Vendedor solo puede consultar"
    echo "   ✓ Roles de Keycloak se mapean correctamente"
    echo ""
    exit 0
else
    echo -e "${RED}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${RED}  ⚠️  ALGUNAS PRUEBAS FALLARON${NC}"
    echo -e "${RED}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo ""
    echo "Por favor revisa los errores arriba."
    echo ""
    exit 1
fi
