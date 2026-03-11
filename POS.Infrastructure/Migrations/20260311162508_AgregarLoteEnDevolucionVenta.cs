using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarLoteEnDevolucionVenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "lote_inventario_id",
                schema: "public",
                table: "detalle_devolucion",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "numero_lote",
                schema: "public",
                table: "detalle_devolucion",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "lote_inventario_id",
                schema: "public",
                table: "detalle_devolucion");

            migrationBuilder.DropColumn(
                name: "numero_lote",
                schema: "public",
                table: "detalle_devolucion");
        }
    }
}
