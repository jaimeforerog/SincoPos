using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarFechaDesactivacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaDesactivacion",
                schema: "public",
                table: "ventas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaDesactivacion",
                schema: "public",
                table: "usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaDesactivacion",
                schema: "public",
                table: "traslados",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaDesactivacion",
                schema: "public",
                table: "terceros",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaDesactivacion",
                schema: "public",
                table: "sucursales",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaDesactivacion",
                schema: "public",
                table: "stock",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaDesactivacion",
                schema: "public",
                table: "retenciones_reglas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaDesactivacion",
                schema: "public",
                table: "productos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaDesactivacion",
                schema: "public",
                table: "precios_sucursal",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaDesactivacion",
                schema: "public",
                table: "ordenes_compra",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaDesactivacion",
                schema: "public",
                table: "lotes_inventario",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaDesactivacion",
                schema: "public",
                table: "impuestos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaDesactivacion",
                schema: "public",
                table: "DocumentosContables",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaDesactivacion",
                schema: "public",
                table: "documentos_electronicos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaDesactivacion",
                schema: "public",
                table: "devoluciones_venta",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaDesactivacion",
                schema: "public",
                table: "conceptos_retencion",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaDesactivacion",
                schema: "public",
                table: "categorias",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaDesactivacion",
                schema: "public",
                table: "cajas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "conceptos_retencion",
                keyColumn: "Id",
                keyValue: 1,
                column: "FechaDesactivacion",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "conceptos_retencion",
                keyColumn: "Id",
                keyValue: 2,
                column: "FechaDesactivacion",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "conceptos_retencion",
                keyColumn: "Id",
                keyValue: 3,
                column: "FechaDesactivacion",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "conceptos_retencion",
                keyColumn: "Id",
                keyValue: 4,
                column: "FechaDesactivacion",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "conceptos_retencion",
                keyColumn: "Id",
                keyValue: 5,
                column: "FechaDesactivacion",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "impuestos",
                keyColumn: "Id",
                keyValue: 1,
                column: "FechaDesactivacion",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "impuestos",
                keyColumn: "Id",
                keyValue: 2,
                column: "FechaDesactivacion",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "impuestos",
                keyColumn: "Id",
                keyValue: 3,
                column: "FechaDesactivacion",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "impuestos",
                keyColumn: "Id",
                keyValue: 4,
                column: "FechaDesactivacion",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "impuestos",
                keyColumn: "Id",
                keyValue: 5,
                column: "FechaDesactivacion",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "retenciones_reglas",
                keyColumn: "Id",
                keyValue: 1,
                column: "FechaDesactivacion",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "retenciones_reglas",
                keyColumn: "Id",
                keyValue: 2,
                column: "FechaDesactivacion",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "retenciones_reglas",
                keyColumn: "Id",
                keyValue: 3,
                column: "FechaDesactivacion",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaDesactivacion",
                schema: "public",
                table: "ventas");

            migrationBuilder.DropColumn(
                name: "FechaDesactivacion",
                schema: "public",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "FechaDesactivacion",
                schema: "public",
                table: "traslados");

            migrationBuilder.DropColumn(
                name: "FechaDesactivacion",
                schema: "public",
                table: "terceros");

            migrationBuilder.DropColumn(
                name: "FechaDesactivacion",
                schema: "public",
                table: "sucursales");

            migrationBuilder.DropColumn(
                name: "FechaDesactivacion",
                schema: "public",
                table: "stock");

            migrationBuilder.DropColumn(
                name: "FechaDesactivacion",
                schema: "public",
                table: "retenciones_reglas");

            migrationBuilder.DropColumn(
                name: "FechaDesactivacion",
                schema: "public",
                table: "productos");

            migrationBuilder.DropColumn(
                name: "FechaDesactivacion",
                schema: "public",
                table: "precios_sucursal");

            migrationBuilder.DropColumn(
                name: "FechaDesactivacion",
                schema: "public",
                table: "ordenes_compra");

            migrationBuilder.DropColumn(
                name: "FechaDesactivacion",
                schema: "public",
                table: "lotes_inventario");

            migrationBuilder.DropColumn(
                name: "FechaDesactivacion",
                schema: "public",
                table: "impuestos");

            migrationBuilder.DropColumn(
                name: "FechaDesactivacion",
                schema: "public",
                table: "DocumentosContables");

            migrationBuilder.DropColumn(
                name: "FechaDesactivacion",
                schema: "public",
                table: "documentos_electronicos");

            migrationBuilder.DropColumn(
                name: "FechaDesactivacion",
                schema: "public",
                table: "devoluciones_venta");

            migrationBuilder.DropColumn(
                name: "FechaDesactivacion",
                schema: "public",
                table: "conceptos_retencion");

            migrationBuilder.DropColumn(
                name: "FechaDesactivacion",
                schema: "public",
                table: "categorias");

            migrationBuilder.DropColumn(
                name: "FechaDesactivacion",
                schema: "public",
                table: "cajas");
        }
    }
}
