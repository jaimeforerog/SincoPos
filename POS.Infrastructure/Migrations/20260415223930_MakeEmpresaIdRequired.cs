using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeEmpresaIdRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── PASO 1: Rellenar todos los EmpresaId nulos con la primera empresa ──────
            // Se usa un solo subquery reutilizable para asignar la empresa por defecto
            // a todos los registros que llegaron como "catálogo global" o sin empresa.
            const string primerEmpresa = @"(SELECT ""Id"" FROM public.""Empresas"" ORDER BY ""Id"" LIMIT 1)";

            // Tablas de catálogo
            migrationBuilder.Sql($@"UPDATE public.impuestos             SET ""EmpresaId"" = {primerEmpresa} WHERE ""EmpresaId"" IS NULL;");
            migrationBuilder.Sql($@"UPDATE public.retenciones_reglas    SET ""EmpresaId"" = {primerEmpresa} WHERE ""EmpresaId"" IS NULL;");
            migrationBuilder.Sql($@"UPDATE public.conceptos_retencion   SET ""EmpresaId"" = {primerEmpresa} WHERE ""EmpresaId"" IS NULL;");
            migrationBuilder.Sql($@"UPDATE public.categorias            SET ""EmpresaId"" = {primerEmpresa} WHERE ""EmpresaId"" IS NULL;");
            migrationBuilder.Sql($@"UPDATE public.productos             SET ""EmpresaId"" = {primerEmpresa} WHERE ""EmpresaId"" IS NULL;");
            migrationBuilder.Sql($@"UPDATE public.terceros              SET ""EmpresaId"" = {primerEmpresa} WHERE ""EmpresaId"" IS NULL;");
            migrationBuilder.Sql($@"UPDATE public.sucursales            SET ""EmpresaId"" = {primerEmpresa} WHERE ""EmpresaId"" IS NULL;");
            migrationBuilder.Sql($@"UPDATE public.cajas                 SET ""EmpresaId"" = {primerEmpresa} WHERE ""EmpresaId"" IS NULL;");
            migrationBuilder.Sql($@"UPDATE public.""ReglasEticas""      SET ""EmpresaId"" = {primerEmpresa} WHERE ""EmpresaId"" IS NULL;");

            // Tablas transaccionales
            migrationBuilder.Sql($@"UPDATE public.ventas                SET ""EmpresaId"" = {primerEmpresa} WHERE ""EmpresaId"" IS NULL;");
            migrationBuilder.Sql($@"UPDATE public.devoluciones_venta    SET ""EmpresaId"" = {primerEmpresa} WHERE ""EmpresaId"" IS NULL;");
            migrationBuilder.Sql($@"UPDATE public.traslados             SET ""EmpresaId"" = {primerEmpresa} WHERE ""EmpresaId"" IS NULL;");
            migrationBuilder.Sql($@"UPDATE public.ordenes_compra        SET ""EmpresaId"" = {primerEmpresa} WHERE ""EmpresaId"" IS NULL;");
            migrationBuilder.Sql($@"UPDATE public.documentos_electronicos SET ""EmpresaId"" = {primerEmpresa} WHERE ""EmpresaId"" IS NULL;");

            // ── PASO 2: Ajustar FK de sucursales (ahora requerido) ────────────────────
            migrationBuilder.DropForeignKey(
                name: "FK_sucursales_Empresas_EmpresaId",
                schema: "public",
                table: "sucursales");

            // ── PASO 3: Alterar columnas a NOT NULL ───────────────────────────────────
            // Todos los nulls fueron rellenados en el paso 1; no se usa defaultValue.

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "ventas",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "traslados",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "terceros",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "sucursales",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "retenciones_reglas",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "ReglasEticas",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "productos",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "ordenes_compra",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "impuestos",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "documentos_electronicos",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "devoluciones_venta",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "conceptos_retencion",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "categorias",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "cajas",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            // ── PASO 4: FK sucursales ahora CASCADE (empresa siempre requerida) ────────
            migrationBuilder.AddForeignKey(
                name: "FK_sucursales_Empresas_EmpresaId",
                schema: "public",
                table: "sucursales",
                column: "EmpresaId",
                principalSchema: "public",
                principalTable: "Empresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sucursales_Empresas_EmpresaId",
                schema: "public",
                table: "sucursales");

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "ventas",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "traslados",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "terceros",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "sucursales",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "retenciones_reglas",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "ReglasEticas",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "productos",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "ordenes_compra",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "impuestos",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "documentos_electronicos",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "devoluciones_venta",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "conceptos_retencion",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "categorias",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "cajas",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_sucursales_Empresas_EmpresaId",
                schema: "public",
                table: "sucursales",
                column: "EmpresaId",
                principalSchema: "public",
                principalTable: "Empresas",
                principalColumn: "Id");
        }
    }
}
