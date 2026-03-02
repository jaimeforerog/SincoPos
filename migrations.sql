CREATE TABLE IF NOT EXISTS public.__ef_migrations_history (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___ef_migrations_history" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE public.categorias (
    "Id" integer GENERATED ALWAYS AS IDENTITY,
    nombre character varying(100) NOT NULL,
    descripcion character varying(300),
    activo boolean NOT NULL,
    CONSTRAINT "PK_categorias" PRIMARY KEY ("Id")
);

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

CREATE INDEX "IX_productos_categoria_id" ON public.productos (categoria_id);

CREATE UNIQUE INDEX ix_productos_codigo_barras ON public.productos (codigo_barras);

INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20260224194002_InitialCreate', '9.0.1');


                INSERT INTO public.categorias OVERRIDING SYSTEM VALUE
                VALUES (1, 'General', NULL, true)
                ON CONFLICT ("Id") DO NOTHING;
            


                SELECT setval(pg_get_serial_sequence('public.categorias', 'Id'),
                    COALESCE((SELECT MAX("Id") FROM public.categorias), 0) + 1, false);
            


                UPDATE public.productos SET categoria_id = 1 WHERE categoria_id IS NULL OR categoria_id = 0;
            

ALTER TABLE public.productos DROP CONSTRAINT "FK_productos_categorias_categoria_id";

UPDATE public.productos SET categoria_id = 0 WHERE categoria_id IS NULL;
ALTER TABLE public.productos ALTER COLUMN categoria_id SET NOT NULL;
ALTER TABLE public.productos ALTER COLUMN categoria_id SET DEFAULT 0;

ALTER TABLE public.productos ADD CONSTRAINT "FK_productos_categorias_categoria_id" FOREIGN KEY (categoria_id) REFERENCES public.categorias ("Id") ON DELETE RESTRICT;

INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20260224215349_MakeCategoriaIdRequired', '9.0.1');

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

CREATE UNIQUE INDEX ix_sucursales_nombre ON public.sucursales (nombre);

INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20260224231948_AddSucursales', '9.0.1');

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

CREATE UNIQUE INDEX ix_cajas_sucursal_nombre ON public.cajas (sucursal_id, nombre);

INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20260224232607_AddCajas', '9.0.1');

ALTER TABLE public.sucursales ADD metodo_costeo integer NOT NULL DEFAULT 0;

INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20260224234212_AddMetodoCosteoSucursal', '9.0.1');

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

CREATE INDEX ix_terceros_external_id ON public.terceros (external_id) WHERE external_id IS NOT NULL;

CREATE UNIQUE INDEX ix_terceros_identificacion ON public.terceros (identificacion);

INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20260224235331_AddTerceros', '9.0.1');

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

CREATE INDEX "IX_movimientos_inventario_sucursal_destino_id" ON public.movimientos_inventario (sucursal_destino_id);

CREATE INDEX "IX_movimientos_inventario_sucursal_id" ON public.movimientos_inventario (sucursal_id);

CREATE INDEX "IX_movimientos_inventario_tercero_id" ON public.movimientos_inventario (tercero_id);

CREATE INDEX ix_movimientos_producto_sucursal_fecha ON public.movimientos_inventario (producto_id, sucursal_id, fecha_movimiento);

CREATE UNIQUE INDEX ix_stock_producto_sucursal ON public.stock (producto_id, sucursal_id);

CREATE INDEX "IX_stock_sucursal_id" ON public.stock (sucursal_id);

INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20260225000249_AddInventario', '9.0.1');

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

CREATE INDEX "IX_lotes_inventario_sucursal_id" ON public.lotes_inventario (sucursal_id);

CREATE INDEX "IX_lotes_inventario_tercero_id" ON public.lotes_inventario (tercero_id);

CREATE INDEX ix_lotes_producto_sucursal_fecha ON public.lotes_inventario (producto_id, sucursal_id, fecha_entrada);

INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20260225003458_AddLotesInventario', '9.0.1');

ALTER TABLE public.categorias ADD margen_ganancia numeric(5,2) NOT NULL DEFAULT 0.3;

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

CREATE INDEX "IX_detalle_ventas_producto_id" ON public.detalle_ventas (producto_id);

CREATE INDEX "IX_detalle_ventas_venta_id" ON public.detalle_ventas (venta_id);

