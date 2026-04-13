CREATE TABLE IF NOT EXISTS public.__ef_migrations_history (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___ef_migrations_history" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260224194002_InitialCreate') THEN
    CREATE TABLE public.categorias (
        "Id" integer GENERATED ALWAYS AS IDENTITY,
        nombre character varying(100) NOT NULL,
        descripcion character varying(300),
        activo boolean NOT NULL,
        CONSTRAINT "PK_categorias" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260224194002_InitialCreate') THEN
    CREATE TABLE public.productos (
        id uuid NOT NULL,
        codigo_barras character varying(50) NOT NULL,
        nombre character varying(200) NOT NULL,
        descripcion character varying(500),
        categoria_id integer,
        precio_venta numeric(18,2) NOT NULL,
        precio_costo numeric(18,2) NOT NULL,
        activo boolean NOT NULL,
        fecha_creacion timestamp with time zone NOT NULL,
        fecha_modificacion timestamp with time zone,
        CONSTRAINT "PK_productos" PRIMARY KEY (id),
        CONSTRAINT "FK_productos_categorias_categoria_id" FOREIGN KEY (categoria_id) REFERENCES public.categorias ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260224194002_InitialCreate') THEN
    CREATE INDEX "IX_productos_categoria_id" ON public.productos (categoria_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260224194002_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_productos_codigo_barras ON public.productos (codigo_barras);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260224194002_InitialCreate') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260224194002_InitialCreate', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260224215349_MakeCategoriaIdRequired') THEN

                    INSERT INTO public.categorias OVERRIDING SYSTEM VALUE
                    VALUES (1, 'General', NULL, true)
                    ON CONFLICT ("Id") DO NOTHING;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260224215349_MakeCategoriaIdRequired') THEN

                    PERFORM setval(pg_get_serial_sequence('public.categorias', 'Id'),
                        COALESCE((SELECT MAX("Id") FROM public.categorias), 0) + 1, false);
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260224215349_MakeCategoriaIdRequired') THEN

                    UPDATE public.productos SET categoria_id = 1 WHERE categoria_id IS NULL OR categoria_id = 0;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260224215349_MakeCategoriaIdRequired') THEN
    ALTER TABLE public.productos DROP CONSTRAINT "FK_productos_categorias_categoria_id";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260224215349_MakeCategoriaIdRequired') THEN
    UPDATE public.productos SET categoria_id = 0 WHERE categoria_id IS NULL;
    ALTER TABLE public.productos ALTER COLUMN categoria_id SET NOT NULL;
    ALTER TABLE public.productos ALTER COLUMN categoria_id SET DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260224215349_MakeCategoriaIdRequired') THEN
    ALTER TABLE public.productos ADD CONSTRAINT "FK_productos_categorias_categoria_id" FOREIGN KEY (categoria_id) REFERENCES public.categorias ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260224215349_MakeCategoriaIdRequired') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260224215349_MakeCategoriaIdRequired', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260224231948_AddSucursales') THEN
    CREATE TABLE public.sucursales (
        "Id" integer GENERATED ALWAYS AS IDENTITY,
        nombre character varying(150) NOT NULL,
        direccion character varying(300),
        ciudad character varying(100),
        telefono character varying(20),
        email character varying(150),
        activo boolean NOT NULL,
        fecha_creacion timestamp with time zone NOT NULL,
        CONSTRAINT "PK_sucursales" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260224231948_AddSucursales') THEN
    CREATE UNIQUE INDEX ix_sucursales_nombre ON public.sucursales (nombre);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260224231948_AddSucursales') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260224231948_AddSucursales', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260224232607_AddCajas') THEN
    CREATE TABLE public.cajas (
        "Id" integer GENERATED ALWAYS AS IDENTITY,
        nombre character varying(50) NOT NULL,
        sucursal_id integer NOT NULL,
        estado integer NOT NULL,
        monto_apertura numeric(18,2) NOT NULL,
        monto_actual numeric(18,2) NOT NULL,
        fecha_apertura timestamp with time zone,
        fecha_cierre timestamp with time zone,
        abierta_por_usuario_id integer,
        activo boolean NOT NULL,
        fecha_creacion timestamp with time zone NOT NULL,
        CONSTRAINT "PK_cajas" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_cajas_sucursales_sucursal_id" FOREIGN KEY (sucursal_id) REFERENCES public.sucursales ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260224232607_AddCajas') THEN
    CREATE UNIQUE INDEX ix_cajas_sucursal_nombre ON public.cajas (sucursal_id, nombre);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260224232607_AddCajas') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260224232607_AddCajas', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260224234212_AddMetodoCosteoSucursal') THEN
    ALTER TABLE public.sucursales ADD metodo_costeo integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260224234212_AddMetodoCosteoSucursal') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260224234212_AddMetodoCosteoSucursal', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260224235331_AddTerceros') THEN
    CREATE TABLE public.terceros (
        "Id" integer GENERATED ALWAYS AS IDENTITY,
        tipo_identificacion integer NOT NULL,
        identificacion character varying(50) NOT NULL,
        nombre character varying(250) NOT NULL,
        tipo_tercero integer NOT NULL,
        telefono character varying(20),
        email character varying(150),
        direccion character varying(300),
        ciudad character varying(100),
        origen_datos integer NOT NULL DEFAULT 0,
        external_id character varying(100),
        activo boolean NOT NULL,
        fecha_creacion timestamp with time zone NOT NULL,
        fecha_modificacion timestamp with time zone,
        CONSTRAINT "PK_terceros" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260224235331_AddTerceros') THEN
    CREATE INDEX ix_terceros_external_id ON public.terceros (external_id) WHERE external_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260224235331_AddTerceros') THEN
    CREATE UNIQUE INDEX ix_terceros_identificacion ON public.terceros (identificacion);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260224235331_AddTerceros') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260224235331_AddTerceros', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225000249_AddInventario') THEN
    CREATE TABLE public.movimientos_inventario (
        "Id" integer GENERATED ALWAYS AS IDENTITY,
        producto_id uuid NOT NULL,
        sucursal_id integer NOT NULL,
        tipo_movimiento integer NOT NULL,
        cantidad numeric(18,4) NOT NULL,
        costo_unitario numeric(18,4) NOT NULL,
        costo_total numeric(18,4) NOT NULL,
        referencia character varying(100),
        observaciones character varying(500),
        tercero_id integer,
        sucursal_destino_id integer,
        usuario_id integer NOT NULL,
        fecha_movimiento timestamp with time zone NOT NULL,
        CONSTRAINT "PK_movimientos_inventario" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_movimientos_inventario_productos_producto_id" FOREIGN KEY (producto_id) REFERENCES public.productos (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_movimientos_inventario_sucursales_sucursal_destino_id" FOREIGN KEY (sucursal_destino_id) REFERENCES public.sucursales ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_movimientos_inventario_sucursales_sucursal_id" FOREIGN KEY (sucursal_id) REFERENCES public.sucursales ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_movimientos_inventario_terceros_tercero_id" FOREIGN KEY (tercero_id) REFERENCES public.terceros ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225000249_AddInventario') THEN
    CREATE TABLE public.stock (
        "Id" integer GENERATED ALWAYS AS IDENTITY,
        producto_id uuid NOT NULL,
        sucursal_id integer NOT NULL,
        cantidad numeric(18,4) NOT NULL,
        stock_minimo numeric(18,4) NOT NULL,
        costo_promedio numeric(18,4) NOT NULL,
        ultima_actualizacion timestamp with time zone NOT NULL,
        CONSTRAINT "PK_stock" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_stock_productos_producto_id" FOREIGN KEY (producto_id) REFERENCES public.productos (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_stock_sucursales_sucursal_id" FOREIGN KEY (sucursal_id) REFERENCES public.sucursales ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225000249_AddInventario') THEN
    CREATE INDEX "IX_movimientos_inventario_sucursal_destino_id" ON public.movimientos_inventario (sucursal_destino_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225000249_AddInventario') THEN
    CREATE INDEX "IX_movimientos_inventario_sucursal_id" ON public.movimientos_inventario (sucursal_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225000249_AddInventario') THEN
    CREATE INDEX "IX_movimientos_inventario_tercero_id" ON public.movimientos_inventario (tercero_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225000249_AddInventario') THEN
    CREATE INDEX ix_movimientos_producto_sucursal_fecha ON public.movimientos_inventario (producto_id, sucursal_id, fecha_movimiento);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225000249_AddInventario') THEN
    CREATE UNIQUE INDEX ix_stock_producto_sucursal ON public.stock (producto_id, sucursal_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225000249_AddInventario') THEN
    CREATE INDEX "IX_stock_sucursal_id" ON public.stock (sucursal_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225000249_AddInventario') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260225000249_AddInventario', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225003458_AddLotesInventario') THEN
    CREATE TABLE public.lotes_inventario (
        "Id" integer GENERATED ALWAYS AS IDENTITY,
        producto_id uuid NOT NULL,
        sucursal_id integer NOT NULL,
        cantidad_inicial numeric(18,4) NOT NULL,
        cantidad_disponible numeric(18,4) NOT NULL,
        costo_unitario numeric(18,4) NOT NULL,
        referencia character varying(100),
        tercero_id integer,
        fecha_entrada timestamp with time zone NOT NULL,
        CONSTRAINT "PK_lotes_inventario" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_lotes_inventario_productos_producto_id" FOREIGN KEY (producto_id) REFERENCES public.productos (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_lotes_inventario_sucursales_sucursal_id" FOREIGN KEY (sucursal_id) REFERENCES public.sucursales ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_lotes_inventario_terceros_tercero_id" FOREIGN KEY (tercero_id) REFERENCES public.terceros ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225003458_AddLotesInventario') THEN
    CREATE INDEX "IX_lotes_inventario_sucursal_id" ON public.lotes_inventario (sucursal_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225003458_AddLotesInventario') THEN
    CREATE INDEX "IX_lotes_inventario_tercero_id" ON public.lotes_inventario (tercero_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225003458_AddLotesInventario') THEN
    CREATE INDEX ix_lotes_producto_sucursal_fecha ON public.lotes_inventario (producto_id, sucursal_id, fecha_entrada);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225003458_AddLotesInventario') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260225003458_AddLotesInventario', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225142337_AddVentasYPrecios') THEN
    ALTER TABLE public.categorias ADD margen_ganancia numeric(5,2) NOT NULL DEFAULT 0.3;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225142337_AddVentasYPrecios') THEN
    CREATE TABLE public.precios_sucursal (
        "Id" integer GENERATED ALWAYS AS IDENTITY,
        producto_id uuid NOT NULL,
        sucursal_id integer NOT NULL,
        precio_venta numeric(18,2) NOT NULL,
        precio_minimo numeric(18,2),
        fecha_actualizacion timestamp with time zone NOT NULL,
        CONSTRAINT "PK_precios_sucursal" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_precios_sucursal_productos_producto_id" FOREIGN KEY (producto_id) REFERENCES public.productos (id) ON DELETE CASCADE,
        CONSTRAINT "FK_precios_sucursal_sucursales_sucursal_id" FOREIGN KEY (sucursal_id) REFERENCES public.sucursales ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225142337_AddVentasYPrecios') THEN
    CREATE TABLE public.ventas (
        "Id" integer GENERATED ALWAYS AS IDENTITY,
        numero_venta character varying(20) NOT NULL,
        sucursal_id integer NOT NULL,
        caja_id integer NOT NULL,
        cliente_id integer,
        usuario_id integer,
        subtotal numeric(18,2) NOT NULL,
        descuento numeric(18,2) NOT NULL,
        impuestos numeric(18,2) NOT NULL,
        total numeric(18,2) NOT NULL,
        estado integer NOT NULL,
        metodo_pago integer NOT NULL,
        monto_pagado numeric(18,2),
        cambio numeric(18,2),
        observaciones character varying(500),
        fecha_venta timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ventas" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ventas_cajas_caja_id" FOREIGN KEY (caja_id) REFERENCES public.cajas ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ventas_sucursales_sucursal_id" FOREIGN KEY (sucursal_id) REFERENCES public.sucursales ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ventas_terceros_cliente_id" FOREIGN KEY (cliente_id) REFERENCES public.terceros ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225142337_AddVentasYPrecios') THEN
    CREATE TABLE public.detalle_ventas (
        "Id" integer GENERATED ALWAYS AS IDENTITY,
        venta_id integer NOT NULL,
        producto_id uuid NOT NULL,
        nombre_producto character varying(200) NOT NULL,
        cantidad numeric(18,2) NOT NULL,
        precio_unitario numeric(18,2) NOT NULL,
        costo_unitario numeric(18,2) NOT NULL,
        descuento numeric(18,2) NOT NULL,
        subtotal numeric(18,2) NOT NULL,
        CONSTRAINT "PK_detalle_ventas" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_detalle_ventas_productos_producto_id" FOREIGN KEY (producto_id) REFERENCES public.productos (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_detalle_ventas_ventas_venta_id" FOREIGN KEY (venta_id) REFERENCES public.ventas ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225142337_AddVentasYPrecios') THEN
    CREATE INDEX "IX_detalle_ventas_producto_id" ON public.detalle_ventas (producto_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225142337_AddVentasYPrecios') THEN
    CREATE INDEX "IX_detalle_ventas_venta_id" ON public.detalle_ventas (venta_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225142337_AddVentasYPrecios') THEN
    CREATE UNIQUE INDEX ix_precios_sucursal_producto_sucursal ON public.precios_sucursal (producto_id, sucursal_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225142337_AddVentasYPrecios') THEN
    CREATE INDEX "IX_precios_sucursal_sucursal_id" ON public.precios_sucursal (sucursal_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225142337_AddVentasYPrecios') THEN
    CREATE INDEX "IX_ventas_caja_id" ON public.ventas (caja_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225142337_AddVentasYPrecios') THEN
    CREATE INDEX "IX_ventas_cliente_id" ON public.ventas (cliente_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225142337_AddVentasYPrecios') THEN
    CREATE INDEX ix_ventas_fecha ON public.ventas (fecha_venta);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225142337_AddVentasYPrecios') THEN
    CREATE UNIQUE INDEX ix_ventas_numero ON public.ventas (numero_venta);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225142337_AddVentasYPrecios') THEN
    CREATE INDEX ix_ventas_sucursal_fecha ON public.ventas (sucursal_id, fecha_venta);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225142337_AddVentasYPrecios') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260225142337_AddVentasYPrecios', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225165257_AgregaCategoriaIdAProducto') THEN
    ALTER TABLE public.productos ADD impuesto_id integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225165257_AgregaCategoriaIdAProducto') THEN
    ALTER TABLE public.movimientos_inventario ADD monto_impuesto numeric(18,4) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225165257_AgregaCategoriaIdAProducto') THEN
    ALTER TABLE public.movimientos_inventario ADD porcentaje_impuesto numeric(5,4) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225165257_AgregaCategoriaIdAProducto') THEN
    ALTER TABLE public.lotes_inventario ADD monto_impuesto_unitario numeric(18,4) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225165257_AgregaCategoriaIdAProducto') THEN
    ALTER TABLE public.lotes_inventario ADD porcentaje_impuesto numeric(5,4) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225165257_AgregaCategoriaIdAProducto') THEN
    ALTER TABLE public.detalle_ventas ADD monto_impuesto numeric(18,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225165257_AgregaCategoriaIdAProducto') THEN
    ALTER TABLE public.detalle_ventas ADD porcentaje_impuesto numeric(5,4) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225165257_AgregaCategoriaIdAProducto') THEN
    CREATE TABLE public.impuestos (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "Nombre" character varying(50) NOT NULL,
        "Porcentaje" numeric(5,4) NOT NULL,
        "Activo" boolean NOT NULL DEFAULT TRUE,
        "FechaCreacion" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_impuestos" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225165257_AgregaCategoriaIdAProducto') THEN
    INSERT INTO public.impuestos ("Id", "Activo", "FechaCreacion", "Nombre", "Porcentaje")
    VALUES (1, TRUE, TIMESTAMPTZ '2026-01-01T00:00:00Z', 'Exento 0%', 0.0);
    INSERT INTO public.impuestos ("Id", "Activo", "FechaCreacion", "Nombre", "Porcentaje")
    VALUES (2, TRUE, TIMESTAMPTZ '2026-01-01T00:00:00Z', 'IVA 5%', 0.05);
    INSERT INTO public.impuestos ("Id", "Activo", "FechaCreacion", "Nombre", "Porcentaje")
    VALUES (3, TRUE, TIMESTAMPTZ '2026-01-01T00:00:00Z', 'IVA 19%', 0.19);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225165257_AgregaCategoriaIdAProducto') THEN
    CREATE INDEX "IX_productos_impuesto_id" ON public.productos (impuesto_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225165257_AgregaCategoriaIdAProducto') THEN
    ALTER TABLE public.productos ADD CONSTRAINT "FK_productos_impuestos_impuesto_id" FOREIGN KEY (impuesto_id) REFERENCES public.impuestos ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225165257_AgregaCategoriaIdAProducto') THEN
    PERFORM setval(
        pg_get_serial_sequence('public.impuestos', 'Id'),
        GREATEST(
            (SELECT MAX("Id") FROM public.impuestos) + 1,
            nextval(pg_get_serial_sequence('public.impuestos', 'Id'))),
        false);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225165257_AgregaCategoriaIdAProducto') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260225165257_AgregaCategoriaIdAProducto', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225171446_AgregarUsuarios') THEN
    CREATE TABLE public.usuarios (
        id integer GENERATED BY DEFAULT AS IDENTITY,
        keycloak_id character varying(100) NOT NULL,
        email character varying(255) NOT NULL,
        nombre_completo character varying(255) NOT NULL,
        telefono character varying(50),
        rol character varying(50) NOT NULL,
        sucursal_default_id integer,
        activo boolean NOT NULL DEFAULT TRUE,
        fecha_creacion timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        fecha_modificacion timestamp with time zone,
        ultimo_acceso timestamp with time zone,
        CONSTRAINT "PK_usuarios" PRIMARY KEY (id),
        CONSTRAINT "FK_usuarios_sucursales_sucursal_default_id" FOREIGN KEY (sucursal_default_id) REFERENCES public.sucursales ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225171446_AgregarUsuarios') THEN
    CREATE INDEX ix_usuarios_email ON public.usuarios (email);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225171446_AgregarUsuarios') THEN
    CREATE UNIQUE INDEX ix_usuarios_keycloak_id ON public.usuarios (keycloak_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225171446_AgregarUsuarios') THEN
    CREATE INDEX "IX_usuarios_sucursal_default_id" ON public.usuarios (sucursal_default_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260225171446_AgregarUsuarios') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260225171446_AgregarUsuarios', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.precios_sucursal RENAME COLUMN fecha_actualizacion TO "FechaCreacion";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.ventas ADD "Activo" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.ventas ADD "CreadoPor" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.ventas ADD "FechaCreacion" timestamp with time zone NOT NULL DEFAULT TIMESTAMPTZ '-infinity';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.ventas ADD "FechaModificacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.ventas ADD "ModificadoPor" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.usuarios ADD "CreadoPor" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.usuarios ADD "ModificadoPor" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.terceros ADD "CreadoPor" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.terceros ADD "ModificadoPor" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.sucursales ADD "CreadoPor" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.sucursales ADD "FechaModificacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.sucursales ADD "ModificadoPor" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.stock ADD "Activo" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.stock ADD "CreadoPor" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.stock ADD "FechaCreacion" timestamp with time zone NOT NULL DEFAULT TIMESTAMPTZ '-infinity';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.stock ADD "FechaModificacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.stock ADD "ModificadoPor" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.productos ADD "CreadoPor" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.productos ADD "ModificadoPor" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.precios_sucursal ADD "Activo" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.precios_sucursal ADD "CreadoPor" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.precios_sucursal ADD "FechaModificacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.precios_sucursal ADD "ModificadoPor" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.lotes_inventario ADD "Activo" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.lotes_inventario ADD "CreadoPor" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.lotes_inventario ADD "FechaCreacion" timestamp with time zone NOT NULL DEFAULT TIMESTAMPTZ '-infinity';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.lotes_inventario ADD "FechaModificacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.lotes_inventario ADD "ModificadoPor" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.impuestos ADD "CreadoPor" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.impuestos ADD "FechaModificacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.impuestos ADD "ModificadoPor" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.categorias ADD "CreadoPor" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.categorias ADD "FechaCreacion" timestamp with time zone NOT NULL DEFAULT TIMESTAMPTZ '-infinity';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.categorias ADD "FechaModificacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.categorias ADD "ModificadoPor" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.cajas ADD "CreadoPor" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.cajas ADD "FechaModificacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    ALTER TABLE public.cajas ADD "ModificadoPor" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    UPDATE public.impuestos SET "CreadoPor" = '', "FechaModificacion" = NULL, "ModificadoPor" = NULL
    WHERE "Id" = 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    UPDATE public.impuestos SET "CreadoPor" = '', "FechaModificacion" = NULL, "ModificadoPor" = NULL
    WHERE "Id" = 2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    UPDATE public.impuestos SET "CreadoPor" = '', "FechaModificacion" = NULL, "ModificadoPor" = NULL
    WHERE "Id" = 3;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226201111_AgregarAuditoria') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260226201111_AgregarAuditoria', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226211551_AgregarActivityLogs') THEN
    CREATE TABLE public.activity_logs (
        id bigint GENERATED BY DEFAULT AS IDENTITY,
        usuario_email character varying(255) NOT NULL,
        usuario_id integer,
        fecha_hora timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        accion character varying(100) NOT NULL,
        tipo integer NOT NULL,
        sucursal_id integer,
        ip_address character varying(50),
        user_agent character varying(500),
        tipo_entidad character varying(100),
        entidad_id character varying(50),
        entidad_nombre character varying(255),
        descripcion character varying(1000),
        datos_anteriores jsonb,
        datos_nuevos jsonb,
        metadatos jsonb,
        exitosa boolean NOT NULL DEFAULT TRUE,
        mensaje_error character varying(1000),
        CONSTRAINT "PK_activity_logs" PRIMARY KEY (id),
        CONSTRAINT "FK_activity_logs_sucursales_sucursal_id" FOREIGN KEY (sucursal_id) REFERENCES public.sucursales ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_activity_logs_usuarios_usuario_id" FOREIGN KEY (usuario_id) REFERENCES public.usuarios (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226211551_AgregarActivityLogs') THEN
    CREATE INDEX idx_activity_dashboard ON public.activity_logs (fecha_hora, tipo, sucursal_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226211551_AgregarActivityLogs') THEN
    CREATE INDEX idx_activity_entidad ON public.activity_logs (tipo_entidad, entidad_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226211551_AgregarActivityLogs') THEN
    CREATE INDEX idx_activity_fecha ON public.activity_logs (fecha_hora);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226211551_AgregarActivityLogs') THEN
    CREATE INDEX idx_activity_tipo ON public.activity_logs (tipo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226211551_AgregarActivityLogs') THEN
    CREATE INDEX idx_activity_usuario ON public.activity_logs (usuario_email);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226211551_AgregarActivityLogs') THEN
    CREATE INDEX "IX_activity_logs_sucursal_id" ON public.activity_logs (sucursal_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226211551_AgregarActivityLogs') THEN
    CREATE INDEX "IX_activity_logs_usuario_id" ON public.activity_logs (usuario_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260226211551_AgregarActivityLogs') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260226211551_AgregarActivityLogs', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302015204_AgregarDevolucionesParciales') THEN
    CREATE TABLE public.devoluciones_venta (
        "Id" integer GENERATED ALWAYS AS IDENTITY,
        venta_id integer NOT NULL,
        numero_devolucion character varying(20) NOT NULL,
        motivo character varying(500) NOT NULL,
        total_devuelto numeric(18,2) NOT NULL,
        fecha_devolucion timestamp with time zone NOT NULL,
        autorizado_por_usuario_id integer,
        "CreadoPor" text NOT NULL,
        "FechaCreacion" timestamp with time zone NOT NULL,
        "ModificadoPor" text,
        "FechaModificacion" timestamp with time zone,
        "Activo" boolean NOT NULL,
        CONSTRAINT "PK_devoluciones_venta" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_devoluciones_venta_ventas_venta_id" FOREIGN KEY (venta_id) REFERENCES public.ventas ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302015204_AgregarDevolucionesParciales') THEN
    CREATE TABLE public.detalle_devolucion (
        "Id" integer GENERATED ALWAYS AS IDENTITY,
        devolucion_venta_id integer NOT NULL,
        producto_id uuid NOT NULL,
        nombre_producto character varying(200) NOT NULL,
        cantidad_devuelta numeric(18,3) NOT NULL,
        precio_unitario numeric(18,2) NOT NULL,
        costo_unitario numeric(18,2) NOT NULL,
        subtotal_devuelto numeric(18,2) NOT NULL,
        CONSTRAINT "PK_detalle_devolucion" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_detalle_devolucion_devoluciones_venta_devolucion_venta_id" FOREIGN KEY (devolucion_venta_id) REFERENCES public.devoluciones_venta ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_detalle_devolucion_productos_producto_id" FOREIGN KEY (producto_id) REFERENCES public.productos (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302015204_AgregarDevolucionesParciales') THEN
    CREATE INDEX "IX_detalle_devolucion_devolucion_venta_id" ON public.detalle_devolucion (devolucion_venta_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302015204_AgregarDevolucionesParciales') THEN
    CREATE INDEX "IX_detalle_devolucion_producto_id" ON public.detalle_devolucion (producto_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302015204_AgregarDevolucionesParciales') THEN
    CREATE INDEX ix_devoluciones_fecha ON public.devoluciones_venta (fecha_devolucion);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302015204_AgregarDevolucionesParciales') THEN
    CREATE UNIQUE INDEX ix_devoluciones_numero ON public.devoluciones_venta (numero_devolucion);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302015204_AgregarDevolucionesParciales') THEN
    CREATE INDEX ix_devoluciones_venta_fecha ON public.devoluciones_venta (venta_id, fecha_devolucion);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302015204_AgregarDevolucionesParciales') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260302015204_AgregarDevolucionesParciales', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302024257_AgregarTraslados') THEN
    CREATE TABLE public.traslados (
        "Id" integer GENERATED ALWAYS AS IDENTITY,
        numero_traslado character varying(20) NOT NULL,
        sucursal_origen_id integer NOT NULL,
        sucursal_destino_id integer NOT NULL,
        estado integer NOT NULL,
        fecha_traslado timestamp with time zone NOT NULL,
        fecha_envio timestamp with time zone,
        fecha_recepcion timestamp with time zone,
        recibido_por_usuario_id integer,
        observaciones character varying(500),
        motivo_rechazo character varying(500),
        "CreadoPor" text NOT NULL,
        "FechaCreacion" timestamp with time zone NOT NULL,
        "ModificadoPor" text,
        "FechaModificacion" timestamp with time zone,
        "Activo" boolean NOT NULL,
        CONSTRAINT "PK_traslados" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_traslados_sucursales_sucursal_destino_id" FOREIGN KEY (sucursal_destino_id) REFERENCES public.sucursales ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_traslados_sucursales_sucursal_origen_id" FOREIGN KEY (sucursal_origen_id) REFERENCES public.sucursales ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302024257_AgregarTraslados') THEN
    CREATE TABLE public.detalle_traslados (
        "Id" integer GENERATED ALWAYS AS IDENTITY,
        traslado_id integer NOT NULL,
        producto_id uuid NOT NULL,
        nombre_producto character varying(200) NOT NULL,
        cantidad_solicitada numeric(18,4) NOT NULL,
        cantidad_recibida numeric(18,4) NOT NULL,
        costo_unitario numeric(18,4) NOT NULL,
        costo_total numeric(18,4) NOT NULL,
        observaciones character varying(300),
        CONSTRAINT "PK_detalle_traslados" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_detalle_traslados_productos_producto_id" FOREIGN KEY (producto_id) REFERENCES public.productos (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_detalle_traslados_traslados_traslado_id" FOREIGN KEY (traslado_id) REFERENCES public.traslados ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302024257_AgregarTraslados') THEN
    CREATE INDEX "IX_detalle_traslados_producto_id" ON public.detalle_traslados (producto_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302024257_AgregarTraslados') THEN
    CREATE INDEX "IX_detalle_traslados_traslado_id" ON public.detalle_traslados (traslado_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302024257_AgregarTraslados') THEN
    CREATE INDEX ix_traslados_destino_estado ON public.traslados (sucursal_destino_id, estado);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302024257_AgregarTraslados') THEN
    CREATE INDEX ix_traslados_fecha ON public.traslados (fecha_traslado);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302024257_AgregarTraslados') THEN
    CREATE UNIQUE INDEX ix_traslados_numero ON public.traslados (numero_traslado);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302024257_AgregarTraslados') THEN
    CREATE INDEX ix_traslados_origen_fecha ON public.traslados (sucursal_origen_id, fecha_traslado);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302024257_AgregarTraslados') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260302024257_AgregarTraslados', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302141123_AgregarOrdenesCompra') THEN
    CREATE TABLE public.ordenes_compra (
        "Id" integer GENERATED ALWAYS AS IDENTITY,
        numero_orden character varying(20) NOT NULL,
        sucursal_id integer NOT NULL,
        proveedor_id integer NOT NULL,
        estado integer NOT NULL,
        fecha_orden timestamp with time zone NOT NULL,
        fecha_entrega_esperada timestamp with time zone,
        fecha_aprobacion timestamp with time zone,
        fecha_recepcion timestamp with time zone,
        aprobado_por_usuario_id integer,
        recibido_por_usuario_id integer,
        observaciones character varying(500),
        motivo_rechazo character varying(500),
        subtotal numeric(18,2) NOT NULL,
        impuestos numeric(18,2) NOT NULL,
        total numeric(18,2) NOT NULL,
        "CreadoPor" text NOT NULL,
        "FechaCreacion" timestamp with time zone NOT NULL,
        "ModificadoPor" text,
        "FechaModificacion" timestamp with time zone,
        "Activo" boolean NOT NULL,
        CONSTRAINT "PK_ordenes_compra" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ordenes_compra_sucursales_sucursal_id" FOREIGN KEY (sucursal_id) REFERENCES public.sucursales ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ordenes_compra_terceros_proveedor_id" FOREIGN KEY (proveedor_id) REFERENCES public.terceros ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302141123_AgregarOrdenesCompra') THEN
    CREATE TABLE public.detalle_ordenes_compra (
        "Id" integer GENERATED ALWAYS AS IDENTITY,
        orden_compra_id integer NOT NULL,
        producto_id uuid NOT NULL,
        nombre_producto character varying(200) NOT NULL,
        cantidad_solicitada numeric(18,4) NOT NULL,
        cantidad_recibida numeric(18,4) NOT NULL,
        precio_unitario numeric(18,4) NOT NULL,
        porcentaje_impuesto numeric(5,4) NOT NULL,
        monto_impuesto numeric(18,2) NOT NULL,
        subtotal numeric(18,2) NOT NULL,
        observaciones character varying(300),
        CONSTRAINT "PK_detalle_ordenes_compra" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_detalle_ordenes_compra_ordenes_compra_orden_compra_id" FOREIGN KEY (orden_compra_id) REFERENCES public.ordenes_compra ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_detalle_ordenes_compra_productos_producto_id" FOREIGN KEY (producto_id) REFERENCES public.productos (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302141123_AgregarOrdenesCompra') THEN
    CREATE INDEX "IX_detalle_ordenes_compra_orden_compra_id" ON public.detalle_ordenes_compra (orden_compra_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302141123_AgregarOrdenesCompra') THEN
    CREATE INDEX "IX_detalle_ordenes_compra_producto_id" ON public.detalle_ordenes_compra (producto_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302141123_AgregarOrdenesCompra') THEN
    CREATE INDEX ix_ordenes_compra_fecha ON public.ordenes_compra (fecha_orden);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302141123_AgregarOrdenesCompra') THEN
    CREATE UNIQUE INDEX ix_ordenes_compra_numero ON public.ordenes_compra (numero_orden);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302141123_AgregarOrdenesCompra') THEN
    CREATE INDEX ix_ordenes_compra_proveedor_fecha ON public.ordenes_compra (proveedor_id, fecha_orden);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302141123_AgregarOrdenesCompra') THEN
    CREATE INDEX ix_ordenes_compra_sucursal_estado ON public.ordenes_compra (sucursal_id, estado);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302141123_AgregarOrdenesCompra') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260302141123_AgregarOrdenesCompra', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302175517_AgregarPaisASucursal') THEN
    ALTER TABLE public.sucursales ADD "CodigoPais" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302175517_AgregarPaisASucursal') THEN
    ALTER TABLE public.sucursales ADD "NombrePais" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302175517_AgregarPaisASucursal') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260302175517_AgregarPaisASucursal', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302210242_AgregarOrigenDatoAPrecioSucursal') THEN
    ALTER TABLE public.precios_sucursal ADD "OrigenDato" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302210242_AgregarOrigenDatoAPrecioSucursal') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260302210242_AgregarOrigenDatoAPrecioSucursal', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302211137_AgregarTablaMigracionesLog') THEN
    CREATE TABLE public.migraciones_log (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        migracion_id character varying(150) NOT NULL,
        descripcion character varying(500) NOT NULL,
        product_version character varying(32) NOT NULL,
        fecha_aplicacion timestamp with time zone NOT NULL,
        aplicado_por character varying(255) NOT NULL,
        estado character varying(50) NOT NULL,
        duracion_ms bigint NOT NULL,
        notas text,
        sql_ejecutado text,
        CONSTRAINT "PK_migraciones_log" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302211137_AgregarTablaMigracionesLog') THEN
    CREATE INDEX "IX_migraciones_log_fecha_aplicacion" ON public.migraciones_log (fecha_aplicacion);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302211137_AgregarTablaMigracionesLog') THEN
    CREATE INDEX "IX_migraciones_log_migracion_id" ON public.migraciones_log (migracion_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302211137_AgregarTablaMigracionesLog') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260302211137_AgregarTablaMigracionesLog', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302234824_CategoriasJerarquicas') THEN
    DROP TABLE IF EXISTS public.detalle_agrupaciones CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302234824_CategoriasJerarquicas') THEN
    DROP TABLE IF EXISTS public.agrupaciones CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302234824_CategoriasJerarquicas') THEN
    ALTER TABLE public.categorias ADD categoria_padre_id integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302234824_CategoriasJerarquicas') THEN
    ALTER TABLE public.categorias ADD nivel integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302234824_CategoriasJerarquicas') THEN
    ALTER TABLE public.categorias ADD ruta_completa character varying(500) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302234824_CategoriasJerarquicas') THEN
    CREATE INDEX ix_categorias_categoria_padre_id ON public.categorias (categoria_padre_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302234824_CategoriasJerarquicas') THEN
    ALTER TABLE public.categorias ADD CONSTRAINT "FK_categorias_categorias_categoria_padre_id" FOREIGN KEY (categoria_padre_id) REFERENCES public.categorias ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260302234824_CategoriasJerarquicas') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260302234824_CategoriasJerarquicas', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN
    ALTER TABLE public.ventas ADD "RequiereFacturaElectronica" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN
    ALTER TABLE public.terceros ADD "PerfilTributario" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN
    ALTER TABLE public.sucursales ADD "CodigoMunicipio" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN
    ALTER TABLE public.sucursales ADD "PerfilTributario" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN
    ALTER TABLE public.sucursales ADD "ValorUVT" numeric NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN
    ALTER TABLE public.productos ADD "EsAlimentoUltraprocesado" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN
    ALTER TABLE public.productos ADD "GramosAzucarPor100ml" numeric;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN
    ALTER TABLE public.impuestos ALTER COLUMN "Porcentaje" TYPE numeric(8,4);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN
    ALTER TABLE public.impuestos ALTER COLUMN "Nombre" TYPE character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN
    ALTER TABLE public.impuestos ADD "AplicaSobreBase" boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN
    ALTER TABLE public.impuestos ADD "CodigoCuentaContable" character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN
    ALTER TABLE public.impuestos ADD "CodigoPais" character varying(2) NOT NULL DEFAULT 'CO';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN
    ALTER TABLE public.impuestos ADD "Descripcion" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN
    ALTER TABLE public.impuestos ADD "Tipo" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN
    ALTER TABLE public.impuestos ADD "ValorFijo" numeric(18,2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN
    CREATE TABLE public.retenciones_reglas (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "Nombre" character varying(100) NOT NULL,
        "Tipo" integer NOT NULL,
        "Porcentaje" numeric(8,6) NOT NULL,
        "BaseMinUVT" numeric(8,2) NOT NULL DEFAULT 4.0,
        "CodigoMunicipio" character varying(10),
        "PerfilVendedor" character varying(50) NOT NULL,
        "PerfilComprador" character varying(50) NOT NULL,
        "CodigoCuentaContable" character varying(20),
        "CreadoPor" text NOT NULL,
        "FechaCreacion" timestamp with time zone NOT NULL,
        "ModificadoPor" text,
        "FechaModificacion" timestamp with time zone,
        "Activo" boolean NOT NULL DEFAULT TRUE,
        CONSTRAINT "PK_retenciones_reglas" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN
    UPDATE public.impuestos SET "AplicaSobreBase" = TRUE, "CodigoCuentaContable" = '2408', "CodigoPais" = 'CO', "Descripcion" = 'Bienes y servicios exentos de IVA', "ValorFijo" = NULL
    WHERE "Id" = 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN
    UPDATE public.impuestos SET "AplicaSobreBase" = TRUE, "CodigoCuentaContable" = '2408', "CodigoPais" = 'CO', "Descripcion" = 'IVA tarifa diferencial 5%', "ValorFijo" = NULL
    WHERE "Id" = 2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN
    UPDATE public.impuestos SET "AplicaSobreBase" = TRUE, "CodigoCuentaContable" = '2408', "CodigoPais" = 'CO', "Descripcion" = 'IVA tarifa general 19%', "ValorFijo" = NULL
    WHERE "Id" = 3;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN

                    INSERT INTO public.impuestos ("Id", "Activo", "CodigoCuentaContable", "CodigoPais", "CreadoPor", "Descripcion", "FechaCreacion", "FechaModificacion", "ModificadoPor", "Nombre", "Porcentaje", "Tipo", "ValorFijo", "AplicaSobreBase")
                    VALUES
                        (4, true, '2412', 'CO', '', 'INC restaurantes, bares, cafeterías (Art. 512-1 ET)', '2026-01-01T00:00:00Z', null, null, 'INC 8%', 0.08, 1, null, false),
                        (5, true, '2424', 'CO', '', 'Impuesto bolsas plásticas 2026 (Ley 1819/2016)', '2026-01-01T00:00:00Z', null, null, 'Bolsa $66', 0.00, 3, 66, false)
                    ON CONFLICT ("Id") DO UPDATE SET
                        "Tipo" = EXCLUDED."Tipo",
                        "CodigoCuentaContable" = EXCLUDED."CodigoCuentaContable",
                        "CodigoPais" = EXCLUDED."CodigoPais",
                        "Descripcion" = EXCLUDED."Descripcion",
                        "AplicaSobreBase" = EXCLUDED."AplicaSobreBase";
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN
    INSERT INTO public.retenciones_reglas ("Id", "Activo", "BaseMinUVT", "CodigoCuentaContable", "CodigoMunicipio", "CreadoPor", "FechaCreacion", "FechaModificacion", "ModificadoPor", "Nombre", "PerfilComprador", "PerfilVendedor", "Porcentaje", "Tipo")
    VALUES (1, TRUE, 4.0, '1355', NULL, '', TIMESTAMPTZ '2026-01-01T00:00:00Z', NULL, NULL, 'ReteFuente Compras 2.5%', 'GRAN_CONTRIBUYENTE', 'REGIMEN_ORDINARIO', 0.025, 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN
    INSERT INTO public.retenciones_reglas ("Id", "CodigoCuentaContable", "CodigoMunicipio", "CreadoPor", "FechaCreacion", "FechaModificacion", "ModificadoPor", "Nombre", "PerfilComprador", "PerfilVendedor", "Porcentaje", "Tipo")
    VALUES (2, '1355', NULL, '', TIMESTAMPTZ '2026-01-01T00:00:00Z', NULL, NULL, 'ReteFuente Honorarios 11%', 'GRAN_CONTRIBUYENTE', 'REGIMEN_ORDINARIO', 0.11, 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN
    INSERT INTO public.retenciones_reglas ("Id", "Activo", "CodigoCuentaContable", "CodigoMunicipio", "CreadoPor", "FechaCreacion", "FechaModificacion", "ModificadoPor", "Nombre", "PerfilComprador", "PerfilVendedor", "Porcentaje", "Tipo")
    VALUES (3, TRUE, '1356', '11001', '', TIMESTAMPTZ '2026-01-01T00:00:00Z', NULL, NULL, 'ReteICA Bogotá 0.966%', 'GRAN_CONTRIBUYENTE', 'REGIMEN_ORDINARIO', 0.00966, 1);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN
    PERFORM setval(
        pg_get_serial_sequence('public.retenciones_reglas', 'Id'),
        GREATEST(
            (SELECT MAX("Id") FROM public.retenciones_reglas) + 1,
            nextval(pg_get_serial_sequence('public.retenciones_reglas', 'Id'))),
        false);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304160602_AgregarTaxEngine') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260304160602_AgregarTaxEngine', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304233852_ImpuestosEnCompras') THEN
    ALTER TABLE public.ordenes_compra ADD requiere_factura_electronica boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304233852_ImpuestosEnCompras') THEN
    ALTER TABLE public.detalle_ordenes_compra ADD nombre_impuesto character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260304233852_ImpuestosEnCompras') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260304233852_ImpuestosEnCompras', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305002952_AgregarCamposFiscalesTerceros') THEN
    ALTER TABLE public.terceros RENAME COLUMN "PerfilTributario" TO perfil_tributario;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305002952_AgregarCamposFiscalesTerceros') THEN
    ALTER TABLE public.terceros ALTER COLUMN perfil_tributario TYPE character varying(50);
    ALTER TABLE public.terceros ALTER COLUMN perfil_tributario SET DEFAULT 'REGIMEN_COMUN';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305002952_AgregarCamposFiscalesTerceros') THEN
    ALTER TABLE public.terceros ADD codigo_departamento character varying(10);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305002952_AgregarCamposFiscalesTerceros') THEN
    ALTER TABLE public.terceros ADD codigo_municipio character varying(10);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305002952_AgregarCamposFiscalesTerceros') THEN
    ALTER TABLE public.terceros ADD digito_verificacion character varying(1);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305002952_AgregarCamposFiscalesTerceros') THEN
    ALTER TABLE public.terceros ADD es_autorretenedor boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305002952_AgregarCamposFiscalesTerceros') THEN
    ALTER TABLE public.terceros ADD es_gran_contribuyente boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305002952_AgregarCamposFiscalesTerceros') THEN
    ALTER TABLE public.terceros ADD es_responsable_iva boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305002952_AgregarCamposFiscalesTerceros') THEN
    CREATE TABLE public.tercero_actividades (
        "Id" integer GENERATED ALWAYS AS IDENTITY,
        tercero_id integer NOT NULL,
        codigo_ciiu character varying(10) NOT NULL,
        descripcion character varying(200) NOT NULL,
        es_principal boolean NOT NULL DEFAULT FALSE,
        CONSTRAINT "PK_tercero_actividades" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_tercero_actividades_terceros_tercero_id" FOREIGN KEY (tercero_id) REFERENCES public.terceros ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305002952_AgregarCamposFiscalesTerceros') THEN
    CREATE UNIQUE INDEX ix_tercero_actividades_tercero_ciiu ON public.tercero_actividades (tercero_id, codigo_ciiu);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305002952_AgregarCamposFiscalesTerceros') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260305002952_AgregarCamposFiscalesTerceros', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305134426_AgregarIndicesRendimiento') THEN
    DROP INDEX public."IX_ventas_cliente_id";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305134426_AgregarIndicesRendimiento') THEN
    ALTER INDEX public."IX_productos_categoria_id" RENAME TO ix_productos_categoria_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305134426_AgregarIndicesRendimiento') THEN
    ALTER INDEX public."IX_precios_sucursal_sucursal_id" RENAME TO ix_precios_sucursal_sucursal_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305134426_AgregarIndicesRendimiento') THEN
    ALTER INDEX public."IX_detalle_ventas_venta_id" RENAME TO ix_detalle_ventas_venta_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305134426_AgregarIndicesRendimiento') THEN
    ALTER INDEX public."IX_detalle_ventas_producto_id" RENAME TO ix_detalle_ventas_producto_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305134426_AgregarIndicesRendimiento') THEN
    CREATE INDEX ix_ventas_cliente_id ON public.ventas (cliente_id) WHERE cliente_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305134426_AgregarIndicesRendimiento') THEN
    CREATE INDEX ix_ventas_estado ON public.ventas (estado);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305134426_AgregarIndicesRendimiento') THEN
    CREATE INDEX ix_traslados_origen_estado ON public.traslados (sucursal_origen_id, estado);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305134426_AgregarIndicesRendimiento') THEN
    CREATE INDEX ix_terceros_activo ON public.terceros (activo) WHERE activo = true;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305134426_AgregarIndicesRendimiento') THEN
    CREATE INDEX ix_terceros_tipo_tercero ON public.terceros (tipo_tercero);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305134426_AgregarIndicesRendimiento') THEN
    CREATE INDEX ix_productos_activo ON public.productos (activo) WHERE activo = true;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305134426_AgregarIndicesRendimiento') THEN
    CREATE INDEX ix_lotes_disponibles ON public.lotes_inventario (producto_id, sucursal_id, cantidad_disponible) WHERE cantidad_disponible > 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305134426_AgregarIndicesRendimiento') THEN
    CREATE INDEX ix_cajas_sucursal_estado ON public.cajas (sucursal_id, estado);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305134426_AgregarIndicesRendimiento') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260305134426_AgregarIndicesRendimiento', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305200000_AgregarUnidadMedidaProducto') THEN
    ALTER TABLE public.productos ADD COLUMN unidad_medida character varying(10) NOT NULL DEFAULT '94';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260305200000_AgregarUnidadMedidaProducto') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260305200000_AgregarUnidadMedidaProducto', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260306000000_AgregarMultiSucursalUsuario') THEN
    CREATE TABLE public.usuario_sucursales (
        usuario_id integer NOT NULL,
        sucursal_id integer NOT NULL,
        CONSTRAINT pk_usuario_sucursales PRIMARY KEY (usuario_id, sucursal_id),
        CONSTRAINT fk_usuario_sucursales_sucursal FOREIGN KEY (sucursal_id) REFERENCES public.sucursales ("Id") ON DELETE CASCADE,
        CONSTRAINT fk_usuario_sucursales_usuario FOREIGN KEY (usuario_id) REFERENCES public.usuarios (id) ON DELETE CASCADE
    );
    CREATE INDEX ix_usuario_sucursales_sucursal_id ON public.usuario_sucursales (sucursal_id);
    INSERT INTO public.usuario_sucursales (usuario_id, sucursal_id)
        SELECT id, sucursal_default_id FROM public.usuarios WHERE sucursal_default_id IS NOT NULL ON CONFLICT DO NOTHING;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260306000000_AgregarMultiSucursalUsuario') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260306000000_AgregarMultiSucursalUsuario', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260306141746_FixUsuarioSucursalesNavigation') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260306141746_FixUsuarioSucursalesNavigation', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260307000000_AgregarFacturacionElectronica') THEN
    CREATE TABLE public.configuracion_emisor (
        id integer GENERATED ALWAYS AS IDENTITY,
        sucursal_id integer NOT NULL,
        nit character varying(20) NOT NULL,
        digito_verificacion character varying(1) NOT NULL,
        razon_social character varying(250) NOT NULL,
        nombre_comercial character varying(250) NOT NULL,
        direccion character varying(500) NOT NULL,
        codigo_municipio character varying(10) NOT NULL,
        codigo_departamento character varying(10) NOT NULL,
        telefono character varying(20) NOT NULL,
        email character varying(100) NOT NULL,
        codigo_ciiu character varying(10) NOT NULL,
        perfil_tributario character varying(50) NOT NULL DEFAULT 'REGIMEN_ORDINARIO',
        numero_resolucion character varying(50) NOT NULL,
        fecha_resolucion timestamp with time zone NOT NULL,
        prefijo character varying(5) NOT NULL DEFAULT 'FV',
        numero_desde bigint NOT NULL,
        numero_hasta bigint NOT NULL,
        numero_actual bigint NOT NULL DEFAULT 0,
        fecha_vigencia_desde timestamp with time zone NOT NULL,
        fecha_vigencia_hasta timestamp with time zone NOT NULL,
        ambiente character varying(1) NOT NULL DEFAULT '2',
        pin_software character varying(100) NOT NULL,
        id_software character varying(100) NOT NULL,
        certificado_base64 text NOT NULL,
        certificado_password character varying(200) NOT NULL,
        CONSTRAINT pk_configuracion_emisor PRIMARY KEY (id),
        CONSTRAINT fk_configuracion_emisor_sucursal FOREIGN KEY (sucursal_id) REFERENCES public.sucursales ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260307000000_AgregarFacturacionElectronica') THEN
    CREATE UNIQUE INDEX ix_configuracion_emisor_sucursal_id ON public.configuracion_emisor (sucursal_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260307000000_AgregarFacturacionElectronica') THEN
    CREATE TABLE public.documentos_electronicos (
        id integer GENERATED ALWAYS AS IDENTITY,
        venta_id integer,
        sucursal_id integer NOT NULL,
        tipo_documento character varying(2) NOT NULL,
        prefijo character varying(5) NOT NULL,
        numero bigint NOT NULL,
        numero_completo character varying(20) NOT NULL,
        cufe character varying(200) NOT NULL,
        fecha_emision timestamp with time zone NOT NULL,
        xml_ubl text NOT NULL,
        estado integer NOT NULL DEFAULT 0,
        fecha_envio_dian timestamp with time zone,
        codigo_respuesta_dian character varying(20),
        mensaje_respuesta_dian character varying(1000),
        intentos integer NOT NULL DEFAULT 0,
        creado_por character varying(200) NOT NULL DEFAULT 'sistema',
        fecha_creacion timestamp with time zone NOT NULL,
        modificado_por character varying(200),
        fecha_modificacion timestamp with time zone,
        CONSTRAINT pk_documentos_electronicos PRIMARY KEY (id),
        CONSTRAINT fk_documentos_electronicos_venta FOREIGN KEY (venta_id) REFERENCES public.ventas ("Id") ON DELETE RESTRICT,
        CONSTRAINT fk_documentos_electronicos_sucursal FOREIGN KEY (sucursal_id) REFERENCES public.sucursales ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260307000000_AgregarFacturacionElectronica') THEN
    CREATE UNIQUE INDEX ix_documentos_electronicos_cufe ON public.documentos_electronicos (cufe);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260307000000_AgregarFacturacionElectronica') THEN
    CREATE INDEX ix_documentos_electronicos_estado ON public.documentos_electronicos (estado);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260307000000_AgregarFacturacionElectronica') THEN
    CREATE INDEX ix_documentos_electronicos_sucursal_fecha ON public.documentos_electronicos (sucursal_id, fecha_emision);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260307000000_AgregarFacturacionElectronica') THEN
    CREATE INDEX ix_documentos_electronicos_venta_id ON public.documentos_electronicos (venta_id) WHERE venta_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260307000000_AgregarFacturacionElectronica') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260307000000_AgregarFacturacionElectronica', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260308000000_AgregarIndicesRendimiento2') THEN
    ALTER TABLE public.documentos_electronicos DROP CONSTRAINT pk_documentos_electronicos;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260308000000_AgregarIndicesRendimiento2') THEN
    ALTER TABLE public.configuracion_emisor DROP CONSTRAINT pk_configuracion_emisor;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260308000000_AgregarIndicesRendimiento2') THEN
    ALTER TABLE public.documentos_electronicos RENAME COLUMN id TO "Id";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260308000000_AgregarIndicesRendimiento2') THEN
    ALTER TABLE public.configuracion_emisor RENAME COLUMN id TO "Id";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260308000000_AgregarIndicesRendimiento2') THEN
    ALTER TABLE public.documentos_electronicos ADD CONSTRAINT "PK_documentos_electronicos" PRIMARY KEY ("Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260308000000_AgregarIndicesRendimiento2') THEN
    ALTER TABLE public.configuracion_emisor ADD CONSTRAINT "PK_configuracion_emisor" PRIMARY KEY ("Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260308000000_AgregarIndicesRendimiento2') THEN
    ALTER TABLE public.configuracion_emisor ADD CONSTRAINT "FK_configuracion_emisor_sucursales_sucursal_id" FOREIGN KEY (sucursal_id) REFERENCES public.sucursales ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260308000000_AgregarIndicesRendimiento2') THEN
    ALTER TABLE public.documentos_electronicos ADD CONSTRAINT "FK_documentos_electronicos_sucursales_sucursal_id" FOREIGN KEY (sucursal_id) REFERENCES public.sucursales ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260308000000_AgregarIndicesRendimiento2') THEN
    ALTER TABLE public.documentos_electronicos ADD CONSTRAINT "FK_documentos_electronicos_ventas_venta_id" FOREIGN KEY (venta_id) REFERENCES public.ventas ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260308000000_AgregarIndicesRendimiento2') THEN
    CREATE INDEX ix_documentos_electronicos_sucursal_estado ON public.documentos_electronicos (sucursal_id, estado);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260308000000_AgregarIndicesRendimiento2') THEN
    CREATE INDEX ix_documentos_electronicos_venta_tipo ON public.documentos_electronicos (venta_id, tipo_documento) WHERE venta_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260308000000_AgregarIndicesRendimiento2') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260308000000_AgregarIndicesRendimiento2', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260308024854_AddUsuarioSucursalesTable') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260308024854_AddUsuarioSucursalesTable', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260308180530_AddFechaAsignacionToUsuarioSucursales') THEN

                    DO $$
                    BEGIN
                        IF NOT EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_schema = 'public'
                              AND table_name = 'usuario_sucursales'
                              AND column_name = 'fecha_asignacion'
                        ) THEN
                            ALTER TABLE public.usuario_sucursales
                                ADD COLUMN fecha_asignacion timestamp with time zone NOT NULL DEFAULT NOW();
                        END IF;
                    END $$;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260308180530_AddFechaAsignacionToUsuarioSucursales') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260308180530_AddFechaAsignacionToUsuarioSucursales', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260309133713_AddUniqueEmailIndex') THEN

                    -- Remove duplicate emails keeping the record with the smallest id
                    DELETE FROM public.usuarios
                    WHERE id NOT IN (
                        SELECT MIN(id) FROM public.usuarios GROUP BY LOWER(email)
                    );

                    -- Drop the old non-unique index if it exists
                    DROP INDEX IF EXISTS public.ix_usuarios_email;

                    -- Create the unique index if it does not already exist
                    CREATE UNIQUE INDEX IF NOT EXISTS ix_usuarios_email
                        ON public.usuarios (email);
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260309133713_AddUniqueEmailIndex') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260309133713_AddUniqueEmailIndex', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260309183242_AgregarConceptoRetencion') THEN
    ALTER TABLE public.retenciones_reglas ADD "ConceptoRetencionId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260309183242_AgregarConceptoRetencion') THEN
    ALTER TABLE public.productos ADD concepto_retencion_id integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260309183242_AgregarConceptoRetencion') THEN
    CREATE TABLE public.conceptos_retencion (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "Nombre" character varying(100) NOT NULL,
        "CodigoDian" character varying(10),
        "PorcentajeSugerido" numeric(5,2),
        "Activo" boolean NOT NULL DEFAULT TRUE,
        "CreadoPor" text NOT NULL,
        "FechaCreacion" timestamp with time zone NOT NULL,
        "ModificadoPor" text,
        "FechaModificacion" timestamp with time zone,
        CONSTRAINT "PK_conceptos_retencion" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260309183242_AgregarConceptoRetencion') THEN
    INSERT INTO public.conceptos_retencion ("Id", "Activo", "CodigoDian", "CreadoPor", "FechaCreacion", "FechaModificacion", "ModificadoPor", "Nombre", "PorcentajeSugerido")
    VALUES (1, TRUE, '2307', '', TIMESTAMPTZ '2026-01-01T00:00:00Z', NULL, NULL, 'Compras generales', 2.5);
    INSERT INTO public.conceptos_retencion ("Id", "Activo", "CodigoDian", "CreadoPor", "FechaCreacion", "FechaModificacion", "ModificadoPor", "Nombre", "PorcentajeSugerido")
    VALUES (2, TRUE, '2304', '', TIMESTAMPTZ '2026-01-01T00:00:00Z', NULL, NULL, 'Servicios generales', 4.0);
    INSERT INTO public.conceptos_retencion ("Id", "Activo", "CodigoDian", "CreadoPor", "FechaCreacion", "FechaModificacion", "ModificadoPor", "Nombre", "PorcentajeSugerido")
    VALUES (3, TRUE, '2301', '', TIMESTAMPTZ '2026-01-01T00:00:00Z', NULL, NULL, 'Honorarios', 11.0);
    INSERT INTO public.conceptos_retencion ("Id", "Activo", "CodigoDian", "CreadoPor", "FechaCreacion", "FechaModificacion", "ModificadoPor", "Nombre", "PorcentajeSugerido")
    VALUES (4, TRUE, '2302', '', TIMESTAMPTZ '2026-01-01T00:00:00Z', NULL, NULL, 'Comisiones', 11.0);
    INSERT INTO public.conceptos_retencion ("Id", "Activo", "CodigoDian", "CreadoPor", "FechaCreacion", "FechaModificacion", "ModificadoPor", "Nombre", "PorcentajeSugerido")
    VALUES (5, TRUE, '2306', '', TIMESTAMPTZ '2026-01-01T00:00:00Z', NULL, NULL, 'Arrendamientos', 3.5);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260309183242_AgregarConceptoRetencion') THEN
    UPDATE public.retenciones_reglas SET "ConceptoRetencionId" = NULL
    WHERE "Id" = 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260309183242_AgregarConceptoRetencion') THEN
    UPDATE public.retenciones_reglas SET "ConceptoRetencionId" = NULL
    WHERE "Id" = 2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260309183242_AgregarConceptoRetencion') THEN
    UPDATE public.retenciones_reglas SET "ConceptoRetencionId" = NULL
    WHERE "Id" = 3;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260309183242_AgregarConceptoRetencion') THEN
    CREATE INDEX "IX_retenciones_reglas_ConceptoRetencionId" ON public.retenciones_reglas ("ConceptoRetencionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260309183242_AgregarConceptoRetencion') THEN
    CREATE INDEX "IX_productos_concepto_retencion_id" ON public.productos (concepto_retencion_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260309183242_AgregarConceptoRetencion') THEN
    ALTER TABLE public.productos ADD CONSTRAINT "FK_productos_conceptos_retencion_concepto_retencion_id" FOREIGN KEY (concepto_retencion_id) REFERENCES public.conceptos_retencion ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260309183242_AgregarConceptoRetencion') THEN
    ALTER TABLE public.retenciones_reglas ADD CONSTRAINT "FK_retenciones_reglas_conceptos_retencion_ConceptoRetencionId" FOREIGN KEY ("ConceptoRetencionId") REFERENCES public.conceptos_retencion ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260309183242_AgregarConceptoRetencion') THEN
    PERFORM setval(
        pg_get_serial_sequence('public.conceptos_retencion', 'Id'),
        GREATEST(
            (SELECT MAX("Id") FROM public.conceptos_retencion) + 1,
            nextval(pg_get_serial_sequence('public.conceptos_retencion', 'Id'))),
        false);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260309183242_AgregarConceptoRetencion') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260309183242_AgregarConceptoRetencion', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310000123_AddErpIntegration_Sinco') THEN
    ALTER TABLE public.ordenes_compra ADD "ErpReferencia" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310000123_AddErpIntegration_Sinco') THEN
    ALTER TABLE public.ordenes_compra ADD "ErrorSincronizacion" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310000123_AddErpIntegration_Sinco') THEN
    ALTER TABLE public.ordenes_compra ADD "FechaSincronizacionErp" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310000123_AddErpIntegration_Sinco') THEN
    ALTER TABLE public.ordenes_compra ADD "SincronizadoErp" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310000123_AddErpIntegration_Sinco') THEN
    ALTER TABLE public.categorias ADD cuenta_costo character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310000123_AddErpIntegration_Sinco') THEN
    ALTER TABLE public.categorias ADD cuenta_ingreso character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310000123_AddErpIntegration_Sinco') THEN
    ALTER TABLE public.categorias ADD cuenta_inventario character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310000123_AddErpIntegration_Sinco') THEN
    ALTER TABLE public.categorias ADD external_id character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310000123_AddErpIntegration_Sinco') THEN
    ALTER TABLE public.categorias ADD origen_datos character varying(50) NOT NULL DEFAULT 'Local';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310000123_AddErpIntegration_Sinco') THEN
    CREATE TABLE public.erp_outbox_messages (
        "Id" bigint GENERATED ALWAYS AS IDENTITY,
        tipo_documento character varying(100) NOT NULL,
        entidad_id integer NOT NULL,
        payload jsonb NOT NULL,
        fecha_creacion timestamp with time zone NOT NULL,
        fecha_procesamiento timestamp with time zone,
        intentos integer NOT NULL DEFAULT 0,
        ultimo_error text,
        estado integer NOT NULL DEFAULT 0,
        CONSTRAINT "PK_erp_outbox_messages" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310000123_AddErpIntegration_Sinco') THEN
    CREATE INDEX ix_erp_outbox_estado_fecha ON public.erp_outbox_messages (estado, fecha_creacion);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310000123_AddErpIntegration_Sinco') THEN
    CREATE INDEX ix_erp_outbox_tipo_entidad ON public.erp_outbox_messages (tipo_documento, entidad_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310000123_AddErpIntegration_Sinco') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260310000123_AddErpIntegration_Sinco', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310004207_AddFormaPago_OrdenCompra') THEN
    ALTER TABLE public.ordenes_compra ADD "DiasPlazo" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310004207_AddFormaPago_OrdenCompra') THEN
    ALTER TABLE public.ordenes_compra ADD "FormaPago" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310004207_AddFormaPago_OrdenCompra') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260310004207_AddFormaPago_OrdenCompra', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310010001_AddDocumentoContableTablas') THEN
    CREATE TABLE public."DocumentosContables" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "TipoDocumento" text NOT NULL,
        "NumeroSoporte" text NOT NULL,
        "TerceroId" integer,
        "SucursalId" integer,
        "FechaCausacion" timestamp with time zone NOT NULL,
        "FormaPago" text NOT NULL,
        "FechaVencimiento" timestamp with time zone,
        "TotalDebito" numeric NOT NULL,
        "TotalCredito" numeric NOT NULL,
        "SincronizadoErp" boolean NOT NULL,
        "ErpReferencia" text,
        "FechaSincronizacionErp" timestamp with time zone,
        "CreadoPor" text NOT NULL,
        "FechaCreacion" timestamp with time zone NOT NULL,
        "ModificadoPor" text,
        "FechaModificacion" timestamp with time zone,
        "Activo" boolean NOT NULL,
        CONSTRAINT "PK_DocumentosContables" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_DocumentosContables_sucursales_SucursalId" FOREIGN KEY ("SucursalId") REFERENCES public.sucursales ("Id"),
        CONSTRAINT "FK_DocumentosContables_terceros_TerceroId" FOREIGN KEY ("TerceroId") REFERENCES public.terceros ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310010001_AddDocumentoContableTablas') THEN
    CREATE TABLE public."DetallesDocumentoContable" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "DocumentoContableId" integer NOT NULL,
        "CuentaContable" text NOT NULL,
        "Naturaleza" text NOT NULL,
        "Valor" numeric NOT NULL,
        "Nota" text NOT NULL,
        CONSTRAINT "PK_DetallesDocumentoContable" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_DetallesDocumentoContable_DocumentosContables_DocumentoCont~" FOREIGN KEY ("DocumentoContableId") REFERENCES public."DocumentosContables" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310010001_AddDocumentoContableTablas') THEN
    CREATE INDEX "IX_DetallesDocumentoContable_DocumentoContableId" ON public."DetallesDocumentoContable" ("DocumentoContableId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310010001_AddDocumentoContableTablas') THEN
    CREATE INDEX "IX_DocumentosContables_SucursalId" ON public."DocumentosContables" ("SucursalId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310010001_AddDocumentoContableTablas') THEN
    CREATE INDEX "IX_DocumentosContables_TerceroId" ON public."DocumentosContables" ("TerceroId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310010001_AddDocumentoContableTablas') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260310010001_AddDocumentoContableTablas', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310032657_AddCentroCosto_Sucursal_Documento') THEN
    ALTER TABLE public.sucursales ADD "CentroCosto" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310032657_AddCentroCosto_Sucursal_Documento') THEN
    ALTER TABLE public."DetallesDocumentoContable" ADD "CentroCosto" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260310032657_AddCentroCosto_Sucursal_Documento') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260310032657_AddCentroCosto_Sucursal_Documento', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311131528_AgregarLotesVencimiento') THEN
    ALTER TABLE public.sucursales ADD dias_alerta_vencimiento_lotes integer NOT NULL DEFAULT 30;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311131528_AgregarLotesVencimiento') THEN
    ALTER TABLE public.productos ADD maneja_lotes boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311131528_AgregarLotesVencimiento') THEN
    ALTER TABLE public.lotes_inventario ADD fecha_vencimiento date;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311131528_AgregarLotesVencimiento') THEN
    ALTER TABLE public.lotes_inventario ADD numero_lote character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311131528_AgregarLotesVencimiento') THEN
    ALTER TABLE public.lotes_inventario ADD orden_compra_id integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311131528_AgregarLotesVencimiento') THEN
    ALTER TABLE public.detalle_ventas ADD lote_inventario_id integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311131528_AgregarLotesVencimiento') THEN
    ALTER TABLE public.detalle_ventas ADD numero_lote character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311131528_AgregarLotesVencimiento') THEN
    CREATE INDEX ix_lotes_fefo ON public.lotes_inventario (producto_id, sucursal_id, fecha_vencimiento) WHERE cantidad_disponible > 0 AND fecha_vencimiento IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311131528_AgregarLotesVencimiento') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260311131528_AgregarLotesVencimiento', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311151348_AgregarDiasVidaUtil') THEN
    ALTER TABLE public.productos ADD dias_vida_util integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311151348_AgregarDiasVidaUtil') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260311151348_AgregarDiasVidaUtil', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311162508_AgregarLoteEnDevolucionVenta') THEN
    ALTER TABLE public.detalle_devolucion ADD lote_inventario_id integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311162508_AgregarLoteEnDevolucionVenta') THEN
    ALTER TABLE public.detalle_devolucion ADD numero_lote character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311162508_AgregarLoteEnDevolucionVenta') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260311162508_AgregarLoteEnDevolucionVenta', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311163636_AgregarLotesEnTraslados') THEN
    ALTER TABLE public.detalle_traslados ADD fecha_vencimiento date;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311163636_AgregarLotesEnTraslados') THEN
    ALTER TABLE public.detalle_traslados ADD lote_inventario_id integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311163636_AgregarLotesEnTraslados') THEN
    ALTER TABLE public.detalle_traslados ADD numero_lote character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311163636_AgregarLotesEnTraslados') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260311163636_AgregarLotesEnTraslados', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    ALTER TABLE public.ventas ADD "FechaDesactivacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    ALTER TABLE public.usuarios ADD "FechaDesactivacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    ALTER TABLE public.traslados ADD "FechaDesactivacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    ALTER TABLE public.terceros ADD "FechaDesactivacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    ALTER TABLE public.sucursales ADD "FechaDesactivacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    ALTER TABLE public.stock ADD "FechaDesactivacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    ALTER TABLE public.retenciones_reglas ADD "FechaDesactivacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    ALTER TABLE public.productos ADD "FechaDesactivacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    ALTER TABLE public.precios_sucursal ADD "FechaDesactivacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    ALTER TABLE public.ordenes_compra ADD "FechaDesactivacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    ALTER TABLE public.lotes_inventario ADD "FechaDesactivacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    ALTER TABLE public.impuestos ADD "FechaDesactivacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    ALTER TABLE public."DocumentosContables" ADD "FechaDesactivacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    ALTER TABLE public.documentos_electronicos ADD "FechaDesactivacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    ALTER TABLE public.devoluciones_venta ADD "FechaDesactivacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    ALTER TABLE public.conceptos_retencion ADD "FechaDesactivacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    ALTER TABLE public.categorias ADD "FechaDesactivacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    ALTER TABLE public.cajas ADD "FechaDesactivacion" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    UPDATE public.conceptos_retencion SET "FechaDesactivacion" = NULL
    WHERE "Id" = 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    UPDATE public.conceptos_retencion SET "FechaDesactivacion" = NULL
    WHERE "Id" = 2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    UPDATE public.conceptos_retencion SET "FechaDesactivacion" = NULL
    WHERE "Id" = 3;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    UPDATE public.conceptos_retencion SET "FechaDesactivacion" = NULL
    WHERE "Id" = 4;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    UPDATE public.conceptos_retencion SET "FechaDesactivacion" = NULL
    WHERE "Id" = 5;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    UPDATE public.impuestos SET "FechaDesactivacion" = NULL
    WHERE "Id" = 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    UPDATE public.impuestos SET "FechaDesactivacion" = NULL
    WHERE "Id" = 2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    UPDATE public.impuestos SET "FechaDesactivacion" = NULL
    WHERE "Id" = 3;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    UPDATE public.impuestos SET "FechaDesactivacion" = NULL
    WHERE "Id" = 4;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    UPDATE public.impuestos SET "FechaDesactivacion" = NULL
    WHERE "Id" = 5;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    UPDATE public.retenciones_reglas SET "FechaDesactivacion" = NULL
    WHERE "Id" = 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    UPDATE public.retenciones_reglas SET "FechaDesactivacion" = NULL
    WHERE "Id" = 2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    UPDATE public.retenciones_reglas SET "FechaDesactivacion" = NULL
    WHERE "Id" = 3;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311173523_AgregarFechaDesactivacion') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260311173523_AgregarFechaDesactivacion', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311231306_AddSoftDeleteColumns') THEN
    ALTER TABLE public.documentos_electronicos RENAME COLUMN "FechaDesactivacion" TO fecha_desactivacion;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311231306_AddSoftDeleteColumns') THEN
    ALTER TABLE public.documentos_electronicos ADD "Activo" boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260311231306_AddSoftDeleteColumns') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260311231306_AddSoftDeleteColumns', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260312002532_AddErpSyncToVenta') THEN
    ALTER TABLE public.ventas ADD erp_referencia character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260312002532_AddErpSyncToVenta') THEN
    ALTER TABLE public.ventas ADD error_sincronizacion character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260312002532_AddErpSyncToVenta') THEN
    ALTER TABLE public.ventas ADD fecha_sincronizacion_erp timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260312002532_AddErpSyncToVenta') THEN
    ALTER TABLE public.ventas ADD sincronizado_erp boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260312002532_AddErpSyncToVenta') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260312002532_AddErpSyncToVenta', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260312223748_AddErpSyncToDevolucionVenta') THEN
    ALTER TABLE public.devoluciones_venta ADD erp_referencia character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260312223748_AddErpSyncToDevolucionVenta') THEN
    ALTER TABLE public.devoluciones_venta ADD error_sincronizacion character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260312223748_AddErpSyncToDevolucionVenta') THEN
    ALTER TABLE public.devoluciones_venta ADD fecha_sincronizacion_erp timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260312223748_AddErpSyncToDevolucionVenta') THEN
    ALTER TABLE public.devoluciones_venta ADD sincronizado_erp boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260312223748_AddErpSyncToDevolucionVenta') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260312223748_AddErpSyncToDevolucionVenta', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260317210706_AddMultiEmpresa') THEN
    ALTER TABLE public.terceros ADD "EmpresaId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260317210706_AddMultiEmpresa') THEN
    ALTER TABLE public.sucursales ADD "EmpresaId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260317210706_AddMultiEmpresa') THEN
    ALTER TABLE public.productos ADD "EmpresaId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260317210706_AddMultiEmpresa') THEN
    ALTER TABLE public.categorias ADD "EmpresaId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260317210706_AddMultiEmpresa') THEN
    CREATE TABLE public."Empresas" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "Nombre" text NOT NULL,
        "Nit" text,
        "RazonSocial" text,
        "Activo" boolean NOT NULL,
        "FechaCreacion" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Empresas" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260317210706_AddMultiEmpresa') THEN
    CREATE INDEX "IX_sucursales_EmpresaId" ON public.sucursales ("EmpresaId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260317210706_AddMultiEmpresa') THEN
    ALTER TABLE public.sucursales ADD CONSTRAINT "FK_sucursales_Empresas_EmpresaId" FOREIGN KEY ("EmpresaId") REFERENCES public."Empresas" ("Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260317210706_AddMultiEmpresa') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260317210706_AddMultiEmpresa', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260318204619_AddEmpresaIdTransactional') THEN
    ALTER TABLE public.ventas ADD "EmpresaId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260318204619_AddEmpresaIdTransactional') THEN
    ALTER TABLE public.traslados ADD "EmpresaId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260318204619_AddEmpresaIdTransactional') THEN
    ALTER TABLE public.ordenes_compra ADD "EmpresaId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260318204619_AddEmpresaIdTransactional') THEN
    ALTER TABLE public.documentos_electronicos ADD "EmpresaId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260318204619_AddEmpresaIdTransactional') THEN
    ALTER TABLE public.devoluciones_venta ADD "EmpresaId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260318204619_AddEmpresaIdTransactional') THEN
    ALTER TABLE public.cajas ADD "EmpresaId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260318204619_AddEmpresaIdTransactional') THEN

                    UPDATE public.ventas v
                    SET "EmpresaId" = s."EmpresaId"
                    FROM public.sucursales s
                    WHERE v.sucursal_id = s."Id" AND s."EmpresaId" IS NOT NULL;

                    UPDATE public.cajas c
                    SET "EmpresaId" = s."EmpresaId"
                    FROM public.sucursales s
                    WHERE c.sucursal_id = s."Id" AND s."EmpresaId" IS NOT NULL;

                    UPDATE public.ordenes_compra o
                    SET "EmpresaId" = s."EmpresaId"
                    FROM public.sucursales s
                    WHERE o.sucursal_id = s."Id" AND s."EmpresaId" IS NOT NULL;

                    UPDATE public.traslados t
                    SET "EmpresaId" = s."EmpresaId"
                    FROM public.sucursales s
                    WHERE t.sucursal_origen_id = s."Id" AND s."EmpresaId" IS NOT NULL;

                    UPDATE public.documentos_electronicos d
                    SET "EmpresaId" = s."EmpresaId"
                    FROM public.sucursales s
                    WHERE d.sucursal_id = s."Id" AND s."EmpresaId" IS NOT NULL;

                    UPDATE public.devoluciones_venta dv
                    SET "EmpresaId" = v."EmpresaId"
                    FROM public.ventas v
                    WHERE dv.venta_id = v."Id" AND v."EmpresaId" IS NOT NULL;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260318204619_AddEmpresaIdTransactional') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260318204619_AddEmpresaIdTransactional', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260319130026_AddEthicalGuard') THEN
    CREATE TABLE public."ReglasEticas" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "EmpresaId" integer,
        "Nombre" text NOT NULL,
        "Contexto" integer NOT NULL,
        "Condicion" integer NOT NULL,
        "ValorLimite" numeric NOT NULL,
        "Accion" integer NOT NULL,
        "Mensaje" text,
        "Activo" boolean NOT NULL,
        "FechaCreacion" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ReglasEticas" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260319130026_AddEthicalGuard') THEN
    CREATE TABLE public."ActivacionesReglaEtica" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "ReglaEticaId" integer NOT NULL,
        "VentaId" integer,
        "SucursalId" integer,
        "UsuarioId" integer,
        "Detalle" text,
        "AccionTomada" integer NOT NULL,
        "FechaActivacion" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ActivacionesReglaEtica" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ActivacionesReglaEtica_ReglasEticas_ReglaEticaId" FOREIGN KEY ("ReglaEticaId") REFERENCES public."ReglasEticas" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260319130026_AddEthicalGuard') THEN
    CREATE INDEX "IX_ActivacionesReglaEtica_ReglaEticaId" ON public."ActivacionesReglaEtica" ("ReglaEticaId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260319130026_AddEthicalGuard') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260319130026_AddEthicalGuard', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260322135501_AddEmpresaIdToImpuestos') THEN
    ALTER TABLE public.retenciones_reglas ADD "EmpresaId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260322135501_AddEmpresaIdToImpuestos') THEN
    ALTER TABLE public.impuestos ADD "EmpresaId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260322135501_AddEmpresaIdToImpuestos') THEN
    ALTER TABLE public.conceptos_retencion ADD "EmpresaId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260322135501_AddEmpresaIdToImpuestos') THEN
    UPDATE public.conceptos_retencion SET "EmpresaId" = NULL
    WHERE "Id" = 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260322135501_AddEmpresaIdToImpuestos') THEN
    UPDATE public.conceptos_retencion SET "EmpresaId" = NULL
    WHERE "Id" = 2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260322135501_AddEmpresaIdToImpuestos') THEN
    UPDATE public.conceptos_retencion SET "EmpresaId" = NULL
    WHERE "Id" = 3;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260322135501_AddEmpresaIdToImpuestos') THEN
    UPDATE public.conceptos_retencion SET "EmpresaId" = NULL
    WHERE "Id" = 4;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260322135501_AddEmpresaIdToImpuestos') THEN
    UPDATE public.conceptos_retencion SET "EmpresaId" = NULL
    WHERE "Id" = 5;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260322135501_AddEmpresaIdToImpuestos') THEN
    UPDATE public.impuestos SET "EmpresaId" = NULL
    WHERE "Id" = 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260322135501_AddEmpresaIdToImpuestos') THEN
    UPDATE public.impuestos SET "EmpresaId" = NULL
    WHERE "Id" = 2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260322135501_AddEmpresaIdToImpuestos') THEN
    UPDATE public.impuestos SET "EmpresaId" = NULL
    WHERE "Id" = 3;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260322135501_AddEmpresaIdToImpuestos') THEN
    UPDATE public.impuestos SET "EmpresaId" = NULL
    WHERE "Id" = 4;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260322135501_AddEmpresaIdToImpuestos') THEN
    UPDATE public.impuestos SET "EmpresaId" = NULL
    WHERE "Id" = 5;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260322135501_AddEmpresaIdToImpuestos') THEN
    UPDATE public.retenciones_reglas SET "EmpresaId" = NULL
    WHERE "Id" = 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260322135501_AddEmpresaIdToImpuestos') THEN
    UPDATE public.retenciones_reglas SET "EmpresaId" = NULL
    WHERE "Id" = 2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260322135501_AddEmpresaIdToImpuestos') THEN
    UPDATE public.retenciones_reglas SET "EmpresaId" = NULL
    WHERE "Id" = 3;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260322135501_AddEmpresaIdToImpuestos') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260322135501_AddEmpresaIdToImpuestos', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260323025710_FixUniqueIndexesPorSucursal') THEN
    DROP INDEX public.ix_ventas_numero;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260323025710_FixUniqueIndexesPorSucursal') THEN
    DROP INDEX public.ix_devoluciones_numero;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260323025710_FixUniqueIndexesPorSucursal') THEN
    CREATE UNIQUE INDEX ix_ventas_sucursal_numero ON public.ventas (sucursal_id, numero_venta);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260323025710_FixUniqueIndexesPorSucursal') THEN
    CREATE UNIQUE INDEX ix_devoluciones_empresa_numero ON public.devoluciones_venta ("EmpresaId", numero_devolucion);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public.__ef_migrations_history WHERE "MigrationId" = '20260323025710_FixUniqueIndexesPorSucursal') THEN
    INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260323025710_FixUniqueIndexesPorSucursal', '9.0.1');
    END IF;
END $EF$;
COMMIT;

