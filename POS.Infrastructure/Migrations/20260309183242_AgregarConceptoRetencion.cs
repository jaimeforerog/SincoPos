using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarConceptoRetencion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConceptoRetencionId",
                schema: "public",
                table: "retenciones_reglas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "concepto_retencion_id",
                schema: "public",
                table: "productos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "conceptos_retencion",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CodigoDian = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    PorcentajeSugerido = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreadoPor = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoPor = table.Column<string>(type: "text", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conceptos_retencion", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "conceptos_retencion",
                columns: new[] { "Id", "Activo", "CodigoDian", "CreadoPor", "FechaCreacion", "FechaModificacion", "ModificadoPor", "Nombre", "PorcentajeSugerido" },
                values: new object[,]
                {
                    { 1, true, "2307", "", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Compras generales", 2.5m },
                    { 2, true, "2304", "", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Servicios generales", 4m },
                    { 3, true, "2301", "", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Honorarios", 11m },
                    { 4, true, "2302", "", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Comisiones", 11m },
                    { 5, true, "2306", "", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Arrendamientos", 3.5m }
                });

            migrationBuilder.UpdateData(
                schema: "public",
                table: "retenciones_reglas",
                keyColumn: "Id",
                keyValue: 1,
                column: "ConceptoRetencionId",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "retenciones_reglas",
                keyColumn: "Id",
                keyValue: 2,
                column: "ConceptoRetencionId",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "retenciones_reglas",
                keyColumn: "Id",
                keyValue: 3,
                column: "ConceptoRetencionId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_retenciones_reglas_ConceptoRetencionId",
                schema: "public",
                table: "retenciones_reglas",
                column: "ConceptoRetencionId");

            migrationBuilder.CreateIndex(
                name: "IX_productos_concepto_retencion_id",
                schema: "public",
                table: "productos",
                column: "concepto_retencion_id");

            migrationBuilder.AddForeignKey(
                name: "FK_productos_conceptos_retencion_concepto_retencion_id",
                schema: "public",
                table: "productos",
                column: "concepto_retencion_id",
                principalSchema: "public",
                principalTable: "conceptos_retencion",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_retenciones_reglas_conceptos_retencion_ConceptoRetencionId",
                schema: "public",
                table: "retenciones_reglas",
                column: "ConceptoRetencionId",
                principalSchema: "public",
                principalTable: "conceptos_retencion",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_productos_conceptos_retencion_concepto_retencion_id",
                schema: "public",
                table: "productos");

            migrationBuilder.DropForeignKey(
                name: "FK_retenciones_reglas_conceptos_retencion_ConceptoRetencionId",
                schema: "public",
                table: "retenciones_reglas");

            migrationBuilder.DropTable(
                name: "conceptos_retencion",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_retenciones_reglas_ConceptoRetencionId",
                schema: "public",
                table: "retenciones_reglas");

            migrationBuilder.DropIndex(
                name: "IX_productos_concepto_retencion_id",
                schema: "public",
                table: "productos");

            migrationBuilder.DropColumn(
                name: "ConceptoRetencionId",
                schema: "public",
                table: "retenciones_reglas");

            migrationBuilder.DropColumn(
                name: "concepto_retencion_id",
                schema: "public",
                table: "productos");
        }
    }
}