CREATE UNIQUE INDEX ix_precios_sucursal_producto_sucursal ON public.precios_sucursal (producto_id, sucursal_id);

CREATE INDEX "IX_precios_sucursal_sucursal_id" ON public.precios_sucursal (sucursal_id);

CREATE INDEX "IX_ventas_caja_id" ON public.ventas (caja_id);

CREATE INDEX "IX_ventas_cliente_id" ON public.ventas (cliente_id);

CREATE INDEX ix_ventas_fecha ON public.ventas (fecha_venta);

CREATE UNIQUE INDEX ix_ventas_numero ON public.ventas (numero_venta);

CREATE INDEX ix_ventas_sucursal_fecha ON public.ventas (sucursal_id, fecha_venta);

INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20260225142337_AddVentasYPrecios', '9.0.1');

ALTER TABLE public.productos ADD impuesto_id integer;

ALTER TABLE public.movimientos_inventario ADD monto_impuesto numeric(18,4) NOT NULL DEFAULT 0.0;

ALTER TABLE public.movimientos_inventario ADD porcentaje_impuesto numeric(5,4) NOT NULL DEFAULT 0.0;

ALTER TABLE public.lotes_inventario ADD monto_impuesto_unitario numeric(18,4) NOT NULL DEFAULT 0.0;

ALTER TABLE public.lotes_inventario ADD porcentaje_impuesto numeric(5,4) NOT NULL DEFAULT 0.0;

ALTER TABLE public.detalle_ventas ADD monto_impuesto numeric(18,2) NOT NULL DEFAULT 0.0;

ALTER TABLE public.detalle_ventas ADD porcentaje_impuesto numeric(5,4) NOT NULL DEFAULT 0.0;

CREATE TABLE public.impuestos (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY,
    "Nombre" character varying(50) NOT NULL,
    "Porcentaje" numeric(5,4) NOT NULL,
    "Activo" boolean NOT NULL DEFAULT TRUE,
    "FechaCreacion" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_impuestos" PRIMARY KEY ("Id")
);

INSERT INTO public.impuestos ("Id", "Activo", "FechaCreacion", "Nombre", "Porcentaje")
VALUES (1, TRUE, TIMESTAMPTZ '2026-01-01T00:00:00Z', 'Exento 0%', 0.0);
INSERT INTO public.impuestos ("Id", "Activo", "FechaCreacion", "Nombre", "Porcentaje")
VALUES (2, TRUE, TIMESTAMPTZ '2026-01-01T00:00:00Z', 'IVA 5%', 0.05);
INSERT INTO public.impuestos ("Id", "Activo", "FechaCreacion", "Nombre", "Porcentaje")
VALUES (3, TRUE, TIMESTAMPTZ '2026-01-01T00:00:00Z', 'IVA 19%', 0.19);

CREATE INDEX "IX_productos_impuesto_id" ON public.productos (impuesto_id);

ALTER TABLE public.productos ADD CONSTRAINT "FK_productos_impuestos_impuesto_id" FOREIGN KEY (impuesto_id) REFERENCES public.impuestos ("Id") ON DELETE SET NULL;

SELECT setval(
    pg_get_serial_sequence('public.impuestos', 'Id'),
    GREATEST(
        (SELECT MAX("Id") FROM public.impuestos) + 1,
        nextval(pg_get_serial_sequence('public.impuestos', 'Id'))),
    false);

INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20260225165257_AgregaCategoriaIdAProducto', '9.0.1');

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

CREATE INDEX ix_usuarios_email ON public.usuarios (email);

CREATE UNIQUE INDEX ix_usuarios_keycloak_id ON public.usuarios (keycloak_id);

CREATE INDEX "IX_usuarios_sucursal_default_id" ON public.usuarios (sucursal_default_id);

INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20260225171446_AgregarUsuarios', '9.0.1');

ALTER TABLE public.precios_sucursal RENAME COLUMN fecha_actualizacion TO "FechaCreacion";

ALTER TABLE public.ventas ADD "Activo" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE public.ventas ADD "CreadoPor" text NOT NULL DEFAULT '';

ALTER TABLE public.ventas ADD "FechaCreacion" timestamp with time zone NOT NULL DEFAULT TIMESTAMPTZ '-infinity';

ALTER TABLE public.ventas ADD "FechaModificacion" timestamp with time zone;

ALTER TABLE public.ventas ADD "ModificadoPor" text;

