#!/bin/bash

GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo "=========================================="
echo "   VERIFICACIÓN DEL SISTEMA - SINCOPOS"
echo "=========================================="
echo ""

# Verificar Docker
echo -n "1. Docker: "
if docker --version &> /dev/null; then
    echo -e "${GREEN}✓ Corriendo${NC}"
else
    echo -e "${RED}✗ No disponible${NC}"
    exit 1
fi

# Verificar PostgreSQL
echo -n "2. PostgreSQL (App): "
if docker ps | grep -q "sincopos-db.*healthy"; then
    echo -e "${GREEN}✓ Healthy${NC}"
else
    echo -e "${RED}✗ No disponible${NC}"
    exit 1
fi

# Verificar Keycloak DB
echo -n "3. PostgreSQL (Keycloak): "
if docker ps | grep -q "keycloak-db.*healthy"; then
    echo -e "${GREEN}✓ Healthy${NC}"
else
    echo -e "${RED}✗ No disponible${NC}"
    exit 1
fi

# Verificar Keycloak
echo -n "4. Keycloak: "
if curl -s http://localhost:8080/health/ready | grep -q "UP"; then
    echo -e "${GREEN}✓ Ready${NC}"
else
    echo -e "${YELLOW}⚠ Iniciando...${NC}"
    echo "   Espera 30 segundos más"
fi

# Verificar API
echo -n "5. API (.NET): "
if curl -s --head http://localhost:5000/swagger/index.html | grep -q "200"; then
    echo -e "${GREEN}✓ Corriendo${NC}"
    API_RUNNING=true
else
    echo -e "${RED}✗ No está corriendo${NC}"
    API_RUNNING=false
fi

echo ""
echo "=========================================="

if [ "$API_RUNNING" = false ]; then
    echo -e "${YELLOW}⚠ ACCIÓN REQUERIDA${NC}"
    echo ""
    echo "La API no está corriendo. Por favor:"
    echo ""
    echo -e "${BLUE}1.${NC} Abre una nueva terminal"
    echo -e "${BLUE}2.${NC} Ejecuta:"
    echo ""
    echo "   cd C:\Users\jaime.forero\RiderProjects\SincoPos"
    echo "   dotnet run --project POS.Api"
    echo ""
    echo -e "${BLUE}3.${NC} Espera a ver: ${GREEN}Now listening on: http://localhost:5000${NC}"
    echo -e "${BLUE}4.${NC} Luego ejecuta las pruebas:"
    echo ""
    echo "   bash scripts/test-login.sh"
    echo ""
else
    echo -e "${GREEN}✅ SISTEMA LISTO${NC}"
    echo ""
    echo "Puedes ejecutar las pruebas:"
    echo ""
    echo "   bash scripts/test-login.sh"
    echo ""
fi
