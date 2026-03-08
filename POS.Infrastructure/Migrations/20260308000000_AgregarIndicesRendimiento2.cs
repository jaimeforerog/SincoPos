using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

using POS.Infrastructure.Data;

namespace POS.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260308000000_AgregarIndicesRendimiento2")]
    public partial class AgregarIndicesRendimiento2 : Migration
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

            // Índice compuesto para filtrar documentos electrónicos por sucursal y estado
            // Usado en: FacturacionController listar (filtro más común)
            migrationBuilder.CreateIndex(
                name: "ix_documentos_electronicos_sucursal_estado",
                schema: "public",
                table: "documentos_electronicos",
                columns: ["sucursal_id", "estado"]);

            // Índice para buscar factura de una venta específica por tipo (FV, NC, ND)
            // Usado en: FacturacionService.EmitirFacturaVentaAsync (verificar si ya existe)
            migrationBuilder.CreateIndex(
                name: "ix_documentos_electronicos_venta_tipo",
                schema: "public",
                table: "documentos_electronicos",
                columns: ["venta_id", "tipo_documento"],
                filter: "venta_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_documentos_electronicos_sucursal_estado",
                schema: "public",
                table: "documentos_electronicos");

            migrationBuilder.DropIndex(
                name: "ix_documentos_electronicos_venta_tipo",
                schema: "public",
                table: "documentos_electronicos");

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
