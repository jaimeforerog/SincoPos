-- ==============================================================================
-- Script de Seed de Datos: 50 Proveedores y 50 Clientes
-- EmpresaId = 1
-- ==============================================================================

DO $$
DECLARE
    i INT;
    v_identificacion VARCHAR(50);
BEGIN
    -- 1. Generar 50 Proveedores (TipoTercero = 1, NIT = 1)
    FOR i IN 1..50 LOOP
        v_identificacion := '900' || LPAD(i::text, 6, '0');
        
        INSERT INTO public.terceros (
            tipo_identificacion,
            identificacion,
            nombre,
            tipo_tercero,
            telefono,
            email,
            direccion,
            ciudad,
            origen_datos,
            activo,
            fecha_creacion,
            "CreadoPor",
            perfil_tributario,
            es_gran_contribuyente,
            es_autorretenedor,
            es_responsable_iva,
            "EmpresaId"
        )
        VALUES (
            1, -- NIT
            v_identificacion,
            'Proveedor de prueba ' || i,
            1, -- Proveedor
            '300' || LPAD(i::text, 7, '0'),
            'proveedor' || i || '@test.com',
            'Calle ' || i || ' # ' || (i * 2) || '-' || (i * 3),
            'Bogotá',
            0, -- Local
            true,
            NOW(),
            'seed_terceros',
            'REGIMEN_COMUN',
            false,
            false,
            true,
            1 -- Empresa 1
        )
        ON CONFLICT (identificacion) DO NOTHING;
    END LOOP;

    -- 2. Generar 50 Clientes (TipoTercero = 0, CC = 0)
    FOR i IN 1..50 LOOP
        v_identificacion := '100' || LPAD(i::text, 7, '0');
        
        INSERT INTO public.terceros (
            tipo_identificacion,
            identificacion,
            nombre,
            tipo_tercero,
            telefono,
            email,
            direccion,
            ciudad,
            origen_datos,
            activo,
            fecha_creacion,
            "CreadoPor",
            perfil_tributario,
            es_gran_contribuyente,
            es_autorretenedor,
            es_responsable_iva,
            "EmpresaId"
        )
        VALUES (
            0, -- CC
            v_identificacion,
            'Cliente de prueba ' || i,
            0, -- Cliente
            '310' || LPAD(i::text, 7, '0'),
            'cliente' || i || '@test.com',
            'Carrera ' || i || ' # ' || (i * 2) || '-' || (i * 3),
            'Medellín',
            0, -- Local
            true,
            NOW(),
            'seed_terceros',
            'PERSONA_NATURAL',
            false,
            false,
            false,
            1 -- Empresa 1
        )
        ON CONFLICT (identificacion) DO NOTHING;
    END LOOP;

END $$;
