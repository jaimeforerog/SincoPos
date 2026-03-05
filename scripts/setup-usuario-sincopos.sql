-- ============================================================================
-- Crear usuario de desarrollo en la base de datos sincopos (minúsculas)
-- ============================================================================

-- Eliminar usuario existente si existe
DELETE FROM public.usuarios WHERE email = 'dev@sincopos.com';

-- Crear usuario de desarrollo usando sucursal 152 (Suc PromedioPonderado)
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
    152,  -- Suc PromedioPonderado en la base de datos sincopos
    NOW(),
    true,
    NOW()
);

-- Verificar que se creó correctamente
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

-- Mostrar sucursales disponibles
SELECT
    id,
    nombre,
    metodo_costeo,
    activo
FROM public.sucursales
WHERE activo = true
ORDER BY id;
