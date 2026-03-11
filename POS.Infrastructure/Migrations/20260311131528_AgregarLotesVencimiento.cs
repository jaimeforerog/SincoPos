using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarLotesVencimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "dias_alerta_vencimiento_lotes",
                schema: "public",
                table: "sucursales",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<bool>(
                name: "maneja_lotes",
                schema: "public",
                table: "productos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fecha_vencimiento",
                schema: "public",
                table: "lotes_inventario",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "numero_lote",
                schema: "public",
                table: "lotes_inventario",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "orden_compra_id",
                schema: "public",
                table: "lotes_inventario",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "lote_inventario_id",
                schema: "public",
                table: "detalle_ventas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "numero_lote",
                schema: "public",
                table: "detalle_ventas",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_lotes_fefo",
                schema: "public",
                table: "lotes_inventario",
                columns: new[] { "producto_id", "sucursal_id", "fecha_vencimiento" },
                filter: "cantidad_disponible > 0 AND fecha_vencimiento IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_lotes_fefo",
                schema: "public",
                table: "lotes_inventario");

            migrationBuilder.DropColumn(
                name: "dias_alerta_vencimiento_lotes",
                schema: "public",
                table: "sucursales");

            migrationBuilder.DropColumn(
                name: "maneja_lotes",
                schema: "public",
                table: "productos");

            migrationBuilder.DropColumn(
                name: "fecha_vencimiento",
                schema: "public",
                table: "lotes_inventario");

            migrationBuilder.DropColumn(
                name: "numero_lote",
                schema: "public",
                table: "lotes_inventario");

            migrationBuilder.DropColumn(
                name: "orden_compra_id",
                schema: "public",
                table: "lotes_inventario");

            migrationBuilder.DropColumn(
                name: "lote_inventario_id",
                schema: "public",
                table: "detalle_ventas");

            migrationBuilder.DropColumn(
                name: "numero_lote",
                schema: "public",
                table: "detalle_ventas");
        }
    }
}
