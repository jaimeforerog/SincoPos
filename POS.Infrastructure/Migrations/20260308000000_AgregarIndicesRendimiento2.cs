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
        }
    }
}
