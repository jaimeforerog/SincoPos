using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSucursales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sucursales",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    direccion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ciudad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sucursales", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sucursales_nombre",
                schema: "public",
                table: "sucursales",
                column: "nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sucursales",
                schema: "public");
        }
    }
}
