using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ImpuestosEnCompras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "requiere_factura_electronica",
                schema: "public",
                table: "ordenes_compra",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "nombre_impuesto",
                schema: "public",
                table: "detalle_ordenes_compra",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "requiere_factura_electronica",
                schema: "public",
                table: "ordenes_compra");

            migrationBuilder.DropColumn(
                name: "nombre_impuesto",
                schema: "public",
                table: "detalle_ordenes_compra");
        }
    }
}
