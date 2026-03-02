using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVentasYPrecios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "margen_ganancia",
                schema: "public",
                table: "categorias",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0.30m);

            migrationBuilder.CreateTable(
                name: "precios_sucursal",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sucursal_id = table.Column<int>(type: "integer", nullable: false),
                    precio_venta = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    precio_minimo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    fecha_actualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_precios_sucursal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_precios_sucursal_productos_producto_id",
                        column: x => x.producto_id,
                        principalSchema: "public",
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_precios_sucursal_sucursales_sucursal_id",
                        column: x => x.sucursal_id,
                        principalSchema: "public",
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ventas",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    numero_venta = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sucursal_id = table.Column<int>(type: "integer", nullable: false),
                    caja_id = table.Column<int>(type: "integer", nullable: false),
                    cliente_id = table.Column<int>(type: "integer", nullable: true),
                    usuario_id = table.Column<int>(type: "integer", nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    descuento = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    impuestos = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    metodo_pago = table.Column<int>(type: "integer", nullable: false),
                    monto_pagado = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    cambio = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    observaciones = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fecha_venta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ventas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ventas_cajas_caja_id",
                        column: x => x.caja_id,
                        principalSchema: "public",
                        principalTable: "cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ventas_sucursales_sucursal_id",
                        column: x => x.sucursal_id,
                        principalSchema: "public",
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ventas_terceros_cliente_id",
                        column: x => x.cliente_id,
                        principalSchema: "public",
                        principalTable: "terceros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "detalle_ventas",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    venta_id = table.Column<int>(type: "integer", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_producto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    costo_unitario = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    descuento = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detalle_ventas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_detalle_ventas_productos_producto_id",
                        column: x => x.producto_id,
                        principalSchema: "public",
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_detalle_ventas_ventas_venta_id",
                        column: x => x.venta_id,
                        principalSchema: "public",
                        principalTable: "ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_detalle_ventas_producto_id",
                schema: "public",
                table: "detalle_ventas",
                column: "producto_id");

            migrationBuilder.CreateIndex(
                name: "IX_detalle_ventas_venta_id",
                schema: "public",
                table: "detalle_ventas",
                column: "venta_id");

            migrationBuilder.CreateIndex(
                name: "ix_precios_sucursal_producto_sucursal",
                schema: "public",
                table: "precios_sucursal",
                columns: new[] { "producto_id", "sucursal_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_precios_sucursal_sucursal_id",
                schema: "public",
                table: "precios_sucursal",
                column: "sucursal_id");

            migrationBuilder.CreateIndex(
                name: "IX_ventas_caja_id",
                schema: "public",
                table: "ventas",
                column: "caja_id");

            migrationBuilder.CreateIndex(
                name: "IX_ventas_cliente_id",
                schema: "public",
                table: "ventas",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "ix_ventas_fecha",
                schema: "public",
                table: "ventas",
                column: "fecha_venta");

            migrationBuilder.CreateIndex(
                name: "ix_ventas_numero",
                schema: "public",
                table: "ventas",
                column: "numero_venta",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ventas_sucursal_fecha",
                schema: "public",
                table: "ventas",
                columns: new[] { "sucursal_id", "fecha_venta" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "detalle_ventas",
                schema: "public");

            migrationBuilder.DropTable(
                name: "precios_sucursal",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ventas",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "margen_ganancia",
                schema: "public",
                table: "categorias");
        }
    }
}
