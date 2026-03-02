using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarUsuarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "usuarios",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    keycloak_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    nombre_completo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    telefono = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    rol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sucursal_default_id = table.Column<int>(type: "integer", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ultimo_acceso = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.id);
                    table.ForeignKey(
                        name: "FK_usuarios_sucursales_sucursal_default_id",
                        column: x => x.sucursal_default_id,
                        principalSchema: "public",
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_email",
                schema: "public",
                table: "usuarios",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_keycloak_id",
                schema: "public",
                table: "usuarios",
                column: "keycloak_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_sucursal_default_id",
                schema: "public",
                table: "usuarios",
                column: "sucursal_default_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "usuarios",
                schema: "public");
        }
    }
}
