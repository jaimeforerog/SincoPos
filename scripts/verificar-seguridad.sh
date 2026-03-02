#!/bin/bash

# Script de Verificación de Seguridad - SincoPOS
# Verifica que todas las protecciones estén implementadas correctamente

echo "=========================================="
echo "   VERIFICACIÓN DE SEGURIDAD - SINCOPOS"
echo "=========================================="
echo ""

# Colores
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

PASSED=0
FAILED=0
WARNINGS=0

# Función para verificar archivos
check_file() {
    local file=$1
    local pattern=$2
    local description=$3

    echo -n "Verificando: $description... "

    if [ ! -f "$file" ]; then
        echo -e "${RED}✗ Archivo no encontrado${NC}"
        ((FAILED++))
        return 1
    fi

    if grep -q "$pattern" "$file"; then
        echo -e "${GREEN}✓ OK${NC}"
        ((PASSED++))
        return 0
    else
        echo -e "${RED}✗ No encontrado${NC}"
        ((FAILED++))
        return 1
    fi
}

# Función para contar ocurrencias
count_pattern() {
    local file=$1
    local pattern=$2
    grep -o "$pattern" "$file" | wc -l
}

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  1. VERIFICACIÓN DE CONTROLADORES"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

CONTROLLERS_DIR="POS.Api/Controllers"

# Array de controladores que deben estar protegidos
declare -a CONTROLLERS=(
    "VentasController.cs"
    "ProductosController.cs"
    "InventarioController.cs"
    "CategoriasController.cs"
    "ImpuestosController.cs"
    "SucursalesController.cs"
    "TercerosController.cs"
    "PreciosController.cs"
    "CajasController.cs"
)

for controller in "${CONTROLLERS[@]}"; do
    file="$CONTROLLERS_DIR/$controller"

    echo "━━━ $controller ━━━"

    # Verificar import de Authorization
    check_file "$file" "using Microsoft.AspNetCore.Authorization" "Import Authorization"

    # Verificar [Authorize] a nivel de clase
    check_file "$file" "\[Authorize\]" "Atributo [Authorize]"

    # Contar políticas específicas
    policies=$(count_pattern "$file" '\[Authorize(Policy = ')
    if [ "$policies" -gt 0 ]; then
        echo -e "  ${BLUE}ℹ${NC} Políticas específicas encontradas: $policies"
    fi

    echo ""
done

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  2. VERIFICACIÓN DE POLÍTICAS CRÍTICAS"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# VentasController - Crear venta debe ser Cajero
check_file "$CONTROLLERS_DIR/VentasController.cs" 'Policy = "Cajero"' "VentasController: POST requiere Cajero"

# VentasController - Anular venta debe ser Supervisor
check_file "$CONTROLLERS_DIR/VentasController.cs" 'Policy = "Supervisor"' "VentasController: Anular requiere Supervisor"

# ProductosController - Crear debe ser Supervisor
check_file "$CONTROLLERS_DIR/ProductosController.cs" 'Policy = "Supervisor"' "ProductosController: POST requiere Supervisor"

# ProductosController - Eliminar debe ser Admin
check_file "$CONTROLLERS_DIR/ProductosController.cs" 'Policy = "Admin"' "ProductosController: DELETE requiere Admin"

# SucursalesController - Crear debe ser Admin
check_file "$CONTROLLERS_DIR/SucursalesController.cs" 'Policy = "Admin"' "SucursalesController: Operaciones requieren Admin"

# ImpuestosController - Crear debe ser Admin
check_file "$CONTROLLERS_DIR/ImpuestosController.cs" 'Policy = "Admin"' "ImpuestosController: Operaciones requieren Admin"

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  3. VERIFICACIÓN DE CONFIGURACIÓN"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Program.cs - Autenticación configurada
check_file "POS.Api/Program.cs" "AddAuthentication" "Program.cs: Autenticación configurada"

# Program.cs - Autorización configurada
check_file "POS.Api/Program.cs" "AddAuthorization" "Program.cs: Autorización configurada"

# Program.cs - Políticas definidas
check_file "POS.Api/Program.cs" 'AddPolicy("Admin"' "Program.cs: Política Admin definida"
check_file "POS.Api/Program.cs" 'AddPolicy("Supervisor"' "Program.cs: Política Supervisor definida"
check_file "POS.Api/Program.cs" 'AddPolicy("Cajero"' "Program.cs: Política Cajero definida"
check_file "POS.Api/Program.cs" 'AddPolicy("Vendedor"' "Program.cs: Política Vendedor definida"

