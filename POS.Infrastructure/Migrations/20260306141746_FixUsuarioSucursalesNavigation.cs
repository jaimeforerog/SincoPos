using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixUsuarioSucursalesNavigation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_documentos_electronicos",
                schema: "public",
                table: "documentos_electronicos");

            migrationBuilder.DropPrimaryKey(
                name: "pk_configuracion_emisor",
                schema: "public",
                table: "configuracion_emisor");

            migrationBuilder.RenameColumn(
                name: "id",
                schema: "public",
                table: "documentos_electronicos",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "id",
                schema: "public",
                table: "configuracion_emisor",
                newName: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_documentos_electronicos",
                schema: "public",
                table: "documentos_electronicos",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_configuracion_emisor",
                schema: "public",
                table: "configuracion_emisor",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_configuracion_emisor_sucursales_sucursal_id",
                schema: "public",
                table: "configuracion_emisor",
                column: "sucursal_id",
                principalSchema: "public",
                principalTable: "sucursales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_documentos_electronicos_sucursales_sucursal_id",
                schema: "public",
                table: "documentos_electronicos",
                column: "sucursal_id",
                principalSchema: "public",
                principalTable: "sucursales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_documentos_electronicos_ventas_venta_id",
                schema: "public",
                table: "documentos_electronicos",
                column: "venta_id",
                principalSchema: "public",
                principalTable: "ventas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_configuracion_emisor_sucursales_sucursal_id",
                schema: "public",
                table: "configuracion_emisor");

            migrationBuilder.DropForeignKey(
                name: "FK_documentos_electronicos_sucursales_sucursal_id",
                schema: "public",
                table: "documentos_electronicos");

            migrationBuilder.DropForeignKey(
                name: "FK_documentos_electronicos_ventas_venta_id",
                schema: "public",
                table: "documentos_electronicos");

            migrationBuilder.DropPrimaryKey(
                name: "PK_documentos_electronicos",
                schema: "public",
                table: "documentos_electronicos");

            migrationBuilder.DropPrimaryKey(
                name: "PK_configuracion_emisor",
                schema: "public",
                table: "configuracion_emisor");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "public",
                table: "documentos_electronicos",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "public",
                table: "configuracion_emisor",
                newName: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_documentos_electronicos",
                schema: "public",
                table: "documentos_electronicos",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_configuracion_emisor",
                schema: "public",
                table: "configuracion_emisor",
                column: "id");
        }
    }
}
