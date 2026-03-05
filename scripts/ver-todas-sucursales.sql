-- Ver TODAS las sucursales (activas e inactivas)
SELECT
    "Id" as id,
    nombre,
    activo,
    fecha_creacion,
    metodo_costeo
FROM public.sucursales
ORDER BY "Id";
