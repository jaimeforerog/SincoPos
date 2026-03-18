using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "terceros",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "sucursales",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "productos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "categorias",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Empresas",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Nit = table.Column<string>(type: "text", nullable: true),
                    RazonSocial = table.Column<string>(type: "text", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empresas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sucursales_EmpresaId",
                schema: "public",
                table: "sucursales",
                column: "EmpresaId");

            migrationBuilder.AddForeignKey(
                name: "FK_sucursales_Empresas_EmpresaId",
                schema: "public",
                table: "sucursales",
                column: "EmpresaId",
                principalSchema: "public",
                principalTable: "Empresas",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sucursales_Empresas_EmpresaId",
                schema: "public",
                table: "sucursales");

            migrationBuilder.DropTable(
                name: "Empresas",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_sucursales_EmpresaId",
                schema: "public",
                table: "sucursales");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                schema: "public",
                table: "terceros");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                schema: "public",
                table: "sucursales");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                schema: "public",
                table: "productos");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                schema: "public",
                table: "categorias");
        }
    }
}
