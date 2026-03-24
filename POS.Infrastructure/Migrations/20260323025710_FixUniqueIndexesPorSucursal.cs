using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixUniqueIndexesPorSucursal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ventas_numero",
                schema: "public",
                table: "ventas");

            migrationBuilder.DropIndex(
                name: "ix_devoluciones_numero",
                schema: "public",
                table: "devoluciones_venta");

            migrationBuilder.CreateIndex(
                name: "ix_ventas_sucursal_numero",
                schema: "public",
                table: "ventas",
                columns: new[] { "sucursal_id", "numero_venta" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_devoluciones_empresa_numero",
                schema: "public",
                table: "devoluciones_venta",
                columns: new[] { "EmpresaId", "numero_devolucion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ventas_sucursal_numero",
                schema: "public",
                table: "ventas");

            migrationBuilder.DropIndex(
                name: "ix_devoluciones_empresa_numero",
                schema: "public",
                table: "devoluciones_venta");

            migrationBuilder.CreateIndex(
                name: "ix_ventas_numero",
                schema: "public",
                table: "ventas",
                column: "numero_venta",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_devoluciones_numero",
                schema: "public",
                table: "devoluciones_venta",
                column: "numero_devolucion",
                unique: true);
        }
    }
}