# Program.cs - Middleware de autenticación
check_file "POS.Api/Program.cs" "UseAuthentication" "Program.cs: Middleware UseAuthentication"
check_file "POS.Api/Program.cs" "UseAuthorization" "Program.cs: Middleware UseAuthorization"

# appsettings - Configuración de Keycloak
check_file "POS.Api/appsettings.Development.json" "Authentication" "appsettings: Sección Authentication"
check_file "POS.Api/appsettings.Development.json" "Authority" "appsettings: Authority configurado"
check_file "POS.Api/appsettings.Development.json" "Audience" "appsettings: Audience configurado"

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  4. VERIFICACIÓN DE EXTENSIONES"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# ClaimsPrincipalExtensions
check_file "POS.Api/Extensions/ClaimsPrincipalExtensions.cs" "GetKeycloakId" "Extensions: GetKeycloakId"
check_file "POS.Api/Extensions/ClaimsPrincipalExtensions.cs" "GetEmail" "Extensions: GetEmail"
check_file "POS.Api/Extensions/ClaimsPrincipalExtensions.cs" "GetRoles" "Extensions: GetRoles"
check_file "POS.Api/Extensions/ClaimsPrincipalExtensions.cs" "TieneRol" "Extensions: TieneRol"

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  5. ESTADÍSTICAS DE SEGURIDAD"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Contar total de [Authorize]
total_authorize=0
for controller in "${CONTROLLERS[@]}"; do
    file="$CONTROLLERS_DIR/$controller"
    if [ -f "$file" ]; then
        count=$(count_pattern "$file" '\[Authorize')
        total_authorize=$((total_authorize + count))
    fi
done

echo -e "${BLUE}Total de atributos [Authorize]:${NC} $total_authorize"

# Contar políticas específicas
total_admin=$(grep -r 'Policy = "Admin"' "$CONTROLLERS_DIR" 2>/dev/null | wc -l)
total_supervisor=$(grep -r 'Policy = "Supervisor"' "$CONTROLLERS_DIR" 2>/dev/null | wc -l)
total_cajero=$(grep -r 'Policy = "Cajero"' "$CONTROLLERS_DIR" 2>/dev/null | wc -l)
total_vendedor=$(grep -r 'Policy = "Vendedor"' "$CONTROLLERS_DIR" 2>/dev/null | wc -l)

echo -e "${BLUE}Políticas Admin:${NC} $total_admin"
echo -e "${BLUE}Políticas Supervisor:${NC} $total_supervisor"
echo -e "${BLUE}Políticas Cajero:${NC} $total_cajero"
echo -e "${BLUE}Políticas Vendedor:${NC} $total_vendedor"

# Controladores protegidos
protected_controllers=0
for controller in "${CONTROLLERS[@]}"; do
    file="$CONTROLLERS_DIR/$controller"
    if [ -f "$file" ] && grep -q '\[Authorize\]' "$file"; then
        protected_controllers=$((protected_controllers + 1))
    fi
done

echo -e "${BLUE}Controladores protegidos:${NC} $protected_controllers/${#CONTROLLERS[@]}"

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  RESUMEN DE VERIFICACIÓN"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

echo -e "${GREEN}✓ Pruebas pasadas:${NC} $PASSED"
echo -e "${RED}✗ Pruebas fallidas:${NC} $FAILED"

if [ "$WARNINGS" -gt 0 ]; then
    echo -e "${YELLOW}⚠ Advertencias:${NC} $WARNINGS"
fi

echo ""

if [ $FAILED -eq 0 ]; then
    echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${GREEN}  ✅ TODAS LAS VERIFICACIONES PASARON${NC}"
    echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo ""
    echo "El sistema está correctamente protegido."
    echo "Todos los controladores tienen autenticación implementada."
    echo ""
    exit 0
else
    echo -e "${RED}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${RED}  ⚠️  ALGUNAS VERIFICACIONES FALLARON${NC}"
    echo -e "${RED}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo ""
    echo "Por favor revisa los errores arriba."
    echo ""
    exit 1
fi
