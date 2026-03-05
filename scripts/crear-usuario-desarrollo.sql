-- Script para crear usuario de desarrollo
-- Ejecutar este script una sola vez en la base de datos de desarrollo

-- Eliminar usuario existente si existe
DELETE FROM public.usuarios WHERE email = 'dev@sincopos.com';

-- Insertar usuario de desarrollo
INSERT INTO public.usuarios (
    keycloak_id,
    email,
    nombre_completo,
    telefono,
    rol,
    sucursal_default_id,
    ultimo_acceso,
    activo,
    fecha_creacion,
    fecha_modificacion
) VALUES (
    'dev-user-1',                           -- keycloak_id (mismo que en DevAuthProvider)
    'dev@sincopos.com',                     -- email
    'Usuario Desarrollo',                    -- nombre_completo
    '+57 300 0000000',                      -- telefono
    'admin',                                -- rol (admin tiene todos los permisos)
    1,                                      -- sucursal_default_id (asume que existe sucursal con ID 1)
    NOW(),                                  -- ultimo_acceso
    true,                                   -- activo
    NOW(),                                  -- fecha_creacion
    NOW()                                   -- fecha_modificacion
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