ALTER TABLE public.usuarios ADD "CreadoPor" text NOT NULL DEFAULT '';

ALTER TABLE public.usuarios ADD "ModificadoPor" text;

ALTER TABLE public.terceros ADD "CreadoPor" text NOT NULL DEFAULT '';

ALTER TABLE public.terceros ADD "ModificadoPor" text;

ALTER TABLE public.sucursales ADD "CreadoPor" text NOT NULL DEFAULT '';

ALTER TABLE public.sucursales ADD "FechaModificacion" timestamp with time zone;

ALTER TABLE public.sucursales ADD "ModificadoPor" text;

ALTER TABLE public.stock ADD "Activo" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE public.stock ADD "CreadoPor" text NOT NULL DEFAULT '';

ALTER TABLE public.stock ADD "FechaCreacion" timestamp with time zone NOT NULL DEFAULT TIMESTAMPTZ '-infinity';

ALTER TABLE public.stock ADD "FechaModificacion" timestamp with time zone;

ALTER TABLE public.stock ADD "ModificadoPor" text;

ALTER TABLE public.productos ADD "CreadoPor" text NOT NULL DEFAULT '';

ALTER TABLE public.productos ADD "ModificadoPor" text;

ALTER TABLE public.precios_sucursal ADD "Activo" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE public.precios_sucursal ADD "CreadoPor" text NOT NULL DEFAULT '';

ALTER TABLE public.precios_sucursal ADD "FechaModificacion" timestamp with time zone;

ALTER TABLE public.precios_sucursal ADD "ModificadoPor" text;

ALTER TABLE public.lotes_inventario ADD "Activo" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE public.lotes_inventario ADD "CreadoPor" text NOT NULL DEFAULT '';

ALTER TABLE public.lotes_inventario ADD "FechaCreacion" timestamp with time zone NOT NULL DEFAULT TIMESTAMPTZ '-infinity';

ALTER TABLE public.lotes_inventario ADD "FechaModificacion" timestamp with time zone;

ALTER TABLE public.lotes_inventario ADD "ModificadoPor" text;

ALTER TABLE public.impuestos ADD "CreadoPor" text NOT NULL DEFAULT '';

ALTER TABLE public.impuestos ADD "FechaModificacion" timestamp with time zone;

ALTER TABLE public.impuestos ADD "ModificadoPor" text;

ALTER TABLE public.categorias ADD "CreadoPor" text NOT NULL DEFAULT '';

ALTER TABLE public.categorias ADD "FechaCreacion" timestamp with time zone NOT NULL DEFAULT TIMESTAMPTZ '-infinity';

ALTER TABLE public.categorias ADD "FechaModificacion" timestamp with time zone;

ALTER TABLE public.categorias ADD "ModificadoPor" text;

ALTER TABLE public.cajas ADD "CreadoPor" text NOT NULL DEFAULT '';

ALTER TABLE public.cajas ADD "FechaModificacion" timestamp with time zone;

ALTER TABLE public.cajas ADD "ModificadoPor" text;

UPDATE public.impuestos SET "CreadoPor" = '', "FechaModificacion" = NULL, "ModificadoPor" = NULL
WHERE "Id" = 1;

UPDATE public.impuestos SET "CreadoPor" = '', "FechaModificacion" = NULL, "ModificadoPor" = NULL
WHERE "Id" = 2;

UPDATE public.impuestos SET "CreadoPor" = '', "FechaModificacion" = NULL, "ModificadoPor" = NULL
WHERE "Id" = 3;

INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20260226201111_AgregarAuditoria', '9.0.1');

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

CREATE INDEX idx_activity_dashboard ON public.activity_logs (fecha_hora, tipo, sucursal_id);

CREATE INDEX idx_activity_entidad ON public.activity_logs (tipo_entidad, entidad_id);

CREATE INDEX idx_activity_fecha ON public.activity_logs (fecha_hora);

CREATE INDEX idx_activity_tipo ON public.activity_logs (tipo);

CREATE INDEX idx_activity_usuario ON public.activity_logs (usuario_email);

CREATE INDEX "IX_activity_logs_sucursal_id" ON public.activity_logs (sucursal_id);

CREATE INDEX "IX_activity_logs_usuario_id" ON public.activity_logs (usuario_id);

INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20260226211551_AgregarActivityLogs', '9.0.1');

COMMIT;

