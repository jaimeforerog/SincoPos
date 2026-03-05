using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTaxEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiereFacturaElectronica",
                schema: "public",
                table: "ventas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PerfilTributario",
                schema: "public",
                table: "terceros",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CodigoMunicipio",
                schema: "public",
                table: "sucursales",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PerfilTributario",
                schema: "public",
                table: "sucursales",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ValorUVT",
                schema: "public",
                table: "sucursales",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "EsAlimentoUltraprocesado",
                schema: "public",
                table: "productos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "GramosAzucarPor100ml",
                schema: "public",
                table: "productos",
                type: "numeric",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Porcentaje",
                schema: "public",
                table: "impuestos",
                type: "numeric(8,4)",
                precision: 8,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,4)",
                oldPrecision: 5,
                oldScale: 4);

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                schema: "public",
                table: "impuestos",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<bool>(
                name: "AplicaSobreBase",
                schema: "public",
                table: "impuestos",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "CodigoCuentaContable",
                schema: "public",
                table: "impuestos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodigoPais",
                schema: "public",
                table: "impuestos",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "CO");

            migrationBuilder.AddColumn<string>(
                name: "Descripcion",
                schema: "public",
                table: "impuestos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Tipo",
                schema: "public",
                table: "impuestos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorFijo",
                schema: "public",
                table: "impuestos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "retenciones_reglas",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Porcentaje = table.Column<decimal>(type: "numeric(8,6)", precision: 8, scale: 6, nullable: false),
                    BaseMinUVT = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false, defaultValue: 4m),
                    CodigoMunicipio = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    PerfilVendedor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PerfilComprador = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CodigoCuentaContable = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreadoPor = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoPor = table.Column<string>(type: "text", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retenciones_reglas", x => x.Id);
                });

            migrationBuilder.UpdateData(
                schema: "public",
                table: "impuestos",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AplicaSobreBase", "CodigoCuentaContable", "CodigoPais", "Descripcion", "ValorFijo" },
                values: new object[] { true, "2408", "CO", "Bienes y servicios exentos de IVA", null });

            migrationBuilder.UpdateData(
                schema: "public",
                table: "impuestos",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "AplicaSobreBase", "CodigoCuentaContable", "CodigoPais", "Descripcion", "ValorFijo" },
                values: new object[] { true, "2408", "CO", "IVA tarifa diferencial 5%", null });

            migrationBuilder.UpdateData(
                schema: "public",
                table: "impuestos",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "AplicaSobreBase", "CodigoCuentaContable", "CodigoPais", "Descripcion", "ValorFijo" },
                values: new object[] { true, "2408", "CO", "IVA tarifa general 19%", null });

            migrationBuilder.Sql(@"
                INSERT INTO public.impuestos (""Id"", ""Activo"", ""CodigoCuentaContable"", ""CodigoPais"", ""CreadoPor"", ""Descripcion"", ""FechaCreacion"", ""FechaModificacion"", ""ModificadoPor"", ""Nombre"", ""Porcentaje"", ""Tipo"", ""ValorFijo"", ""AplicaSobreBase"")
                VALUES
                    (4, true, '2412', 'CO', '', 'INC restaurantes, bares, cafeterías (Art. 512-1 ET)', '2026-01-01T00:00:00Z', null, null, 'INC 8%', 0.08, 1, null, false),
                    (5, true, '2424', 'CO', '', 'Impuesto bolsas plásticas 2026 (Ley 1819/2016)', '2026-01-01T00:00:00Z', null, null, 'Bolsa $66', 0.00, 3, 66, false)
                ON CONFLICT (""Id"") DO UPDATE SET
                    ""Tipo"" = EXCLUDED.""Tipo"",
                    ""CodigoCuentaContable"" = EXCLUDED.""CodigoCuentaContable"",
                    ""CodigoPais"" = EXCLUDED.""CodigoPais"",
                    ""Descripcion"" = EXCLUDED.""Descripcion"",
                    ""AplicaSobreBase"" = EXCLUDED.""AplicaSobreBase"";
            ");

            migrationBuilder.InsertData(
                schema: "public",
                table: "retenciones_reglas",
                columns: new[] { "Id", "Activo", "BaseMinUVT", "CodigoCuentaContable", "CodigoMunicipio", "CreadoPor", "FechaCreacion", "FechaModificacion", "ModificadoPor", "Nombre", "PerfilComprador", "PerfilVendedor", "Porcentaje", "Tipo" },
                values: new object[] { 1, true, 4m, "1355", null, "", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "ReteFuente Compras 2.5%", "GRAN_CONTRIBUYENTE", "REGIMEN_ORDINARIO", 0.025m, 0 });

            migrationBuilder.InsertData(
                schema: "public",
                table: "retenciones_reglas",
                columns: new[] { "Id", "CodigoCuentaContable", "CodigoMunicipio", "CreadoPor", "FechaCreacion", "FechaModificacion", "ModificadoPor", "Nombre", "PerfilComprador", "PerfilVendedor", "Porcentaje", "Tipo" },
                values: new object[] { 2, "1355", null, "", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "ReteFuente Honorarios 11%", "GRAN_CONTRIBUYENTE", "REGIMEN_ORDINARIO", 0.11m, 0 });

            migrationBuilder.InsertData(
                schema: "public",
                table: "retenciones_reglas",
                columns: new[] { "Id", "Activo", "CodigoCuentaContable", "CodigoMunicipio", "CreadoPor", "FechaCreacion", "FechaModificacion", "ModificadoPor", "Nombre", "PerfilComprador", "PerfilVendedor", "Porcentaje", "Tipo" },
                values: new object[] { 3, true, "1356", "11001", "", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "ReteICA Bogotá 0.966%", "GRAN_CONTRIBUYENTE", "REGIMEN_ORDINARIO", 0.00966m, 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "retenciones_reglas",
                schema: "public");

            migrationBuilder.DeleteData(
                schema: "public",
                table: "impuestos",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "impuestos",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DropColumn(
                name: "RequiereFacturaElectronica",
                schema: "public",
                table: "ventas");

            migrationBuilder.DropColumn(
                name: "PerfilTributario",
                schema: "public",
                table: "terceros");

            migrationBuilder.DropColumn(
                name: "CodigoMunicipio",
                schema: "public",
                table: "sucursales");

            migrationBuilder.DropColumn(
                name: "PerfilTributario",
                schema: "public",
                table: "sucursales");

            migrationBuilder.DropColumn(
                name: "ValorUVT",
                schema: "public",
                table: "sucursales");

            migrationBuilder.DropColumn(
                name: "EsAlimentoUltraprocesado",
                schema: "public",
                table: "productos");

            migrationBuilder.DropColumn(
                name: "GramosAzucarPor100ml",
                schema: "public",
                table: "productos");

            migrationBuilder.DropColumn(
                name: "AplicaSobreBase",
                schema: "public",
                table: "impuestos");

            migrationBuilder.DropColumn(
                name: "CodigoCuentaContable",
                schema: "public",
                table: "impuestos");

            migrationBuilder.DropColumn(
                name: "CodigoPais",
                schema: "public",
                table: "impuestos");

            migrationBuilder.DropColumn(
                name: "Descripcion",
                schema: "public",
                table: "impuestos");

            migrationBuilder.DropColumn(
                name: "Tipo",
                schema: "public",
                table: "impuestos");

            migrationBuilder.DropColumn(
                name: "ValorFijo",
                schema: "public",
                table: "impuestos");

            migrationBuilder.AlterColumn<decimal>(
                name: "Porcentaje",
                schema: "public",
                table: "impuestos",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,4)",
                oldPrecision: 8,
                oldScale: 4);

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                schema: "public",
                table: "impuestos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);
        }
    }
}
