#!/bin/bash

# Script para analizar el uso del contexto de usuario en controladores

echo "=========================================="
echo "   ANÁLISIS DE USO DE USUARIO"
echo "=========================================="
echo ""

CONTROLLERS_DIR="POS.Api/Controllers"

# Colores
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  1. CONTROLADORES QUE USAN HttpContext.User"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

if grep -r "HttpContext.User\|User\." --include="*.cs" "$CONTROLLERS_DIR" 2>/dev/null | grep -v "//"; then
    echo -e "${GREEN}Encontrado uso de User context${NC}"
else
    echo -e "${YELLOW}⚠ No se encontró uso de User context en controladores${NC}"
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  2. USO DE ClaimsPrincipalExtensions"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

echo "Buscando GetKeycloakId()..."
if grep -r "GetKeycloakId" --include="*.cs" "$CONTROLLERS_DIR" 2>/dev/null; then
    echo -e "${GREEN}✓ Encontrado${NC}"
else
    echo -e "${RED}✗ No encontrado${NC}"
fi

echo ""
echo "Buscando GetEmail()..."
if grep -r "GetEmail" --include="*.cs" "$CONTROLLERS_DIR" 2>/dev/null; then
    echo -e "${GREEN}✓ Encontrado${NC}"
else
    echo -e "${RED}✗ No encontrado${NC}"
fi

echo ""
echo "Buscando GetNombreCompleto()..."
if grep -r "GetNombreCompleto" --include="*.cs" "$CONTROLLERS_DIR" 2>/dev/null; then
    echo -e "${GREEN}✓ Encontrado${NC}"
else
    echo -e "${RED}✗ No encontrado${NC}"
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  3. AUDITORÍA EN ENTIDADES"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

ENTITIES_DIR="POS.Infrastructure/Data/Entities"

echo "Buscando campos de auditoría (CreadoPor, ModificadoPor)..."
if grep -r "CreadoPor\|ModificadoPor\|CreatedBy\|ModifiedBy" --include="*.cs" "$ENTITIES_DIR" 2>/dev/null; then
    echo -e "${GREEN}✓ Encontrado en algunas entidades${NC}"
else
    echo -e "${RED}✗ No encontrado - CRÍTICO: Falta auditoría${NC}"
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  4. ACTIVITY LOG / TRACKING"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

echo "Buscando ActividadUsuario o ActivityLog..."
if find . -name "*.cs" -type f 2>/dev/null | xargs grep -l "ActividadUsuario\|ActivityLog\|UserActivity" 2>/dev/null | grep -v obj | grep -v bin; then
    echo -e "${GREEN}✓ Encontrado${NC}"
else
    echo -e "${RED}✗ No encontrado - Falta logging de actividades${NC}"
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  5. USUARIO SERVICE"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

if [ -f "POS.Infrastructure/Services/UsuarioService.cs" ]; then
    echo -e "${GREEN}✓ UsuarioService existe${NC}"

    echo ""
    echo "Métodos disponibles:"
    grep "public.*Task.*Async\|public.*bool\|public.*int" "POS.Infrastructure/Services/UsuarioService.cs" | grep -v "///" | sed 's/^/  - /'
else
    echo -e "${RED}✗ UsuarioService no encontrado${NC}"
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  6. CONTROLADOR DE USUARIOS"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

if [ -f "$CONTROLLERS_DIR/UsuariosController.cs" ]; then
    echo -e "${GREEN}✓ UsuariosController existe${NC}"

    echo ""
    echo "Endpoints disponibles:"
    grep "\[Http" "$CONTROLLERS_DIR/UsuariosController.cs" | sed 's/^/  - /'
else
    echo -e "${RED}✗ UsuariosController no existe - CRÍTICO${NC}"
    echo -e "${YELLOW}Recomendación: Crear UsuariosController para gestión de usuarios${NC}"
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  7. TESTS DE AUTORIZACIÓN"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

if find tests -name "*Auth*Test*.cs" -o -name "*Security*Test*.cs" 2>/dev/null | grep -v obj | grep -v bin; then
    echo -e "${GREEN}✓ Tests de autorización encontrados${NC}"
else
    echo -e "${RED}✗ No se encontraron tests de autorización${NC}"
    echo -e "${YELLOW}Recomendación: Crear tests automatizados de autorización${NC}"
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  8. RATE LIMITING"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

if grep -r "AddRateLimiter\|EnableRateLimiting" --include="*.cs" . 2>/dev/null | grep -v obj | grep -v bin; then
    echo -e "${GREEN}✓ Rate Limiting configurado${NC}"
else
    echo -e "${YELLOW}⚠ Rate Limiting no configurado${NC}"
    echo -e "${YELLOW}Recomendación: Implementar rate limiting para prevenir abuso${NC}"
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  9. REFRESH TOKENS"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

if grep -r "refresh.*token\|RefreshToken" --include="*.cs" . 2>/dev/null | grep -v obj | grep -v bin | grep -v "//"; then
    echo -e "${GREEN}✓ Refresh tokens implementado${NC}"
else
    echo -e "${YELLOW}⚠ Refresh tokens no implementado${NC}"
    echo -e "${YELLOW}Recomendación: Implementar refresh tokens para mejor UX${NC}"
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  RESUMEN Y RECOMENDACIONES"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

echo -e "${BLUE}Prioridad ALTA (Implementar Ya):${NC}"
echo "  1. Agregar campos de auditoría (CreadoPor/ModificadoPor) a todas las entidades"
echo "  2. Crear UsuariosController para gestión de usuarios"
echo "  3. Implementar tests automatizados de autorización"
echo ""

echo -e "${BLUE}Prioridad MEDIA (Próximos Sprints):${NC}"
echo "  4. Implementar Activity Log para trazabilidad"
echo "  5. Agregar Rate Limiting para prevenir abuso"
echo "  6. Implementar Refresh Tokens para mejor UX"
echo ""

echo -e "${BLUE}Prioridad BAJA (Mejoras Futuras):${NC}"
echo "  7. Implementar Session Management"
echo "  8. Agregar 2FA para roles críticos"
echo "  9. Mejorar security headers"
echo ""

echo "Para más detalles, ver: ANALISIS_SEGURIDAD_Y_USUARIOS.md"
echo ""
