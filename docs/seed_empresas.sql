-- ============================================================
-- SEED MULTI-EMPRESA — ejecutar manualmente en la base de datos
-- ============================================================
-- Este script crea una empresa y asocia las sucursales existentes.
-- No hay UI en POS para administrar esto — se gestiona directo en BD.
--
-- USO:
--   docker exec sincopos-db psql -U posuser -d sincopos -f /path/to/seed_empresas.sql
--   o copiar/pegar en DBeaver / pgAdmin
-- ============================================================

-- 1. Insertar empresa(s)
INSERT INTO public."Empresas" ("Nombre", "Nit", "RazonSocial", "Activo", "FechaCreacion")
VALUES
    ('Demo S.A.S', '900123456-1', 'Demo Empresa S.A.S', true, NOW())
ON CONFLICT DO NOTHING;

-- 2. Ver IDs de empresas creadas
SELECT "Id", "Nombre", "Nit" FROM public."Empresas";

-- 3. Asignar empresa a sucursales (ajustar EmpresaId según el Id del paso anterior)
--    Ejemplo: empresa con Id = 1, sucursal con Id = 1
UPDATE public.sucursales
SET "EmpresaId" = 1
WHERE "Id" IN (
    SELECT "Id" FROM public.sucursales WHERE "EmpresaId" IS NULL
);

-- 4. Verificar resultado
SELECT s."Id", s."Nombre", s."EmpresaId", e."Nombre" AS empresa
FROM public.sucursales s
LEFT JOIN public."Empresas" e ON e."Id" = s."EmpresaId"
ORDER BY s."Id";

-- ============================================================
-- PARA AGREGAR UNA SEGUNDA EMPRESA:
-- ============================================================
-- INSERT INTO public."Empresas" ("Nombre", "Nit", "RazonSocial", "Activo", "FechaCreacion")
-- VALUES ('Empresa B Ltda', '800999888-2', 'Empresa B Ltda', true, NOW());
--
-- UPDATE public.sucursales
-- SET "EmpresaId" = 2
-- WHERE "Id" IN (5, 6, 7);  -- sucursales que pertenecen a empresa 2
-- ============================================================
