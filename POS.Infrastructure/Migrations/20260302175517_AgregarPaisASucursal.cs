using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPaisASucursal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoPais",
                schema: "public",
                table: "sucursales",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombrePais",
                schema: "public",
                table: "sucursales",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodigoPais",
                schema: "public",
                table: "sucursales");

            migrationBuilder.DropColumn(
                name: "NombrePais",
                schema: "public",
                table: "sucursales");
        }
    }
}
