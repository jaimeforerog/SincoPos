using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmpresaIdToImpuestos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "retenciones_reglas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "impuestos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "conceptos_retencion",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "conceptos_retencion",
                keyColumn: "Id",
                keyValue: 1,
                column: "EmpresaId",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "conceptos_retencion",
                keyColumn: "Id",
                keyValue: 2,
                column: "EmpresaId",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "conceptos_retencion",
                keyColumn: "Id",
                keyValue: 3,
                column: "EmpresaId",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "conceptos_retencion",
                keyColumn: "Id",
                keyValue: 4,
                column: "EmpresaId",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "conceptos_retencion",
                keyColumn: "Id",
                keyValue: 5,
                column: "EmpresaId",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "impuestos",
                keyColumn: "Id",
                keyValue: 1,
                column: "EmpresaId",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "impuestos",
                keyColumn: "Id",
                keyValue: 2,
                column: "EmpresaId",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "impuestos",
                keyColumn: "Id",
                keyValue: 3,
                column: "EmpresaId",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "impuestos",
                keyColumn: "Id",
                keyValue: 4,
                column: "EmpresaId",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "impuestos",
                keyColumn: "Id",
                keyValue: 5,
                column: "EmpresaId",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "retenciones_reglas",
                keyColumn: "Id",
                keyValue: 1,
                column: "EmpresaId",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "retenciones_reglas",
                keyColumn: "Id",
                keyValue: 2,
                column: "EmpresaId",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "retenciones_reglas",
                keyColumn: "Id",
                keyValue: 3,
                column: "EmpresaId",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmpresaId",
                schema: "public",
                table: "retenciones_reglas");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                schema: "public",
                table: "impuestos");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                schema: "public",
                table: "conceptos_retencion");
        }
    }
}
