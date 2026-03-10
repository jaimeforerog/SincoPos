using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCentroCosto_Sucursal_Documento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CentroCosto",
                schema: "public",
                table: "sucursales",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CentroCosto",
                schema: "public",
                table: "DetallesDocumentoContable",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CentroCosto",
                schema: "public",
                table: "sucursales");

            migrationBuilder.DropColumn(
                name: "CentroCosto",
                schema: "public",
                table: "DetallesDocumentoContable");
        }
    }
}
