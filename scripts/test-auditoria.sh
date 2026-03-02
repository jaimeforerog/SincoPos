#!/bin/bash

# Script de Prueba de Auditoría - SincoPOS
# Verifica que los campos de auditoría se registren correctamente

echo "=========================================="
echo "   PRUEBA DE AUDITORÍA - SINCOPOS"
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
CYAN='\033[0;36m'
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

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  2. CREAR REGISTROS (Verificar CreadoPor)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

TIMESTAMP=$(date +%s)

echo -e "${CYAN}[Admin]${NC} Creando Sucursal 'Sucursal Test Norte'..."
SUCURSAL_1=$(curl -s -X POST "$API_URL/api/sucursales" \
    -H "Authorization: Bearer $TOKEN_ADMIN" \
    -H "Content-Type: application/json" \
    -d "{
        \"codigo\": \"SUC-T-$TIMESTAMP\",
        \"nombre\": \"Sucursal Test Norte\",
        \"direccion\": \"Calle 100 #15-20\",
        \"telefono\": \"6012345678\",
        \"email\": \"testnorte$TIMESTAMP@sincopos.com\",
        \"activo\": true
    }")

SUCURSAL_1_ID=$(echo $SUCURSAL_1 | grep -o '"id":[0-9]*' | head -1 | cut -d':' -f2)
echo -e "${GREEN}✓${NC} Sucursal creada con ID: $SUCURSAL_1_ID"
echo ""

echo -e "${CYAN}[Supervisor]${NC} Creando Categoría 'Categoría Test'..."
CATEGORIA_1=$(curl -s -X POST "$API_URL/api/categorias" \
    -H "Authorization: Bearer $TOKEN_SUPERVISOR" \
    -H "Content-Type: application/json" \
    -d "{
        \"codigo\": \"CAT-T-$TIMESTAMP\",
        \"nombre\": \"Categoría Test $TIMESTAMP\",
        \"descripcion\": \"Prueba de auditoría\",
        \"activo\": true
    }")

CATEGORIA_1_ID=$(echo $CATEGORIA_1 | grep -o '"id":[0-9]*' | head -1 | cut -d':' -f2)
echo -e "${GREEN}✓${NC} Categoría creada con ID: $CATEGORIA_1_ID"
echo ""

echo -e "${CYAN}[Admin]${NC} Creando Impuesto 'IVA Test'..."
IMPUESTO_1=$(curl -s -X POST "$API_URL/api/impuestos" \
    -H "Authorization: Bearer $TOKEN_ADMIN" \
    -H "Content-Type: application/json" \
    -d "{
        \"codigo\": \"IVA-T-$TIMESTAMP\",
        \"nombre\": \"IVA Test $TIMESTAMP\",
        \"porcentaje\": 19.0,
        \"activo\": true
    }")

IMPUESTO_1_ID=$(echo $IMPUESTO_1 | grep -o '"id":[0-9]*' | head -1 | cut -d':' -f2)
echo -e "${GREEN}✓${NC} Impuesto creado con ID: $IMPUESTO_1_ID"
echo ""

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  3. VERIFICAR CAMPOS DE CREACIÓN"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

sleep 1

# Verificar Sucursal
SUCURSAL=$(curl -s -X GET "$API_URL/api/sucursales/$SUCURSAL_1_ID" \
    -H "Authorization: Bearer $TOKEN_ADMIN")

CREADO_POR=$(echo $SUCURSAL | jq -r '.creadoPor')
FECHA_CREACION=$(echo $SUCURSAL | jq -r '.fechaCreacion')
MODIFICADO_POR=$(echo $SUCURSAL | jq -r '.modificadoPor')

echo "Sucursal ID $SUCURSAL_1_ID:"
echo "  CreadoPor: $CREADO_POR"
echo "  FechaCreacion: $FECHA_CREACION"
echo "  ModificadoPor: $MODIFICADO_POR"

if [ "$CREADO_POR" == "admin@sincopos.com" ]; then
    echo -e "${GREEN}✓ PASS${NC}: Sucursal creada por admin@sincopos.com"
    ((PASSED++))
