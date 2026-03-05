-- ============================================================================
-- SCRIPT COMPLETO DE CONFIGURACIÓN PARA DESARROLLO
-- ============================================================================
-- Este script crea/verifica:
-- 1. Sucursal de desarrollo (si no existe)
-- 2. Usuario de desarrollo
-- 3. Caja de desarrollo (si no existe)
-- ============================================================================

-- PASO 1: Verificar/Crear Sucursal
-- ============================================================================
DO $$
DECLARE
    v_sucursal_id INT;
BEGIN
    -- Buscar si existe alguna sucursal
    SELECT "Id" INTO v_sucursal_id FROM public.sucursales ORDER BY "Id" LIMIT 1;

    IF v_sucursal_id IS NULL THEN
        -- No hay sucursales, crear una nueva
        INSERT INTO public.sucursales (
            nombre,
            direccion,
            ciudad,
            telefono,
            email,
            metodo_costeo,
            activo,
            fecha_creacion,
            creado_por
        ) VALUES (
            'Sucursal Principal',
            'Calle Principal #123',
            'Bogotá',
            '+57 300 0000000',
            'principal@sincopos.com',
            0,  -- 0 = Promedio Ponderado
            true,
            NOW(),
            'sistema'
        ) RETURNING "Id" INTO v_sucursal_id;

        RAISE NOTICE 'Sucursal creada con ID: %', v_sucursal_id;
    ELSE
        RAISE NOTICE 'Usando sucursal existente con ID: %', v_sucursal_id;
    END IF;

    -- Guardar el ID en una tabla temporal para usarlo después
    CREATE TEMP TABLE IF NOT EXISTS temp_config (
        sucursal_id INT
    );
    DELETE FROM temp_config;
    INSERT INTO temp_config VALUES (v_sucursal_id);
END $$;

-- PASO 2: Crear/Actualizar Usuario de Desarrollo
-- ============================================================================
DO $$
DECLARE
    v_sucursal_id INT;
    v_usuario_id INT;
BEGIN
    -- Obtener el ID de la sucursal
    SELECT sucursal_id INTO v_sucursal_id FROM temp_config;

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
        fecha_creacion
    ) VALUES (
        'dev-user-1',
        'dev@sincopos.com',
        'Usuario Desarrollo',
        '+57 300 0000000',
        'admin',
        v_sucursal_id,
        NOW(),
        true,
        NOW()
    ) RETURNING id INTO v_usuario_id;

    RAISE NOTICE 'Usuario de desarrollo creado con ID: %', v_usuario_id;
    RAISE NOTICE '  Email: dev@sincopos.com';
    RAISE NOTICE '  Rol: admin';
    RAISE NOTICE '  Sucursal ID: %', v_sucursal_id;
END $$;

-- PASO 3: Crear Caja de Desarrollo (si no existe)
-- ============================================================================
DO $$
DECLARE
    v_sucursal_id INT;
    v_caja_id INT;
    v_caja_existente INT;
BEGIN
    -- Obtener el ID de la sucursal
    SELECT sucursal_id INTO v_sucursal_id FROM temp_config;

    -- Verificar si ya existe alguna caja en esta sucursal
    SELECT "Id" INTO v_caja_existente
    FROM public.cajas
    WHERE sucursal_id = v_sucursal_id
    LIMIT 1;

    IF v_caja_existente IS NULL THEN
        -- No hay cajas, crear una
        INSERT INTO public.cajas (
            nombre,
            sucursal_id,
            estado,
            monto_apertura,
            monto_actual,
            activo,
            fecha_creacion
        ) VALUES (
            'Caja Principal',
            v_sucursal_id,
            1,  -- 1 = Cerrada (el cajero la debe abrir desde la app)
            0.00,
            0.00,
            true,
            NOW()
        ) RETURNING "Id" INTO v_caja_id;

        RAISE NOTICE 'Caja creada con ID: %', v_caja_id;
        RAISE NOTICE '  Estado: Cerrada (debe abrirse desde la aplicación)';
    ELSE
        RAISE NOTICE 'Ya existe una caja con ID: %', v_caja_existente;
    END IF;
END $$;

-- Limpiar tabla temporal
DROP TABLE IF EXISTS temp_config;

-- ============================================================================
-- VERIFICACIÓN FINAL
-- ============================================================================
SELECT
    '=== CONFIGURACIÓN DE DESARROLLO COMPLETADA ===' as mensaje;

SELECT
    'SUCURSALES' as tipo,
    "Id" as id,
    nombre,
    ciudad,
    activo
FROM public.sucursales
ORDER BY "Id";

SELECT
    'USUARIOS' as tipo,
    id,
    keycloak_id,
    email,
    nombre_completo,
    rol,
    sucursal_default_id,
    activo
FROM public.usuarios
WHERE email = 'dev@sincopos.com';

SELECT
    'CAJAS' as tipo,
    c."Id" as id,
    c.nombre,
    s.nombre as sucursal,
    CASE c.estado
        WHEN 0 THEN 'Abierta'
        WHEN 1 THEN 'Cerrada'
        ELSE 'Desconocido'
    END as estado,
    c.activo
FROM public.cajas c
INNER JOIN public.sucursales s ON c.sucursal_id = s."Id"
ORDER BY c."Id";
