using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregaCategoriaIdAProducto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "impuesto_id",
                schema: "public",
                table: "productos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "monto_impuesto",
                schema: "public",
                table: "movimientos_inventario",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "porcentaje_impuesto",
                schema: "public",
                table: "movimientos_inventario",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "monto_impuesto_unitario",
                schema: "public",
                table: "lotes_inventario",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "porcentaje_impuesto",
                schema: "public",
                table: "lotes_inventario",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "monto_impuesto",
                schema: "public",
                table: "detalle_ventas",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "porcentaje_impuesto",
                schema: "public",
                table: "detalle_ventas",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "impuestos",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Porcentaje = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_impuestos", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "impuestos",
                columns: new[] { "Id", "Activo", "FechaCreacion", "Nombre", "Porcentaje" },
                values: new object[,]
                {
                    { 1, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Exento 0%", 0.00m },
                    { 2, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "IVA 5%", 0.05m },
                    { 3, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "IVA 19%", 0.19m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_productos_impuesto_id",
                schema: "public",
                table: "productos",
                column: "impuesto_id");

            migrationBuilder.AddForeignKey(
                name: "FK_productos_impuestos_impuesto_id",
                schema: "public",
                table: "productos",
                column: "impuesto_id",
                principalSchema: "public",
                principalTable: "impuestos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_productos_impuestos_impuesto_id",
                schema: "public",
                table: "productos");

            migrationBuilder.DropTable(
                name: "impuestos",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_productos_impuesto_id",
                schema: "public",
                table: "productos");

            migrationBuilder.DropColumn(
                name: "impuesto_id",
                schema: "public",
                table: "productos");

            migrationBuilder.DropColumn(
                name: "monto_impuesto",
                schema: "public",
                table: "movimientos_inventario");

            migrationBuilder.DropColumn(
                name: "porcentaje_impuesto",
                schema: "public",
                table: "movimientos_inventario");

            migrationBuilder.DropColumn(
                name: "monto_impuesto_unitario",
                schema: "public",
                table: "lotes_inventario");

            migrationBuilder.DropColumn(
                name: "porcentaje_impuesto",
                schema: "public",
                table: "lotes_inventario");

            migrationBuilder.DropColumn(
                name: "monto_impuesto",
                schema: "public",
                table: "detalle_ventas");

            migrationBuilder.DropColumn(
                name: "porcentaje_impuesto",
                schema: "public",
                table: "detalle_ventas");
        }
    }
}