else
    echo -e "${RED}✗ FAIL${NC}: CreadoPor incorrecto: $CREADO_POR"
    ((FAILED++))
fi

if [ "$MODIFICADO_POR" == "null" ]; then
    echo -e "${GREEN}✓ PASS${NC}: ModificadoPor es null (correcto para nuevo registro)"
    ((PASSED++))
else
    echo -e "${RED}✗ FAIL${NC}: ModificadoPor debería ser null: $MODIFICADO_POR"
    ((FAILED++))
fi

echo ""

# Verificar Categoría
CATEGORIA=$(curl -s -X GET "$API_URL/api/categorias/$CATEGORIA_1_ID" \
    -H "Authorization: Bearer $TOKEN_ADMIN")

CREADO_POR=$(echo $CATEGORIA | jq -r '.creadoPor')

echo "Categoría ID $CATEGORIA_1_ID:"
echo "  CreadoPor: $CREADO_POR"

if [ "$CREADO_POR" == "supervisor@sincopos.com" ]; then
    echo -e "${GREEN}✓ PASS${NC}: Categoría creada por supervisor@sincopos.com"
    ((PASSED++))
else
    echo -e "${RED}✗ FAIL${NC}: CreadoPor incorrecto: $CREADO_POR"
    ((FAILED++))
fi

echo ""

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  4. MODIFICAR REGISTROS (Verificar ModificadoPor)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

sleep 2

echo -e "${CYAN}[Supervisor]${NC} Modificando Sucursal (creada por Admin)..."
curl -s -X PUT "$API_URL/api/sucursales/$SUCURSAL_1_ID" \
    -H "Authorization: Bearer $TOKEN_SUPERVISOR" \
    -H "Content-Type: application/json" \
    -d "{
        \"codigo\": \"SUC-T-$TIMESTAMP\",
        \"nombre\": \"Sucursal Test Norte - Actualizada\",
        \"direccion\": \"Calle 100 #15-20, Piso 2\",
        \"telefono\": \"6012345678\",
        \"email\": \"testnorte$TIMESTAMP@sincopos.com\",
        \"activo\": true
    }" > /dev/null

echo -e "${GREEN}✓${NC} Sucursal modificada por Supervisor"
echo ""

echo -e "${CYAN}[Admin]${NC} Modificando Categoría (creada por Supervisor)..."
curl -s -X PUT "$API_URL/api/categorias/$CATEGORIA_1_ID" \
    -H "Authorization: Bearer $TOKEN_ADMIN" \
    -H "Content-Type: application/json" \
    -d "{
        \"codigo\": \"CAT-T-$TIMESTAMP\",
        \"nombre\": \"Categoría Test - Actualizada\",
        \"descripcion\": \"Modificada por admin\",
        \"activo\": true
    }" > /dev/null

echo -e "${GREEN}✓${NC} Categoría modificada por Admin"
echo ""

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  5. VERIFICAR CAMPOS DE MODIFICACIÓN"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

sleep 1

# Verificar Sucursal modificada
SUCURSAL=$(curl -s -X GET "$API_URL/api/sucursales/$SUCURSAL_1_ID" \
    -H "Authorization: Bearer $TOKEN_ADMIN")

CREADO_POR=$(echo $SUCURSAL | jq -r '.creadoPor')
MODIFICADO_POR=$(echo $SUCURSAL | jq -r '.modificadoPor')
FECHA_CREACION=$(echo $SUCURSAL | jq -r '.fechaCreacion')
FECHA_MODIFICACION=$(echo $SUCURSAL | jq -r '.fechaModificacion')

echo "Sucursal ID $SUCURSAL_1_ID después de modificación:"
echo "  CreadoPor: $CREADO_POR"
echo "  ModificadoPor: $MODIFICADO_POR"
echo "  FechaCreacion: $FECHA_CREACION"
echo "  FechaModificacion: $FECHA_MODIFICACION"

if [ "$CREADO_POR" == "admin@sincopos.com" ]; then
    echo -e "${GREEN}✓ PASS${NC}: CreadoPor se mantiene (admin@sincopos.com)"
    ((PASSED++))
