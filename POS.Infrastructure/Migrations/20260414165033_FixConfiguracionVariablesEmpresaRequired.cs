using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixConfiguracionVariablesEmpresaRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Asignar empresa_id a los registros que quedaron con null (seed inicial)
            migrationBuilder.Sql(@"
                UPDATE public.configuracion_variables
                SET empresa_id = (SELECT ""Id"" FROM public.""Empresas"" ORDER BY ""Id"" LIMIT 1)
                WHERE empresa_id IS NULL;
            ");

            // En BD recién creada (CI) no existe ninguna empresa; el UPDATE devuelve NULL
            // y el AlterColumn SET NOT NULL fallaría. Eliminar esas filas: se recrearán
            // per-empresa por las migraciones AddDiaMaxVentaAtrazada y AddDiasMaxCompraAtrazada.
            // En producción el DELETE es un no-op (el UPDATE ya asignó empresa_id).
            migrationBuilder.Sql(@"
                DELETE FROM public.configuracion_variables WHERE empresa_id IS NULL;
            ");

            migrationBuilder.AlterColumn<int>(
                name: "empresa_id",
                schema: "public",
                table: "configuracion_variables",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "empresa_id",
                schema: "public",
                table: "configuracion_variables",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
