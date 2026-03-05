-- Eliminar tablas de agrupaciones
DROP TABLE IF EXISTS public.detalle_agrupaciones CASCADE;
DROP TABLE IF EXISTS public.agrupaciones CASCADE;

-- Eliminar registro de migración
DELETE FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302232850_AgregarAgrupaciones';
