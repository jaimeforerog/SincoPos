using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDetalleVentaLote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "detalle_venta_lotes",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    detalle_venta_id = table.Column<int>(type: "integer", nullable: false),
                    lote_inventario_id = table.Column<int>(type: "integer", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    costo_unitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    numero_lote = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detalle_venta_lotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_detalle_venta_lotes_detalle_ventas_detalle_venta_id",
                        column: x => x.detalle_venta_id,
                        principalSchema: "public",
                        principalTable: "detalle_ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_detalle_venta_lotes_lotes_inventario_lote_inventario_id",
                        column: x => x.lote_inventario_id,
                        principalSchema: "public",
                        principalTable: "lotes_inventario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dvl_detalle_venta_id",
                schema: "public",
                table: "detalle_venta_lotes",
                column: "detalle_venta_id");

            migrationBuilder.CreateIndex(
                name: "ix_dvl_lote_id",
                schema: "public",
                table: "detalle_venta_lotes",
                column: "lote_inventario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "detalle_venta_lotes",
                schema: "public");
        }
    }
}
