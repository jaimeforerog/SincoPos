using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmpresaAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreadoPor",
                schema: "public",
                table: "Empresas",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ModificadoPor",
                schema: "public",
                table: "Empresas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaModificacion",
                schema: "public",
                table: "Empresas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaDesactivacion",
                schema: "public",
                table: "Empresas",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreadoPor",
                schema: "public",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "ModificadoPor",
                schema: "public",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "FechaModificacion",
                schema: "public",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "FechaDesactivacion",
                schema: "public",
                table: "Empresas");
        }
    }
}
