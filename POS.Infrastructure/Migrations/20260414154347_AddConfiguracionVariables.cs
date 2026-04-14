using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConfiguracionVariables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "configuracion_variables",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    empresa_id = table.Column<int>(type: "integer", nullable: true),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    valor = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    creado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modificado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_desactivacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracion_variables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_configuracion_variables_Empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalSchema: "public",
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_configuracion_variables_empresa_id",
                schema: "public",
                table: "configuracion_variables",
                column: "empresa_id");

            migrationBuilder.CreateIndex(
                name: "ix_configuracion_variables_nombre_empresa",
                schema: "public",
                table: "configuracion_variables",
                columns: new[] { "nombre", "empresa_id" },
                unique: true);

            // Seed: variable global de monto máximo de apertura de caja
            migrationBuilder.InsertData(
                schema: "public",
                table: "configuracion_variables",
                columns: new[] { "nombre", "valor", "descripcion", "empresa_id", "activo", "creado_por", "fecha_creacion" },
                values: new object[] {
                    "AperturaCaja_MontoMax",
                    "5000000",
                    "Monto máximo permitido al abrir una caja. Valor en pesos colombianos (COP). El sistema rechazará aperturas que superen este límite.",
                    null,
                    true,
                    "system",
                    new DateTime(2026, 4, 14, 0, 0, 0, DateTimeKind.Utc)
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuracion_variables",
                schema: "public");
        }
    }
}
