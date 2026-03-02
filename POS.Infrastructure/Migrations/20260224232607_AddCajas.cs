using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCajas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cajas",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    nombre = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sucursal_id = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    monto_apertura = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    monto_actual = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    fecha_apertura = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_cierre = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    abierta_por_usuario_id = table.Column<int>(type: "integer", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cajas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cajas_sucursales_sucursal_id",
                        column: x => x.sucursal_id,
                        principalSchema: "public",
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cajas_sucursal_nombre",
                schema: "public",
                table: "cajas",
                columns: new[] { "sucursal_id", "nombre" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cajas",
                schema: "public");
        }
    }
}
