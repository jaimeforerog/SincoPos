using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarLotesEnTraslados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "fecha_vencimiento",
                schema: "public",
                table: "detalle_traslados",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "lote_inventario_id",
                schema: "public",
                table: "detalle_traslados",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "numero_lote",
                schema: "public",
                table: "detalle_traslados",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fecha_vencimiento",
                schema: "public",
                table: "detalle_traslados");

            migrationBuilder.DropColumn(
                name: "lote_inventario_id",
                schema: "public",
                table: "detalle_traslados");

            migrationBuilder.DropColumn(
                name: "numero_lote",
                schema: "public",
                table: "detalle_traslados");
        }
    }
}
