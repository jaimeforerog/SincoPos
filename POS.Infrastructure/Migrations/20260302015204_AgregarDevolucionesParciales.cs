using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarDevolucionesParciales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "devoluciones_venta",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    venta_id = table.Column<int>(type: "integer", nullable: false),
                    numero_devolucion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    total_devuelto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    fecha_devolucion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    autorizado_por_usuario_id = table.Column<int>(type: "integer", nullable: true),
                    CreadoPor = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoPor = table.Column<string>(type: "text", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devoluciones_venta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_devoluciones_venta_ventas_venta_id",
                        column: x => x.venta_id,
                        principalSchema: "public",
                        principalTable: "ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "detalle_devolucion",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    devolucion_venta_id = table.Column<int>(type: "integer", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_producto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cantidad_devuelta = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    costo_unitario = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    subtotal_devuelto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detalle_devolucion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_detalle_devolucion_devoluciones_venta_devolucion_venta_id",
                        column: x => x.devolucion_venta_id,
                        principalSchema: "public",
                        principalTable: "devoluciones_venta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_detalle_devolucion_productos_producto_id",
                        column: x => x.producto_id,
                        principalSchema: "public",
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_detalle_devolucion_devolucion_venta_id",
                schema: "public",
                table: "detalle_devolucion",
                column: "devolucion_venta_id");

            migrationBuilder.CreateIndex(
                name: "IX_detalle_devolucion_producto_id",
                schema: "public",
                table: "detalle_devolucion",
                column: "producto_id");

            migrationBuilder.CreateIndex(
                name: "ix_devoluciones_fecha",
                schema: "public",
                table: "devoluciones_venta",
                column: "fecha_devolucion");

            migrationBuilder.CreateIndex(
                name: "ix_devoluciones_numero",
                schema: "public",
                table: "devoluciones_venta",
                column: "numero_devolucion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_devoluciones_venta_fecha",
                schema: "public",
                table: "devoluciones_venta",
                columns: new[] { "venta_id", "fecha_devolucion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "detalle_devolucion",
                schema: "public");

            migrationBuilder.DropTable(
                name: "devoluciones_venta",
                schema: "public");
        }
    }
}
