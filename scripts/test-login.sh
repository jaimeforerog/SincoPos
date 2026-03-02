#!/bin/bash

# Script de Prueba de Login - SincoPOS
# Este script prueba la autenticación con Keycloak y valida el acceso a la API

echo "=========================================="
echo "   PRUEBA DE LOGIN - SINCOPOS"
echo "=========================================="
echo ""

# Colores para output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# URLs
KEYCLOAK_URL="http://localhost:8080"
API_URL="http://localhost:5000"

# Función para verificar servicios
check_service() {
    local url=$1
    local name=$2

    echo -n "Verificando $name... "
    if curl -s --head --request GET "$url" | grep "200\|302\|404" > /dev/null; then
        echo -e "${GREEN}✓ OK${NC}"
        return 0
    else
        echo -e "${RED}✗ NO DISPONIBLE${NC}"
        return 1
    fi
}

# Función para obtener token
get_token() {
    local username=$1
    local password=$2

    echo ""
    echo "Obteniendo token para: $username"

    local response=$(curl -s -X POST "${KEYCLOAK_URL}/realms/sincopos/protocol/openid-connect/token" \
        -H "Content-Type: application/x-www-form-urlencoded" \
        -d "client_id=pos-api" \
        -d "username=$username" \
        -d "password=$password" \
        -d "grant_type=password")

    local token=$(echo $response | jq -r '.access_token // empty')

    if [ -z "$token" ] || [ "$token" = "null" ]; then
        echo -e "${RED}✗ Error obteniendo token${NC}"
        echo "Respuesta: $response"
        return 1
    else
        echo -e "${GREEN}✓ Token obtenido exitosamente${NC}"
        echo "Token (primeros 50 chars): ${token:0:50}..."

        # Decodificar y mostrar claims (opcional)
        echo ""
        echo "Claims del token:"
        echo $token | cut -d'.' -f2 | base64 -d 2>/dev/null | jq '.' || echo "No se pudo decodificar"

        echo "$token"
        return 0
    fi
}

# Función para probar endpoint
test_endpoint() {
    local token=$1
    local endpoint=$2
    local method=${3:-GET}

    echo ""
    echo "Probando: $method $endpoint"

    local response=$(curl -s -w "\n%{http_code}" -X $method "${API_URL}${endpoint}" \
        -H "Authorization: Bearer $token" \
        -H "Content-Type: application/json")

    local http_code=$(echo "$response" | tail -n1)
    local body=$(echo "$response" | sed '$d')

    if [ "$http_code" = "200" ] || [ "$http_code" = "201" ]; then
        echo -e "${GREEN}✓ HTTP $http_code - OK${NC}"
        echo "Respuesta (primeras líneas):"
        echo "$body" | jq '.' 2>/dev/null | head -20 || echo "$body" | head -20
        return 0
    elif [ "$http_code" = "401" ]; then
        echo -e "${RED}✗ HTTP 401 - NO AUTORIZADO${NC}"
        echo "El token no es válido o expiró"
        return 1
    elif [ "$http_code" = "403" ]; then
        echo -e "${YELLOW}! HTTP 403 - PROHIBIDO${NC}"
        echo "El usuario no tiene permisos para este endpoint"
        return 1
    else
        echo -e "${RED}✗ HTTP $http_code - ERROR${NC}"
        echo "Respuesta: $body"
        return 1
    fi
}

# ========================================
# INICIO DE LAS PRUEBAS
# ========================================

echo ""
echo "1. VERIFICACIÓN DE SERVICIOS"
echo "----------------------------"

check_service "$KEYCLOAK_URL/health" "Keycloak"
KEYCLOAK_OK=$?

check_service "$API_URL/swagger/index.html" "API"
API_OK=$?

if [ $KEYCLOAK_OK -ne 0 ] || [ $API_OK -ne 0 ]; then
    echo ""
    echo -e "${RED}Error: Uno o más servicios no están disponibles${NC}"
    echo ""
    echo "Asegúrate de que Docker esté corriendo:"
    echo "  docker-compose up -d"
    echo ""
    echo "Y que la API esté ejecutándose:"
    echo "  dotnet run --project POS.Api"
    exit 1
fi

echo ""
echo "=========================================="
echo "2. PRUEBAS DE AUTENTICACIÓN"
echo "=========================================="

# Array de usuarios para probar
declare -a USERS=(
    "admin@sincopos.com:Admin123!:admin"
    "supervisor@sincopos.com:Supervisor123!:supervisor"
    "cajero@sincopos.com:Cajero123!:cajero"
    "vendedor@sincopos.com:Vendedor123!:vendedor"
)

# Probar cada usuario
for user_info in "${USERS[@]}"; do
    IFS=':' read -r username password role <<< "$user_info"

    echo ""
    echo "=========================================="
    echo "Probando usuario: $username (Rol: $role)"
    echo "=========================================="

    # Obtener token
    TOKEN=$(get_token "$username" "$password")

    if [ $? -eq 0 ]; then
        # Probar endpoints según el rol

        # Todos pueden listar productos (no protegido)
        test_endpoint "$TOKEN" "/api/productos"

        # Cajas requiere autenticación
        test_endpoint "$TOKEN" "/api/cajas"

        # Sucursales (no protegido actualmente)
        test_endpoint "$TOKEN" "/api/sucursales"

        # Ventas (no protegido actualmente)
        test_endpoint "$TOKEN" "/api/ventas"

        echo ""
        echo "----------------------------"
    fi

    # Pequeña pausa entre usuarios
    sleep 1
done

echo ""
echo "=========================================="
echo "3. PRUEBA DE TOKEN INVÁLIDO"
echo "=========================================="

echo ""
echo "Probando acceso con token inválido..."
INVALID_TOKEN="eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.invalid.token"
test_endpoint "$INVALID_TOKEN" "/api/cajas"

echo ""
echo "=========================================="
echo "4. PRUEBA SIN TOKEN (Anónimo)"
echo "=========================================="

echo ""
echo "Probando endpoints sin autenticación..."

# Probar productos (no protegido)
echo ""
echo "GET /api/productos (sin auth):"
curl -s "${API_URL}/api/productos" | jq '.' | head -10

# Probar cajas (protegido)
echo ""
echo "GET /api/cajas (sin auth - debe fallar):"
response=$(curl -s -w "\n%{http_code}" "${API_URL}/api/cajas")
http_code=$(echo "$response" | tail -n1)
if [ "$http_code" = "401" ]; then
    echo -e "${GREEN}✓ Correctamente bloqueado - HTTP 401${NC}"
else
    echo -e "${RED}✗ ERROR: Debería devolver 401 pero devolvió $http_code${NC}"
fi

echo ""
echo "=========================================="
echo "RESUMEN DE PRUEBAS COMPLETADO"
echo "=========================================="
echo ""
echo "Notas:"
echo "- Los endpoints de ProductosController NO están protegidos actualmente"
echo "- El endpoint de CajasController SÍ requiere autenticación"
echo "- VentasController NO está protegido actualmente"
echo ""
echo "Recomendaciones:"
echo "1. Agregar [Authorize] a todos los controladores"
echo "2. Definir políticas según roles para operaciones sensibles"
echo "3. Solo ProductosController (GET) podría ser público si es necesario"
echo ""
