using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFormaPago_OrdenCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiasPlazo",
                schema: "public",
                table: "ordenes_compra",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FormaPago",
                schema: "public",
                table: "ordenes_compra",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiasPlazo",
                schema: "public",
                table: "ordenes_compra");

            migrationBuilder.DropColumn(
                name: "FormaPago",
                schema: "public",
                table: "ordenes_compra");
        }
    }
}
