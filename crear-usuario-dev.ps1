# ============================================================================
# Script para crear usuario de desarrollo en PostgreSQL
# ============================================================================

$ErrorActionPreference = "Stop"

Write-Host "=== Creando usuario de desarrollo ===" -ForegroundColor Cyan
Write-Host ""

# Parámetros de conexión
$pgHost = "localhost"
$pgPort = "5432"
$pgDatabase = "sincopos"
$pgUser = "postgres"
$pgPassword = "postgrade"

# SQL a ejecutar
$sql = @"
-- Eliminar usuario existente si existe
DELETE FROM public.usuarios WHERE email = 'dev@sincopos.com';

-- Crear usuario de desarrollo
INSERT INTO public.usuarios (
    keycloak_id,
    email,
    nombre_completo,
    telefono,
    rol,
    sucursal_default_id,
    ultimo_acceso,
    activo,
    fecha_creacion
) VALUES (
    'dev-user-1',
    'dev@sincopos.com',
    'Usuario Desarrollo',
    '+57 300 0000000',
    'admin',
    152,
    NOW(),
    true,
    NOW()
);

-- Verificar
SELECT
    id,
    keycloak_id,
    email,
    nombre_completo,
    rol,
    sucursal_default_id,
    activo
FROM public.usuarios
WHERE email = 'dev@sincopos.com';
"@

# Guardar SQL en archivo temporal
$tempFile = [System.IO.Path]::GetTempFileName() + ".sql"
$sql | Out-File -FilePath $tempFile -Encoding UTF8

Write-Host "SQL guardado en: $tempFile" -ForegroundColor Gray
Write-Host ""

# Buscar psql.exe
$psqlPaths = @(
    "C:\Program Files\PostgreSQL\16\bin\psql.exe",
    "C:\Program Files\PostgreSQL\15\bin\psql.exe",
    "C:\Program Files\PostgreSQL\14\bin\psql.exe",
    "C:\Program Files (x86)\PostgreSQL\16\bin\psql.exe",
    "C:\Program Files (x86)\PostgreSQL\15\bin\psql.exe"
)

$psqlExe = $null
foreach ($path in $psqlPaths) {
    if (Test-Path $path) {
        $psqlExe = $path
        break
    }
}

if (-not $psqlExe) {
    Write-Host "ERROR: No se encontró psql.exe" -ForegroundColor Red
    Write-Host ""
    Write-Host "SOLUCIÓN ALTERNATIVA: Ejecuta el siguiente SQL manualmente en pgAdmin:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host $sql -ForegroundColor White
    Write-Host ""
    Remove-Item $tempFile -Force
    exit 1
}

Write-Host "Usando psql: $psqlExe" -ForegroundColor Green
Write-Host ""

# Configurar variable de entorno para password
$env:PGPASSWORD = $pgPassword

# Ejecutar SQL
Write-Host "Ejecutando SQL..." -ForegroundColor Yellow
& $psqlExe -h $pgHost -p $pgPort -U $pgUser -d $pgDatabase -f $tempFile

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✓ Usuario creado exitosamente!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Datos del usuario:" -ForegroundColor Cyan
    Write-Host "  - Email: dev@sincopos.com" -ForegroundColor White
    Write-Host "  - Keycloak ID: dev-user-1" -ForegroundColor White
    Write-Host "  - Sucursal: 152 (Suc PromedioPonderado)" -ForegroundColor White
    Write-Host "  - Rol: admin" -ForegroundColor White
    Write-Host ""
    Write-Host "IMPORTANTE: Recarga el frontend (F5) para que detecte las cajas abiertas" -ForegroundColor Yellow
} else {
    Write-Host ""
    Write-Host "ERROR: Falló la ejecución del SQL (código: $LASTEXITCODE)" -ForegroundColor Red
}

# Limpiar
Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
$env:PGPASSWORD = $null
