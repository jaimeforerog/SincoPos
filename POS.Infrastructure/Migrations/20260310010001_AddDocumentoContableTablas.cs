using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentoContableTablas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentosContables",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TipoDocumento = table.Column<string>(type: "text", nullable: false),
                    NumeroSoporte = table.Column<string>(type: "text", nullable: false),
                    TerceroId = table.Column<int>(type: "integer", nullable: true),
                    SucursalId = table.Column<int>(type: "integer", nullable: true),
                    FechaCausacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FormaPago = table.Column<string>(type: "text", nullable: false),
                    FechaVencimiento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalDebito = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalCredito = table.Column<decimal>(type: "numeric", nullable: false),
                    SincronizadoErp = table.Column<bool>(type: "boolean", nullable: false),
                    ErpReferencia = table.Column<string>(type: "text", nullable: true),
                    FechaSincronizacionErp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPor = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoPor = table.Column<string>(type: "text", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentosContables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentosContables_sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalSchema: "public",
                        principalTable: "sucursales",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DocumentosContables_terceros_TerceroId",
                        column: x => x.TerceroId,
                        principalSchema: "public",
                        principalTable: "terceros",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DetallesDocumentoContable",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentoContableId = table.Column<int>(type: "integer", nullable: false),
                    CuentaContable = table.Column<string>(type: "text", nullable: false),
                    Naturaleza = table.Column<string>(type: "text", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric", nullable: false),
                    Nota = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesDocumentoContable", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DetallesDocumentoContable_DocumentosContables_DocumentoCont~",
                        column: x => x.DocumentoContableId,
                        principalSchema: "public",
                        principalTable: "DocumentosContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DetallesDocumentoContable_DocumentoContableId",
                schema: "public",
                table: "DetallesDocumentoContable",
                column: "DocumentoContableId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosContables_SucursalId",
                schema: "public",
                table: "DocumentosContables",
                column: "SucursalId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosContables_TerceroId",
                schema: "public",
                table: "DocumentosContables",
                column: "TerceroId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DetallesDocumentoContable",
                schema: "public");

            migrationBuilder.DropTable(
                name: "DocumentosContables",
                schema: "public");
        }
    }
}
