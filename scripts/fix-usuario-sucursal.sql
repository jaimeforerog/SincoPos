-- Actualizar usuario de desarrollo para usar sucursal activa
UPDATE public.usuarios
SET sucursal_default_id = 152
WHERE email = 'dev@sincopos.com';

-- Verificar el cambio
SELECT
    id,
    email,
    nombre_completo,
    sucursal_default_id
FROM public.usuarios
WHERE email = 'dev@sincopos.com';
