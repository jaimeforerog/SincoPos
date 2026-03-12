using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddErpSyncToDevolucionVenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "erp_referencia",
                schema: "public",
                table: "devoluciones_venta",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "error_sincronizacion",
                schema: "public",
                table: "devoluciones_venta",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "fecha_sincronizacion_erp",
                schema: "public",
                table: "devoluciones_venta",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "sincronizado_erp",
                schema: "public",
                table: "devoluciones_venta",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "erp_referencia",
                schema: "public",
                table: "devoluciones_venta");

            migrationBuilder.DropColumn(
                name: "error_sincronizacion",
                schema: "public",
                table: "devoluciones_venta");

            migrationBuilder.DropColumn(
                name: "fecha_sincronizacion_erp",
                schema: "public",
                table: "devoluciones_venta");

            migrationBuilder.DropColumn(
                name: "sincronizado_erp",
                schema: "public",
                table: "devoluciones_venta");
        }
    }
}
