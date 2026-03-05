using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarOrdenesCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ordenes_compra",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    numero_orden = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sucursal_id = table.Column<int>(type: "integer", nullable: false),
                    proveedor_id = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    fecha_orden = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_entrega_esperada = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_aprobacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_recepcion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    aprobado_por_usuario_id = table.Column<int>(type: "integer", nullable: true),
                    recibido_por_usuario_id = table.Column<int>(type: "integer", nullable: true),
                    observaciones = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    motivo_rechazo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    impuestos = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreadoPor = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoPor = table.Column<string>(type: "text", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ordenes_compra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ordenes_compra_sucursales_sucursal_id",
                        column: x => x.sucursal_id,
                        principalSchema: "public",
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ordenes_compra_terceros_proveedor_id",
                        column: x => x.proveedor_id,
                        principalSchema: "public",
                        principalTable: "terceros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "detalle_ordenes_compra",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    orden_compra_id = table.Column<int>(type: "integer", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_producto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cantidad_solicitada = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    cantidad_recibida = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    porcentaje_impuesto = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    monto_impuesto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    observaciones = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detalle_ordenes_compra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_detalle_ordenes_compra_ordenes_compra_orden_compra_id",
                        column: x => x.orden_compra_id,
                        principalSchema: "public",
                        principalTable: "ordenes_compra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_detalle_ordenes_compra_productos_producto_id",
                        column: x => x.producto_id,
                        principalSchema: "public",
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_detalle_ordenes_compra_orden_compra_id",
                schema: "public",
                table: "detalle_ordenes_compra",
                column: "orden_compra_id");

            migrationBuilder.CreateIndex(
                name: "IX_detalle_ordenes_compra_producto_id",
                schema: "public",
                table: "detalle_ordenes_compra",
                column: "producto_id");

            migrationBuilder.CreateIndex(
                name: "ix_ordenes_compra_fecha",
                schema: "public",
                table: "ordenes_compra",
                column: "fecha_orden");

            migrationBuilder.CreateIndex(
                name: "ix_ordenes_compra_numero",
                schema: "public",
                table: "ordenes_compra",
                column: "numero_orden",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ordenes_compra_proveedor_fecha",
                schema: "public",
                table: "ordenes_compra",
                columns: new[] { "proveedor_id", "fecha_orden" });

            migrationBuilder.CreateIndex(
                name: "ix_ordenes_compra_sucursal_estado",
                schema: "public",
                table: "ordenes_compra",
                columns: new[] { "sucursal_id", "estado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "detalle_ordenes_compra",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ordenes_compra",
                schema: "public");
        }
    }
}
