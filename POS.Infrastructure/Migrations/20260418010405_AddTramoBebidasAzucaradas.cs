using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTramoBebidasAzucaradas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TramosBebidasAzucaradas",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaxGramosPor100ml = table.Column<decimal>(type: "numeric", nullable: true),
                    ValorPor100ml = table.Column<decimal>(type: "numeric", nullable: false),
                    VigenciaDesde = table.Column<DateOnly>(type: "date", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TramosBebidasAzucaradas", x => x.Id);
                });

            // Seed: tarifas Ley 2277/2022, Art. 513-521 (vigentes desde 2023-01-01)
            // Para actualizar: INSERT nuevas filas con nueva VigenciaDesde + Activo = true
            // y poner Activo = false en las anteriores.
            migrationBuilder.InsertData(
                table: "TramosBebidasAzucaradas",
                schema: "public",
                columns: ["MaxGramosPor100ml", "ValorPor100ml", "VigenciaDesde", "Activo"],
                values: new object[,]
                {
                    { 6m,    18m, new DateOnly(2023, 1, 1), true },  // ≤ 6 g/100ml → $18
                    { 10m,   35m, new DateOnly(2023, 1, 1), true },  // > 6 y ≤ 10 g → $35
                    { null,  55m, new DateOnly(2023, 1, 1), true },  // > 10 g → $55 (sin límite)
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TramosBebidasAzucaradas",
                schema: "public");
        }
    }
}
