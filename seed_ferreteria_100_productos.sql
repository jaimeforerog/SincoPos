-- ==============================================================================
-- Script de Seed de Datos: 100 Productos de Ferretería
-- EmpresaId = 1
-- ==============================================================================

DO $$
DECLARE
  c_cat_0 INT;
  c_cat_1 INT;
  c_cat_2 INT;
  c_cat_3 INT;
  c_cat_4 INT;
  c_cat_5 INT;
  c_cat_6 INT;
  c_cat_7 INT;
BEGIN

  -- 1. Insertar Categorias (Ignora si ya existen por nombre)
  -- Nota: El ON CONFLICT requiere restriccion de unicidad, como no estamos seguros si la hay, 
  --       verificamos su existencia de forma simple o confiamos en que no se rompa o se ajuste si falla.
  --       Para evitar errores de constraints faltantes, usaremos un UPSERT estilo DO NOTHING
  --       Si no hay indice unico en nombre, esto podria insertar duplicados, pero por el script solo lo corremos 1 vez.

  INSERT INTO public.categorias
    (nombre, descripcion, activo, "CreadoPor", "FechaCreacion", categoria_padre_id, nivel, ruta_completa, margen_ganancia, "EmpresaId")
  VALUES
    ('Herramientas Manuales', 'Herramientas de uso manual', true, 'seed_ferreteria', NOW(), NULL, 0, 'Herramientas Manuales', 0.35, 1),
    ('Herramientas Eléctricas', 'Herramientas motorizadas y accesorios', true, 'seed_ferreteria', NOW(), NULL, 0, 'Herramientas Eléctricas', 0.30, 1),
    ('Materiales de Construcción', 'Materiales pesados y obra negra', true, 'seed_ferreteria', NOW(), NULL, 0, 'Materiales de Construcción', 0.25, 1),
    ('Pinturas y Accesorios', 'Pinturas, brochas, rodillos e insumos', true, 'seed_ferreteria', NOW(), NULL, 0, 'Pinturas y Accesorios', 0.40, 1),
    ('Plomería', 'Tubos, conexiones y herramientas de agua', true, 'seed_ferreteria', NOW(), NULL, 0, 'Plomería', 0.35, 1),
    ('Electricidad', 'Cables y accesorios eléctricos', true, 'seed_ferreteria', NOW(), NULL, 0, 'Electricidad', 0.30, 1),
    ('Tornillería y Fijación', 'Tornillos, clavos, chazos y remaches', true, 'seed_ferreteria', NOW(), NULL, 0, 'Tornillería y Fijación', 0.45, 1),
    ('Seguridad Industrial', 'EPP y señalización de riesgos', true, 'seed_ferreteria', NOW(), NULL, 0, 'Seguridad Industrial', 0.40, 1);
    -- (Nota: Remueve ON CONFLICT si public.categorias no tiene constraint unique)

  -- 2. Obtener IDs recien insertados
  SELECT "Id" INTO c_cat_0 FROM public.categorias WHERE nombre='Herramientas Manuales' AND "EmpresaId"=1 ORDER BY "Id" DESC LIMIT 1;
  SELECT "Id" INTO c_cat_1 FROM public.categorias WHERE nombre='Herramientas Eléctricas' AND "EmpresaId"=1 ORDER BY "Id" DESC LIMIT 1;
  SELECT "Id" INTO c_cat_2 FROM public.categorias WHERE nombre='Materiales de Construcción' AND "EmpresaId"=1 ORDER BY "Id" DESC LIMIT 1;
  SELECT "Id" INTO c_cat_3 FROM public.categorias WHERE nombre='Pinturas y Accesorios' AND "EmpresaId"=1 ORDER BY "Id" DESC LIMIT 1;
  SELECT "Id" INTO c_cat_4 FROM public.categorias WHERE nombre='Plomería' AND "EmpresaId"=1 ORDER BY "Id" DESC LIMIT 1;
  SELECT "Id" INTO c_cat_5 FROM public.categorias WHERE nombre='Electricidad' AND "EmpresaId"=1 ORDER BY "Id" DESC LIMIT 1;
  SELECT "Id" INTO c_cat_6 FROM public.categorias WHERE nombre='Tornillería y Fijación' AND "EmpresaId"=1 ORDER BY "Id" DESC LIMIT 1;
  SELECT "Id" INTO c_cat_7 FROM public.categorias WHERE nombre='Seguridad Industrial' AND "EmpresaId"=1 ORDER BY "Id" DESC LIMIT 1;

  -- 3. Insertar 100 Productos
  INSERT INTO public.productos
    (id, codigo_barras, nombre, descripcion, categoria_id, precio_venta, precio_costo, activo, fecha_creacion, "CreadoPor", unidad_medida, "EsAlimentoUltraprocesado", maneja_lotes, "EmpresaId")
  VALUES
    -- Herramientas Manuales (12 items)
    (gen_random_uuid(), '7702000001', 'Martillo uña 16oz', 'Martillo con mango de madera', c_cat_0, 25000, 18000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000002', 'Destornillador Phillips #2', 'Destornillador estrella imantado', c_cat_0, 8500, 5000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000003', 'Destornillador Plano 1/4', 'Destornillador pala imantado', c_cat_0, 8500, 5000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000004', 'Alicate Universal 8"', 'Alicate de acero al carbono 8 pulg', c_cat_0, 18000, 12000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000005', 'Llave Ajustable 10"', 'Llave expansiva cromada 10 pulg', c_cat_0, 22000, 15000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000006', 'Cinta Métrica 5m', 'Flexómetro metálico 5 metros', c_cat_0, 12000, 7500, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000007', 'Nivel Burbuja 60cm', 'Nivel de aluminio de 3 burbujas', c_cat_0, 20000, 13000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000008', 'Serrucho c/madera 20"', 'Serrucho para carpintero 20 pulg', c_cat_0, 24000, 16000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000009', 'Juego Llaves Allen (9pzas)', 'Juego llaves hexagonales x9', c_cat_0, 15000, 9500, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000010', 'Tijera Cortalata', 'Tijera aviación para lámina', c_cat_0, 28000, 19000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000011', 'Bisturí Industrial', 'Cúter cuerpo metálico reforzado', c_cat_0, 6000, 3500, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000012', 'Hombre Solo de Presión', 'Pinza de presión 10 pulgadas', c_cat_0, 26000, 17500, true, NOW(), 'seed', '94', false, false, 1),

    -- Herramientas Eléctricas (12 items)
    (gen_random_uuid(), '7702000101', 'Taladro Percutor 700W', 'Taladro percutor 1/2" 700W 110V', c_cat_1, 150000, 110000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000102', 'Pulidora Angular 4-1/2 800W', 'Amoladora angular para disco 4.5"', c_cat_1, 140000, 105000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000103', 'Sierra Circular 1400W', 'Sierra circular 7-1/4" madera', c_cat_1, 280000, 210000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000104', 'Lijadora Rotoorbital 300W', 'Lijadora de órbita aleatoria 5"', c_cat_1, 160000, 120000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000105', 'Caladora 500W', 'Sierra caladora pendular 500W', c_cat_1, 130000, 95000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000106', 'Atornillador Inalámbrico 12V', 'Taladro atornillador a batería', c_cat_1, 220000, 165000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000107', 'Pistola Calor 2000W', 'Pistola de calor de 2 temperaturas', c_cat_1, 80000, 58000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000108', 'Rotomartillo SDS 800W', 'Rotomartillo encastre SDS Plus', c_cat_1, 350000, 260000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000109', 'Cepillo Eléctrico', 'Cepillo ahondador madera 750W', c_cat_1, 240000, 180000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000110', 'Fresadora 1200W', 'Ruteadora para madera base fija', c_cat_1, 310000, 235000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000111', 'Sopladora 600W', 'Sopladora / aspiradora 600W', c_cat_1, 95000, 70000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000112', 'Clavadora Neumática', 'Grapadora / Clavadora cal 18', c_cat_1, 180000, 135000, true, NOW(), 'seed', '94', false, false, 1),

    -- Materiales de Construcción (14 items)
    (gen_random_uuid(), '7702000201', 'Cemento Portland 50kg', 'Bulto de cemento gris', c_cat_2, 35000, 28000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000202', 'Bulto Arena Fina 40kg', 'Arena para pañete', c_cat_2, 7500, 4500, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000203', 'Bulto Grava 40kg', 'Gravilla fina mixta triturada', c_cat_2, 8000, 5000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000204', 'Ladrillo Prensado x100', 'Ladrillo caravista rojo prensado', c_cat_2, 95000, 75000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000205', 'Bloque Cemento n5', 'Bloque vibrado N5 (pac x10)', c_cat_2, 14000, 10500, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000206', 'Varilla Corrugada 1/2', 'Varilla de hierro corrugada 6m', c_cat_2, 28000, 22000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000207', 'Alambre Recocido Cal 18', 'Alambre negro dulce amarrar (Kg)', c_cat_2, 7000, 4500, true, NOW(), 'seed', 'KGM', false, false, 1),
    (gen_random_uuid(), '7702000208', 'Malla Electrosoldada', 'Malla 15x15 placa (mt2)', c_cat_2, 12000, 8500, true, NOW(), 'seed', 'MTR', false, false, 1),
    (gen_random_uuid(), '7702000209', 'Pegacor 25kg', 'Pegante para cerámica', c_cat_2, 24000, 18000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000210', 'Yeso 25kg', 'Yeso de construcción bulto 25kg', c_cat_2, 18000, 13000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000211', 'Cal Hidratada 20kg', 'Bulto de cal viva/hidratada', c_cat_2, 16000, 11500, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000212', 'Impermeabilizante Asfáltico 1Gl', 'Emulsión asfáltica tapagoteras', c_cat_2, 45000, 32000, true, NOW(), 'seed', 'GLL', false, false, 1),
    (gen_random_uuid(), '7702000213', 'Aditivo Sika 1 (2kg)', 'Impermeabilizante integral para mezcla', c_cat_2, 38000, 26000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000214', 'Hoja Drywall 1.22x2.44m', 'Lámina de yeso estándar placa', c_cat_2, 32000, 24000, true, NOW(), 'seed', '94', false, false, 1),

    -- Pinturas y Accesorios (12 items)
    (gen_random_uuid(), '7702000301', 'Vinilo Blanco Tipo 1 (1 Gl)', 'Pintura agua alta lavabilidad', c_cat_3, 45000, 32000, true, NOW(), 'seed', 'GLL', false, false, 1),
    (gen_random_uuid(), '7702000302', 'Vinilo Color (1 Gl)', 'Pintura vinilo varios colores claros', c_cat_3, 42000, 29000, true, NOW(), 'seed', 'GLL', false, false, 1),
    (gen_random_uuid(), '7702000303', 'Esmalte Negro (1 Gl)', 'Esmalte sintético brillante metal', c_cat_3, 55000, 40000, true, NOW(), 'seed', 'GLL', false, false, 1),
    (gen_random_uuid(), '7702000304', 'Anticorrosivo Gris (1 Gl)', 'Base anticorrosiva para metal', c_cat_3, 48000, 35000, true, NOW(), 'seed', 'GLL', false, false, 1),
    (gen_random_uuid(), '7702000305', 'Thinner Corriente (1 Gl)', 'Disolvente universal corriente', c_cat_3, 24000, 17000, true, NOW(), 'seed', 'GLL', false, false, 1),
    (gen_random_uuid(), '7702000306', 'Rodillo Antigota 9"', 'Rodillo de felpa antigota profesional', c_cat_3, 12000, 7500, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000307', 'Brocha Cerda 2"', 'Brocha mona cerda sintética 2 pulg', c_cat_3, 4500, 2800, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000308', 'Brocha Cerda 4"', 'Brocha mona gruesa 4 pulgadas', c_cat_3, 8500, 4800, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000309', 'Bandeja Pintura', 'Bandeja plástica estriada rodillo', c_cat_3, 6000, 3800, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000310', 'Cinta Enmascarar 3/4"', 'Cinta de papel para pintores roll', c_cat_3, 4500, 2500, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000311', 'Estuco Plástico (1 Gl)', 'Masilla lista estuco acrílico galón', c_cat_3, 28000, 19000, true, NOW(), 'seed', 'GLL', false, false, 1),
    (gen_random_uuid(), '7702000312', 'Sellador Acrílico (1 Gl)', 'Sellante tapa poros muros galón', c_cat_3, 22000, 15000, true, NOW(), 'seed', 'GLL', false, false, 1),

    -- Plomería (14 items)
    (gen_random_uuid(), '7702000401', 'Tubo PVC Presión 1/2 6m', 'Tubo PVC verde agua potable', c_cat_4, 18000, 12500, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000402', 'Tubo PVC Sanitario 3" 6m', 'Tubo alcantarillado PVC blanco', c_cat_4, 38000, 27500, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000403', 'Codo PVC 1/2 90°', 'Accesorio de PVC presión liso/liso', c_cat_4, 800, 450, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000404', 'Soldadura Líquida PVC 1/4Gl', 'Pegante para tubería PVC', c_cat_4, 18000, 12000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000405', 'Limpiador PVC 1/4Gl', 'Removedor para empalmes PVC', c_cat_4, 15000, 9500, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000406', 'Cinta Teflón 1/2"', 'Teflón para roscas rollo', c_cat_4, 1500, 800, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000407', 'Llave Lavamanos Sencilla', 'Grifería plástica lavamanos', c_cat_4, 14000, 9000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000408', 'Llave Lavaplatos Cuello Cisne', 'Grifería lavaplatos tipo ganso', c_cat_4, 28000, 19000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000409', 'Sifón P PVC', 'Sifón trampa P lavamanos', c_cat_4, 7500, 4800, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000410', 'Válvula Bola PVC 1/2', 'Registro de esfera compacto pvc', c_cat_4, 6500, 4200, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000411', 'Flotador Tanque 1/2', 'Válvula entrada flotante para inodoro', c_cat_4, 15000, 9800, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000412', 'Manguera Jardín 15m', 'Manguera verde con acople y pistola', c_cat_4, 38000, 26000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000413', 'Registro Ducha', 'Registro pomo acrílico cromo', c_cat_4, 25000, 17000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000414', 'Rejilla Piso Acero', 'Sifón de centro metálico 3x3in', c_cat_4, 9500, 6000, true, NOW(), 'seed', '94', false, false, 1),

    -- Electricidad (14 items)
    (gen_random_uuid(), '7702000501', 'Cable Cobre THHN Cal 12', 'Cable rígido forrado metro', c_cat_5, 2500, 1700, true, NOW(), 'seed', 'MTR', false, false, 1),
    (gen_random_uuid(), '7702000502', 'Cable Cobre THHN Cal 10', 'Cable grueso eléctrico metro', c_cat_5, 3800, 2600, true, NOW(), 'seed', 'MTR', false, false, 1),
    (gen_random_uuid(), '7702000503', 'Cinta Aislante Negra 20m', 'Cinta vinílica eléctrica', c_cat_5, 3500, 2000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000504', 'Interruptor Sencillo 10A', 'Suiche de pared blanco s/placa', c_cat_5, 4500, 2800, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000505', 'Tomacorriente Doble 15A', 'Toma de pared c/polo a tierra', c_cat_5, 5500, 3500, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000506', 'Caja Chalupa Rectangular', 'Caja plástica/metálica embutir', c_cat_5, 1200, 750, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000507', 'Breaker Termomagnético 20A', 'Taco breaker tipo enchufable', c_cat_5, 14000, 9500, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000508', 'Plafón Porcelana', 'Roseta / portabombillo E27', c_cat_5, 3500, 2200, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000509', 'Bombillo LED 9W', 'Linterna plafón base e27 luz fría', c_cat_5, 4500, 2800, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000510', 'Bombillo LED 18W', 'Luz potente LED E27 blanca', c_cat_5, 9000, 6000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000511', 'Tubo Conduit PVC 1/2 3m', 'Tubería eléctrica curva verde', c_cat_5, 4800, 3100, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000512', 'Amarres Plásticos 20cm x100', 'Correas plásticas abrazaderas', c_cat_5, 6000, 3500, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000513', 'Clavija Macho 15A', 'Enchufe macho recambiable 2 polos', c_cat_5, 2500, 1400, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000514', 'Multitoma 6 Puestos Blanca', 'Regleta con suiche 6 posiciones', c_cat_5, 18000, 11500, true, NOW(), 'seed', '94', false, false, 1),

    -- Tornillería y Fijación (10 items)
    (gen_random_uuid(), '7702000601', 'Clavos Acero 2" (1kg)', 'Caja clavos lisos p/concreto', c_cat_6, 8500, 5200, true, NOW(), 'seed', 'KGM', false, false, 1),
    (gen_random_uuid(), '7702000602', 'Clavos Madera 2-1/2" (1kg)', 'Clavos con cabeza puntillas madera', c_cat_6, 6500, 4100, true, NOW(), 'seed', 'KGM', false, false, 1),
    (gen_random_uuid(), '7702000603', 'Tornillo Drywall 1-1/4" x1000', 'Tornillo negro fosfatado (caja)', c_cat_6, 28000, 18500, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000604', 'Tornillo Autoperforante 1" x500', 'Cabeza hexagonal punta broca', c_cat_6, 35000, 24000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000605', 'Chazo Plástico 1/4 x100', 'Tarugo plastico 1/4 paquete x100', c_cat_6, 3500, 1800, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000606', 'Chazo Expansivo 3/8', 'Perno expansivo metálico x unidad', c_cat_6, 1200, 700, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000607', 'Tirafondo Hexagonal 1/4x2"', 'Tornillo madera hex zincado', c_cat_6, 300, 180, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000608', 'Arandela Plana 1/4 x100', 'Arandela zincada lisa (bolsa)', c_cat_6, 4500, 2600, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000609', 'Remache Pop 1/8 x100', 'Remaches aluminio ciego (bolsa)', c_cat_6, 6000, 3800, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000610', 'Tuerca Hexagonal 1/4 x100', 'Tuerca grado 2 zinc caja x100', c_cat_6, 5000, 3100, true, NOW(), 'seed', '94', false, false, 1),

    -- Seguridad Industrial (12 items)
    (gen_random_uuid(), '7702000701', 'Casco Seguridad Amarillo', 'Casco tipo 1 ala frontal', c_cat_7, 12000, 8000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000702', 'Gafas Protección', 'Lentes policarbonato antiimpacto', c_cat_7, 4500, 2800, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000703', 'Guantes Carnaza Cortos', 'Guante carnaza sencillo', c_cat_7, 6500, 4200, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000704', 'Guantes Nitrilo', 'Guante negro recubierto flex', c_cat_7, 5000, 3300, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000705', 'Botas Punta Acero', 'Bota cuero industrial seguridad', c_cat_7, 55000, 38000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000706', 'Botas Caucho Negras', 'Bota pantanera con labrado', c_cat_7, 28000, 19500, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000707', 'Mascarilla Válvula N95', 'Respirador polvos y partículas', c_cat_7, 4500, 2500, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000708', 'Tapones Auditivos Silicona', 'Protector auditivo tipo hongo', c_cat_7, 1800, 1000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000709', 'Faja Lumbar', 'Cinturón ergonómico de fuerza', c_cat_7, 25000, 17000, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000710', 'Chaleco Reflectivo', 'Chaleco malla verde con cinta', c_cat_7, 7500, 4800, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000711', 'Cinta Señalización Peligro 50m', 'Rodillo amarillo/negro prevención', c_cat_7, 8500, 5200, true, NOW(), 'seed', '94', false, false, 1),
    (gen_random_uuid(), '7702000712', 'Cono Naranja 70cm', 'Cono reflectivo de señalización', c_cat_7, 22000, 14500, true, NOW(), 'seed', '94', false, false, 1);

END $$;
