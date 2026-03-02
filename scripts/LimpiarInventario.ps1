# Script para limpiar inventario antes de prueba FIFO
$ErrorActionPreference = "Stop"

Write-Host "===========================================" -ForegroundColor Cyan
Write-Host "  LIMPIEZA DE INVENTARIO                  " -ForegroundColor Cyan
Write-Host "===========================================" -ForegroundColor Cyan
Write-Host ""

$ProductoId = "11111111-1111-1111-1111-111111111111"

# Conectar a PostgreSQL y limpiar
Write-Host "[1/4] Eliminando ventas anteriores..." -ForegroundColor Yellow
psql -h localhost -U postgres -d sincopos -c "DELETE FROM public.detalles_venta WHERE producto_id = '$ProductoId';" 2>$null
psql -h localhost -U postgres -d sincopos -c "DELETE FROM public.ventas WHERE id NOT IN (SELECT DISTINCT venta_id FROM public.detalles_venta);" 2>$null
Write-Host "  OK - Ventas eliminadas" -ForegroundColor Green

Write-Host "[2/4] Eliminando lotes de inventario..." -ForegroundColor Yellow
psql -h localhost -U postgres -d sincopos -c "DELETE FROM public.lotes_inventario WHERE producto_id = '$ProductoId';" 2>$null
Write-Host "  OK - Lotes eliminados" -ForegroundColor Green

Write-Host "[3/4] Eliminando stock..." -ForegroundColor Yellow
psql -h localhost -U postgres -d sincopos -c "DELETE FROM public.stock WHERE producto_id = '$ProductoId';" 2>$null
Write-Host "  OK - Stock eliminado" -ForegroundColor Green

Write-Host "[4/4] Eliminando eventos de Marten..." -ForegroundColor Yellow
# Calcular stream_id determinístico
$input = "inv-$ProductoId-1"
$md5 = [System.Security.Cryptography.MD5]::Create()
$hash = $md5.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($input))
$streamId = [System.Guid]::new($hash)

psql -h localhost -U postgres -d sincopos -c "DELETE FROM events.mt_events WHERE stream_id = '$streamId';" 2>$null
psql -h localhost -U postgres -d sincopos -c "DELETE FROM events.mt_streams WHERE id = '$streamId';" 2>$null
Write-Host "  OK - Eventos eliminados" -ForegroundColor Green

Write-Host ""
Write-Host "===========================================" -ForegroundColor Green
Write-Host "  LIMPIEZA COMPLETADA                     " -ForegroundColor Green
Write-Host "===========================================" -ForegroundColor Green
Write-Host ""
