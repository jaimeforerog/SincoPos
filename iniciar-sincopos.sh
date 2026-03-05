#!/bin/bash
# Script de inicio para SincoPos
# Ejecutar con: ./iniciar-sincopos.sh

echo "====================================="
echo "   Iniciando SincoPos"
echo "====================================="
echo ""

# 1. Iniciar Backend en segundo plano
echo "[1/3] Iniciando Backend..."
cd POS.Api
dotnet run &
BACKEND_PID=$!
cd ..

# Esperar a que el backend esté listo
echo "[2/3] Esperando a que el backend inicie..."
sleep 8

# Verificar si el backend está corriendo
if curl -s http://localhost:5086/api/sucursales -H "Authorization: Bearer dev-token-mock" > /dev/null; then
    echo "✓ Backend iniciado correctamente"

    # Verificar si hay sucursales
    SUCURSALES=$(curl -s http://localhost:5086/api/sucursales -H "Authorization: Bearer dev-token-mock")

    if [ "$SUCURSALES" = "[]" ]; then
        echo "[3/3] Creando sucursal de prueba..."

        curl -X POST http://localhost:5086/api/sucursales \
          -H "Content-Type: application/json" \
          -H "Authorization: Bearer dev-token-mock" \
          -d '{
            "nombre": "Sucursal Principal",
            "direccion": "Calle 123 # 45-67",
            "codigoPais": "CO",
            "nombrePais": "Colombia",
            "ciudad": "Bogotá",
            "telefono": "3001234567",
            "metodoCosteo": "PromedioPonderado"
          }' -s > /dev/null

        echo "✓ Sucursal creada"
    else
        echo "✓ Ya existen sucursales"
    fi
else
    echo "✗ Error: No se pudo conectar al backend"
    kill $BACKEND_PID
    exit 1
fi

echo ""
echo "====================================="
echo "   SincoPos Backend Listo!"
echo "====================================="
echo ""
echo "Backend: http://localhost:5086"
echo "Backend PID: $BACKEND_PID"
echo "Frontend: Ejecuta 'npm run dev' en la carpeta frontend"
echo ""
echo "Para iniciar el frontend:"
echo "  cd frontend"
echo "  npm run dev"
echo ""
echo "Para detener el backend:"
echo "  kill $BACKEND_PID"
echo ""
