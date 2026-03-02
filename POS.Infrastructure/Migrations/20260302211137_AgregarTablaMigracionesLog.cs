using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTablaMigracionesLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "migraciones_log",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    migracion_id = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    product_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    fecha_aplicacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    aplicado_por = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    estado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    duracion_ms = table.Column<long>(type: "bigint", nullable: false),
                    notas = table.Column<string>(type: "text", nullable: true),
                    sql_ejecutado = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_migraciones_log", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_migraciones_log_fecha_aplicacion",
                schema: "public",
                table: "migraciones_log",
                column: "fecha_aplicacion");

            migrationBuilder.CreateIndex(
                name: "IX_migraciones_log_migracion_id",
                schema: "public",
                table: "migraciones_log",
                column: "migracion_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "migraciones_log",
                schema: "public");
        }
    }
}
