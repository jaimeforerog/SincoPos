using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FechaDesactivacion",
                schema: "public",
                table: "documentos_electronicos",
                newName: "fecha_desactivacion");

            migrationBuilder.AddColumn<bool>(
                name: "Activo",
                schema: "public",
                table: "documentos_electronicos",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Activo",
                schema: "public",
                table: "documentos_electronicos");

            migrationBuilder.RenameColumn(
                name: "fecha_desactivacion",
                schema: "public",
                table: "documentos_electronicos",
                newName: "FechaDesactivacion");
        }
    }
}