else
    echo -e "${RED}✗ FAIL${NC}: CreadoPor cambió: $CREADO_POR"
    ((FAILED++))
fi

if [ "$MODIFICADO_POR" == "supervisor@sincopos.com" ]; then
    echo -e "${GREEN}✓ PASS${NC}: ModificadoPor actualizado (supervisor@sincopos.com)"
    ((PASSED++))
else
    echo -e "${RED}✗ FAIL${NC}: ModificadoPor incorrecto: $MODIFICADO_POR"
    ((FAILED++))
fi

if [ "$FECHA_MODIFICACION" != "null" ] && [ "$FECHA_MODIFICACION" \> "$FECHA_CREACION" ]; then
    echo -e "${GREEN}✓ PASS${NC}: FechaModificacion > FechaCreacion"
    ((PASSED++))
else
    echo -e "${RED}✗ FAIL${NC}: FechaModificacion no es posterior a FechaCreacion"
    ((FAILED++))
fi

echo ""

# Verificar Categoría modificada
CATEGORIA=$(curl -s -X GET "$API_URL/api/categorias/$CATEGORIA_1_ID" \
    -H "Authorization: Bearer $TOKEN_ADMIN")

CREADO_POR=$(echo $CATEGORIA | jq -r '.creadoPor')
MODIFICADO_POR=$(echo $CATEGORIA | jq -r '.modificadoPor')

echo "Categoría ID $CATEGORIA_1_ID después de modificación:"
echo "  CreadoPor: $CREADO_POR"
echo "  ModificadoPor: $MODIFICADO_POR"

if [ "$CREADO_POR" == "supervisor@sincopos.com" ]; then
    echo -e "${GREEN}✓ PASS${NC}: CreadoPor se mantiene (supervisor@sincopos.com)"
    ((PASSED++))
else
    echo -e "${RED}✗ FAIL${NC}: CreadoPor cambió: $CREADO_POR"
    ((FAILED++))
fi

if [ "$MODIFICADO_POR" == "admin@sincopos.com" ]; then
    echo -e "${GREEN}✓ PASS${NC}: ModificadoPor actualizado (admin@sincopos.com)"
    ((PASSED++))
else
    echo -e "${RED}✗ FAIL${NC}: ModificadoPor incorrecto: $MODIFICADO_POR"
    ((FAILED++))
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  6. VERIFICAR TODOS LOS REGISTROS"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

echo -e "${YELLOW}SUCURSALES:${NC}"
curl -s -X GET "$API_URL/api/sucursales" -H "Authorization: Bearer $TOKEN_ADMIN" | \
    jq -r '.[] | "ID: \(.id) | Creado por: \(.creadoPor) | Modificado por: \(.modificadoPor // "null")"'
echo ""

echo -e "${YELLOW}CATEGORÍAS:${NC}"
curl -s -X GET "$API_URL/api/categorias" -H "Authorization: Bearer $TOKEN_ADMIN" | \
    jq -r '.[] | "ID: \(.id) | Creado por: \(.creadoPor) | Modificado por: \(.modificadoPor // "null")"'
echo ""

echo -e "${YELLOW}IMPUESTOS:${NC}"
curl -s -X GET "$API_URL/api/impuestos" -H "Authorization: Bearer $TOKEN_ADMIN" | \
    jq -r '.[] | "ID: \(.id) | Creado por: \(.creadoPor) | Modificado por: \(.modificadoPor // "null")"'
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
    echo -e "${GREEN}  ✅ SISTEMA DE AUDITORÍA FUNCIONA CORRECTAMENTE${NC}"
    echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo ""
    echo "🎉 Todos los campos de auditoría se están registrando correctamente!"
    echo ""
    echo "IDs creados para referencia:"
    echo "  Sucursal: $SUCURSAL_1_ID"
    echo "  Categoría: $CATEGORIA_1_ID"
    echo "  Impuesto: $IMPUESTO_1_ID"
    echo ""
    exit 0
else
    echo -e "${RED}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${RED}  ⚠️  ALGUNAS PRUEBAS FALLARON${NC}"
    echo -e "${RED}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo ""
    exit 1
fi
