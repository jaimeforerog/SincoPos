using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDevolucionCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "devoluciones_compra",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    orden_compra_id = table.Column<int>(type: "integer", nullable: false),
                    numero_devolucion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    fecha_devolucion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    autorizado_por_usuario_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devoluciones_compra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_devoluciones_compra_ordenes_compra_orden_compra_id",
                        column: x => x.orden_compra_id,
                        principalSchema: "public",
                        principalTable: "ordenes_compra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "detalle_devoluciones_compra",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    devolucion_compra_id = table.Column<int>(type: "integer", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_producto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cantidad_devuelta = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detalle_devoluciones_compra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_detalle_devoluciones_compra_devoluciones_compra_devolucion_~",
                        column: x => x.devolucion_compra_id,
                        principalSchema: "public",
                        principalTable: "devoluciones_compra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_detalle_devoluciones_compra_devolucion_compra_id",
                schema: "public",
                table: "detalle_devoluciones_compra",
                column: "devolucion_compra_id");

            migrationBuilder.CreateIndex(
                name: "ix_devoluciones_compra_numero",
                schema: "public",
                table: "devoluciones_compra",
                column: "numero_devolucion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_devoluciones_compra_orden_compra_id",
                schema: "public",
                table: "devoluciones_compra",
                column: "orden_compra_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "detalle_devoluciones_compra",
                schema: "public");

            migrationBuilder.DropTable(
                name: "devoluciones_compra",
                schema: "public");
        }
    }
}
