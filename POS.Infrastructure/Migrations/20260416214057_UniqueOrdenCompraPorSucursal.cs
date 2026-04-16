using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UniqueOrdenCompraPorSucursal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ordenes_compra_numero",
                schema: "public",
                table: "ordenes_compra");

            migrationBuilder.CreateIndex(
                name: "ix_ordenes_compra_sucursal_numero",
                schema: "public",
                table: "ordenes_compra",
                columns: new[] { "sucursal_id", "numero_orden" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ordenes_compra_sucursal_numero",
                schema: "public",
                table: "ordenes_compra");

            migrationBuilder.CreateIndex(
                name: "ix_ordenes_compra_numero",
                schema: "public",
                table: "ordenes_compra",
                column: "numero_orden",
                unique: true);
        }
    }
}
