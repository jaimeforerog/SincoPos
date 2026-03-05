-- Ver todas las sucursales en la base de datos
SELECT
    "Id" as id,
    nombre,
    activo,
    fecha_creacion
FROM public.sucursales
ORDER BY "Id";

-- Ver el usuario de desarrollo
SELECT
    id,
    email,
    nombre_completo,
    sucursal_default_id
FROM public.usuarios
WHERE email = 'dev@sincopos.com';
