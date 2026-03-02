#!/bin/bash

echo "=========================================="
echo "   PRUEBA RÁPIDA DE AUDITORÍA"
echo "=========================================="
echo ""

API_URL="http://localhost:5086"
KEYCLOAK_URL="http://localhost:8080"

# Obtener token
TOKEN=$(curl -s -X POST "$KEYCLOAK_URL/realms/sincopos/protocol/openid-connect/token" \
    -d "client_id=pos-api" \
    -d "username=admin@sincopos.com" \
    -d "password=admin123" \
    -d "grant_type=password" | grep -o '"access_token":"[^"]*"' | cut -d'"' -f4)

echo "Token obtenido: ${TOKEN:0:30}..."
echo ""

# Crear impuesto
echo "Creando impuesto con Admin..."
RESPONSE=$(curl -s -X POST "$API_URL/api/impuestos" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d '{"nombre": "Test Auditoria", "porcentaje": 0.10}')

echo "Respuesta: $RESPONSE"
echo ""

# Consultar impuestos
echo "Consultando impuestos..."
curl -s -X GET "$API_URL/api/impuestos" \
    -H "Authorization: Bearer $TOKEN" | head -100

echo ""
echo ""
echo "Ahora verifica en la base de datos:"
echo ""
echo "psql -U postgres -d SincoPos -c \"SELECT id, nombre, creado_por, TO_CHAR(fecha_creacion, 'YYYY-MM-DD HH24:MI:SS') as creado FROM public.impuestos WHERE nombre = 'Test Auditoria';\""
