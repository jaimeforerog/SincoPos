using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "fecha_actualizacion",
                schema: "public",
                table: "precios_sucursal",
                newName: "FechaCreacion");

            migrationBuilder.AddColumn<bool>(
                name: "Activo",
                schema: "public",
                table: "ventas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CreadoPor",
                schema: "public",
                table: "ventas",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCreacion",
                schema: "public",
                table: "ventas",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaModificacion",
                schema: "public",
                table: "ventas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModificadoPor",
                schema: "public",
                table: "ventas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreadoPor",
                schema: "public",
                table: "usuarios",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ModificadoPor",
                schema: "public",
                table: "usuarios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreadoPor",
                schema: "public",
                table: "terceros",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ModificadoPor",
                schema: "public",
                table: "terceros",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreadoPor",
                schema: "public",
                table: "sucursales",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaModificacion",
                schema: "public",
                table: "sucursales",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModificadoPor",
                schema: "public",
                table: "sucursales",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Activo",
                schema: "public",
                table: "stock",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CreadoPor",
                schema: "public",
                table: "stock",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCreacion",
                schema: "public",
                table: "stock",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaModificacion",
                schema: "public",
                table: "stock",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModificadoPor",
                schema: "public",
                table: "stock",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreadoPor",
                schema: "public",
                table: "productos",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ModificadoPor",
                schema: "public",
                table: "productos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Activo",
                schema: "public",
                table: "precios_sucursal",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CreadoPor",
                schema: "public",
                table: "precios_sucursal",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaModificacion",
                schema: "public",
                table: "precios_sucursal",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModificadoPor",
                schema: "public",
                table: "precios_sucursal",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Activo",
                schema: "public",
                table: "lotes_inventario",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CreadoPor",
                schema: "public",
                table: "lotes_inventario",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCreacion",
                schema: "public",
                table: "lotes_inventario",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaModificacion",
                schema: "public",
                table: "lotes_inventario",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModificadoPor",
                schema: "public",
                table: "lotes_inventario",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreadoPor",
                schema: "public",
                table: "impuestos",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaModificacion",
                schema: "public",
                table: "impuestos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModificadoPor",
                schema: "public",
                table: "impuestos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreadoPor",
                schema: "public",
                table: "categorias",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCreacion",
                schema: "public",
                table: "categorias",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaModificacion",
                schema: "public",
                table: "categorias",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModificadoPor",
                schema: "public",
                table: "categorias",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreadoPor",
                schema: "public",
                table: "cajas",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaModificacion",
                schema: "public",
                table: "cajas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModificadoPor",
                schema: "public",
                table: "cajas",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "impuestos",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreadoPor", "FechaModificacion", "ModificadoPor" },
                values: new object[] { "", null, null });

            migrationBuilder.UpdateData(
                schema: "public",
                table: "impuestos",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreadoPor", "FechaModificacion", "ModificadoPor" },
                values: new object[] { "", null, null });

            migrationBuilder.UpdateData(
                schema: "public",
                table: "impuestos",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreadoPor", "FechaModificacion", "ModificadoPor" },
                values: new object[] { "", null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Activo",
                schema: "public",
                table: "ventas");

            migrationBuilder.DropColumn(
                name: "CreadoPor",
                schema: "public",
                table: "ventas");

            migrationBuilder.DropColumn(
                name: "FechaCreacion",
                schema: "public",
                table: "ventas");

            migrationBuilder.DropColumn(
                name: "FechaModificacion",
                schema: "public",
                table: "ventas");

            migrationBuilder.DropColumn(
                name: "ModificadoPor",
                schema: "public",
                table: "ventas");

            migrationBuilder.DropColumn(
                name: "CreadoPor",
                schema: "public",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "ModificadoPor",
                schema: "public",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "CreadoPor",
                schema: "public",
                table: "terceros");

            migrationBuilder.DropColumn(
                name: "ModificadoPor",
                schema: "public",
                table: "terceros");

            migrationBuilder.DropColumn(
                name: "CreadoPor",
                schema: "public",
                table: "sucursales");

            migrationBuilder.DropColumn(
                name: "FechaModificacion",
                schema: "public",
                table: "sucursales");

            migrationBuilder.DropColumn(
                name: "ModificadoPor",
                schema: "public",
                table: "sucursales");

            migrationBuilder.DropColumn(
                name: "Activo",
                schema: "public",
                table: "stock");

            migrationBuilder.DropColumn(
                name: "CreadoPor",
                schema: "public",
                table: "stock");

            migrationBuilder.DropColumn(
                name: "FechaCreacion",
                schema: "public",
                table: "stock");

            migrationBuilder.DropColumn(
                name: "FechaModificacion",
                schema: "public",
                table: "stock");

            migrationBuilder.DropColumn(
                name: "ModificadoPor",
                schema: "public",
                table: "stock");

            migrationBuilder.DropColumn(
                name: "CreadoPor",
                schema: "public",
                table: "productos");

            migrationBuilder.DropColumn(
                name: "ModificadoPor",
                schema: "public",
                table: "productos");

            migrationBuilder.DropColumn(
                name: "Activo",
                schema: "public",
                table: "precios_sucursal");

            migrationBuilder.DropColumn(
                name: "CreadoPor",
                schema: "public",
                table: "precios_sucursal");

            migrationBuilder.DropColumn(
                name: "FechaModificacion",
                schema: "public",
                table: "precios_sucursal");

            migrationBuilder.DropColumn(
                name: "ModificadoPor",
                schema: "public",
                table: "precios_sucursal");

            migrationBuilder.DropColumn(
                name: "Activo",
                schema: "public",
                table: "lotes_inventario");

            migrationBuilder.DropColumn(
                name: "CreadoPor",
                schema: "public",
                table: "lotes_inventario");

            migrationBuilder.DropColumn(
                name: "FechaCreacion",
                schema: "public",
                table: "lotes_inventario");

            migrationBuilder.DropColumn(
                name: "FechaModificacion",
                schema: "public",
                table: "lotes_inventario");

            migrationBuilder.DropColumn(
                name: "ModificadoPor",
                schema: "public",
                table: "lotes_inventario");

            migrationBuilder.DropColumn(
                name: "CreadoPor",
                schema: "public",
                table: "impuestos");

            migrationBuilder.DropColumn(
                name: "FechaModificacion",
                schema: "public",
                table: "impuestos");

            migrationBuilder.DropColumn(
                name: "ModificadoPor",
                schema: "public",
                table: "impuestos");

            migrationBuilder.DropColumn(
                name: "CreadoPor",
                schema: "public",
                table: "categorias");

            migrationBuilder.DropColumn(
                name: "FechaCreacion",
                schema: "public",
                table: "categorias");

            migrationBuilder.DropColumn(
                name: "FechaModificacion",
                schema: "public",
                table: "categorias");

            migrationBuilder.DropColumn(
                name: "ModificadoPor",
                schema: "public",
                table: "categorias");

            migrationBuilder.DropColumn(
                name: "CreadoPor",
                schema: "public",
                table: "cajas");

            migrationBuilder.DropColumn(
                name: "FechaModificacion",
                schema: "public",
                table: "cajas");

            migrationBuilder.DropColumn(
                name: "ModificadoPor",
                schema: "public",
                table: "cajas");

            migrationBuilder.RenameColumn(
                name: "FechaCreacion",
                schema: "public",
                table: "precios_sucursal",
                newName: "fecha_actualizacion");
        }
    }
}
