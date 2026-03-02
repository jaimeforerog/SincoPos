-- ============================================
-- Script de Validación de Activity Logs
-- ============================================

\echo '=========================================='
\echo 'VALIDACIÓN DEL SISTEMA DE ACTIVITY LOG'
\echo '=========================================='
\echo ''

-- 1. Verificar que la tabla existe
\echo '1. Verificando que la tabla activity_logs existe...'
SELECT
    tablename,
    schemaname
FROM pg_tables
WHERE tablename = 'activity_logs';
\echo ''

-- 2. Verificar índices
\echo '2. Verificando índices estratégicos...'
SELECT
    indexname,
    indexdef
FROM pg_indexes
WHERE tablename = 'activity_logs'
ORDER BY indexname;
\echo ''

-- 3. Contar registros totales
\echo '3. Total de logs registrados:'
SELECT COUNT(*) as total_logs FROM activity_logs;
\echo ''

-- 4. Logs por tipo
\echo '4. Distribución de logs por tipo:'
SELECT
    CASE tipo
        WHEN 1 THEN 'Caja'
        WHEN 2 THEN 'Venta'
        WHEN 3 THEN 'Inventario'
        WHEN 4 THEN 'Usuario'
        WHEN 10 THEN 'Precio'
        WHEN 11 THEN 'Producto'
        WHEN 12 THEN 'Costeo'
        WHEN 20 THEN 'Configuracion'
        WHEN 99 THEN 'Sistema'
        ELSE 'Otro'
    END as tipo_actividad,
    COUNT(*) as cantidad,
    ROUND(COUNT(*) * 100.0 / SUM(COUNT(*)) OVER (), 2) as porcentaje
FROM activity_logs
GROUP BY tipo
ORDER BY cantidad DESC;
\echo ''

-- 5. Logs de hoy
\echo '5. Actividad del día actual:'
SELECT
    COUNT(*) as logs_hoy,
    COUNT(DISTINCT usuario_email) as usuarios_activos,
    SUM(CASE WHEN exitosa THEN 1 ELSE 0 END) as exitosas,
    SUM(CASE WHEN NOT exitosa THEN 1 ELSE 0 END) as fallidas
FROM activity_logs
WHERE fecha_hora >= CURRENT_DATE;
\echo ''

-- 6. Últimos 10 logs
\echo '6. Últimos 10 logs registrados:'
SELECT
    id,
    TO_CHAR(fecha_hora, 'YYYY-MM-DD HH24:MI:SS') as fecha,
    usuario_email,
    accion,
    CASE tipo
        WHEN 1 THEN 'Caja'
        WHEN 2 THEN 'Venta'
        WHEN 3 THEN 'Inventario'
        WHEN 4 THEN 'Usuario'
        ELSE tipo::text
    END as tipo,
    CASE WHEN exitosa THEN 'OK' ELSE 'FALLO' END as resultado,
    LEFT(descripcion, 50) as descripcion_corta
FROM activity_logs
ORDER BY fecha_hora DESC
LIMIT 10;
\echo ''

-- 7. Usuarios más activos
\echo '7. Top 5 usuarios más activos:'
SELECT
    usuario_email,
    COUNT(*) as total_acciones,
    MAX(fecha_hora) as ultima_actividad
FROM activity_logs
GROUP BY usuario_email
ORDER BY total_acciones DESC
LIMIT 5;
\echo ''

-- 8. Acciones más frecuentes
\echo '8. Top 10 acciones más ejecutadas:'
SELECT
    accion,
    COUNT(*) as cantidad
FROM activity_logs
GROUP BY accion
ORDER BY cantidad DESC
LIMIT 10;
\echo ''

-- 9. Verificar logs con datos JSONB
\echo '9. Logs con datos JSONB (últimos 5):'
SELECT
    id,
    accion,
    CASE
        WHEN datos_anteriores IS NOT NULL THEN 'Sí'
        ELSE 'No'
    END as tiene_datos_anteriores,
    CASE
        WHEN datos_nuevos IS NOT NULL THEN 'Sí'
        ELSE 'No'
    END as tiene_datos_nuevos
FROM activity_logs
WHERE datos_anteriores IS NOT NULL OR datos_nuevos IS NOT NULL
ORDER BY fecha_hora DESC
LIMIT 5;
\echo ''

-- 10. Performance de índice principal
\echo '10. Test de performance del índice principal:'
EXPLAIN (ANALYZE, BUFFERS)
SELECT *
FROM activity_logs
WHERE fecha_hora >= CURRENT_DATE - INTERVAL '7 days'
  AND tipo = 1
ORDER BY fecha_hora DESC
LIMIT 50;
\echo ''

-- 11. Logs de acciones críticas (últimas 24h)
\echo '11. Acciones críticas (últimas 24 horas):'
SELECT
    TO_CHAR(fecha_hora, 'HH24:MI:SS') as hora,
    usuario_email,
    accion,
    LEFT(descripcion, 60) as descripcion
FROM activity_logs
WHERE fecha_hora >= NOW() - INTERVAL '24 hours'
  AND accion IN ('AperturaCaja', 'CierreCaja', 'AnularVenta', 'CambiarEstadoUsuario')
ORDER BY fecha_hora DESC
LIMIT 20;
\echo ''

-- 12. Resumen de salud del sistema
\echo '12. Resumen de salud del sistema:'
SELECT
    'Total logs' as metrica,
    COUNT(*)::text as valor
FROM activity_logs
UNION ALL
SELECT
    'Logs últimas 24h',
    COUNT(*)::text
FROM activity_logs
WHERE fecha_hora >= NOW() - INTERVAL '24 hours'
UNION ALL
SELECT
    'Tasa de éxito (%)',
    ROUND(AVG(CASE WHEN exitosa THEN 100 ELSE 0 END), 2)::text
FROM activity_logs
UNION ALL
SELECT
    'Usuarios únicos',
    COUNT(DISTINCT usuario_email)::text
FROM activity_logs
UNION ALL
SELECT
    'Tamaño tabla (MB)',
    ROUND(pg_total_relation_size('activity_logs') / 1024.0 / 1024.0, 2)::text;
\echo ''

\echo '=========================================='
\echo 'VALIDACIÓN COMPLETADA'
\echo '=========================================='
