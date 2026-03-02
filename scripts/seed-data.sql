-- Datos de prueba para SincoPos
-- Ejecutar en la base de datos sincopos

-- Crear sucursal de prueba
INSERT INTO public.sucursales (nombre, direccion, telefono, metodo_costeo, activo, fecha_creacion, "CreadoPor")
VALUES ('Sucursal Principal', 'Calle 123 #45-67', '3001234567', 0, true, NOW(), 'system')
ON CONFLICT (nombre) DO NOTHING;

-- Crear un producto de prueba
INSERT INTO public.productos (id, codigo_barras, nombre, descripcion, categoria_id, precio_venta, precio_costo, activo, fecha_creacion, "CreadoPor")
VALUES
  ('11111111-1111-1111-1111-111111111111'::uuid, 'PROD001', 'Producto Test 1', 'Producto para pruebas', 1, 50.00, 30.00, true, NOW(), 'system'),
  ('22222222-2222-2222-2222-222222222222'::uuid, 'PROD002', 'Producto Test 2', 'Otro producto', 1, 75.00, 45.00, true, NOW(), 'system')
ON CONFLICT (codigo_barras) DO NOTHING;

-- Crear caja de prueba
INSERT INTO public.cajas (nombre, sucursal_id, estado, monto_apertura, monto_actual, activo, fecha_creacion, "CreadoPor")
SELECT 'Caja Principal', s."Id", 0, 0, 0, true, NOW(), 'system'
FROM public.sucursales s
WHERE s.nombre = 'Sucursal Principal'
ON CONFLICT (sucursal_id, nombre) DO NOTHING;

-- Mostrar resumen
SELECT 'Datos creados exitosamente' as status;
SELECT COUNT(*) as sucursales FROM public.sucursales;
SELECT COUNT(*) as productos FROM public.productos;
SELECT COUNT(*) as cajas FROM public.cajas;
