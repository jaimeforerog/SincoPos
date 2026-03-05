-- Script para poblar impuestos iniciales
-- Ejecutar este script en la base de datos SincoPos

-- Limpiar impuestos existentes si es necesario
-- DELETE FROM "Impuestos";

-- Insertar impuestos comunes en Colombia
INSERT INTO "Impuestos" ("Nombre", "Porcentaje", "Activo", "FechaCreacion")
VALUES
    ('Exento 0%', 0.00, true, NOW()),
    ('IVA 5%', 0.05, true, NOW()),
    ('IVA 8%', 0.08, true, NOW()),
    ('IVA 19%', 0.19, true, NOW())
ON CONFLICT DO NOTHING;

-- Verificar los impuestos creados
SELECT * FROM "Impuestos" WHERE "Activo" = true;
