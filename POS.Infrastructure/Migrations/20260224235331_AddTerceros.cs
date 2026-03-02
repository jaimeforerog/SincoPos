using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTerceros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "terceros",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    tipo_identificacion = table.Column<int>(type: "integer", nullable: false),
                    identificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nombre = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    tipo_tercero = table.Column<int>(type: "integer", nullable: false),
                    telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    direccion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ciudad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    origen_datos = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    external_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_terceros", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_terceros_external_id",
                schema: "public",
                table: "terceros",
                column: "external_id",
                filter: "external_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_terceros_identificacion",
                schema: "public",
                table: "terceros",
                column: "identificacion",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "terceros",
                schema: "public");
        }
    }
}
