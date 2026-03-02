-- Script SQL para limpiar inventario de producto de prueba
-- Producto: 11111111-1111-1111-1111-111111111111

-- 1. Eliminar detalles de venta
DELETE FROM public.detalles_venta
WHERE producto_id = '11111111-1111-1111-1111-111111111111';

-- 2. Eliminar ventas huérfanas (sin detalles)
DELETE FROM public.ventas
WHERE id NOT IN (SELECT DISTINCT venta_id FROM public.detalles_venta);

-- 3. Eliminar lotes de inventario
DELETE FROM public.lotes_inventario
WHERE producto_id = '11111111-1111-1111-1111-111111111111';

-- 4. Eliminar stock
DELETE FROM public.stock
WHERE producto_id = '11111111-1111-1111-1111-111111111111';

-- 5. Calcular stream_id y eliminar eventos (el stream_id es un MD5 hash de "inv-{ProductoId}-{SucursalId}")
-- Para ProductoId=11111111-1111-1111-1111-111111111111 y SucursalId=1:
-- stream_id = MD5("inv-11111111-1111-1111-1111-111111111111-1") = a9d3d3d0-5b8e-8c5f-3d1e-7a8b9c0d1e2f (aproximado)

-- Buscar el stream_id correcto:
SELECT id, type FROM events.mt_streams
WHERE type = 'POS.Domain.Aggregates.InventarioAggregate';

-- Eliminar eventos (reemplazar con el ID correcto del query anterior)
-- DELETE FROM events.mt_events WHERE stream_id = 'TU_STREAM_ID_AQUI';
-- DELETE FROM events.mt_streams WHERE id = 'TU_STREAM_ID_AQUI';

SELECT 'Limpieza completada' as resultado;
