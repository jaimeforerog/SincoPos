# Script para ejecutar seed data en PostgreSQL local
param(
    [string]$ConnectionString = "Host=localhost;Port=5432;Database=sincopos;Username=postgres;Password=postgrade"
)

Write-Host "Ejecutando seed data..." -ForegroundColor Cyan

# Leer el archivo SQL
$sqlFile = Join-Path $PSScriptRoot "seed-data.sql"
$sql = Get-Content $sqlFile -Raw

# Ejecutar usando dotnet script
$scriptContent = @"
using Npgsql;

var connectionString = "$ConnectionString";
using var conn = new NpgsqlConnection(connectionString);
conn.Open();

var sql = @"$($sql.Replace('"', '""'))";

using var cmd = new NpgsqlCommand(sql, conn);
cmd.ExecuteNonQuery();

Console.WriteLine("✓ Seed data ejecutado correctamente");
"@

$scriptPath = Join-Path $env:TEMP "ExecuteSeed.csx"
$scriptContent | Out-File -FilePath $scriptPath -Encoding UTF8

try {
    # Intentar con dotnet script (si está instalado)
    dotnet script $scriptPath 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Datos de prueba creados exitosamente" -ForegroundColor Green
        exit 0
    }
}
catch {
    # Si falla, dar instrucciones manuales
    Write-Host "No se pudo ejecutar automáticamente." -ForegroundColor Yellow
    Write-Host "Ejecuta manualmente este SQL:" -ForegroundColor Yellow
    Write-Host $sql -ForegroundColor Gray
}
