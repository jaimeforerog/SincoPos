using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEthicalGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReglasEticas",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaId = table.Column<int>(type: "integer", nullable: true),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Contexto = table.Column<int>(type: "integer", nullable: false),
                    Condicion = table.Column<int>(type: "integer", nullable: false),
                    ValorLimite = table.Column<decimal>(type: "numeric", nullable: false),
                    Accion = table.Column<int>(type: "integer", nullable: false),
                    Mensaje = table.Column<string>(type: "text", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReglasEticas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ActivacionesReglaEtica",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReglaEticaId = table.Column<int>(type: "integer", nullable: false),
                    VentaId = table.Column<int>(type: "integer", nullable: true),
                    SucursalId = table.Column<int>(type: "integer", nullable: true),
                    UsuarioId = table.Column<int>(type: "integer", nullable: true),
                    Detalle = table.Column<string>(type: "text", nullable: true),
                    AccionTomada = table.Column<int>(type: "integer", nullable: false),
                    FechaActivacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivacionesReglaEtica", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivacionesReglaEtica_ReglasEticas_ReglaEticaId",
                        column: x => x.ReglaEticaId,
                        principalSchema: "public",
                        principalTable: "ReglasEticas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivacionesReglaEtica_ReglaEticaId",
                schema: "public",
                table: "ActivacionesReglaEtica",
                column: "ReglaEticaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivacionesReglaEtica",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ReglasEticas",
                schema: "public");
        }
    }
}
