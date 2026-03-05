using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarUnidadMedidaProducto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "unidad_medida",
                schema: "public",
                table: "productos",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "94");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "unidad_medida",
                schema: "public",
                table: "productos");
        }
    }
}
