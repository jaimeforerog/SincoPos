using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmpresaIdTransactional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "ventas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "traslados",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "ordenes_compra",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "documentos_electronicos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "devoluciones_venta",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                schema: "public",
                table: "cajas",
                type: "integer",
                nullable: true);

            // Backfill: propagar EmpresaId desde la sucursal correspondiente
            migrationBuilder.Sql(@"
                UPDATE public.ventas v
                SET ""EmpresaId"" = s.""EmpresaId""
                FROM public.sucursales s
                WHERE v.""SucursalId"" = s.""Id"" AND s.""EmpresaId"" IS NOT NULL;

                UPDATE public.cajas c
                SET ""EmpresaId"" = s.""EmpresaId""
                FROM public.sucursales s
                WHERE c.""SucursalId"" = s.""Id"" AND s.""EmpresaId"" IS NOT NULL;

                UPDATE public.ordenes_compra o
                SET ""EmpresaId"" = s.""EmpresaId""
                FROM public.sucursales s
                WHERE o.""SucursalId"" = s.""Id"" AND s.""EmpresaId"" IS NOT NULL;

                UPDATE public.traslados t
                SET ""EmpresaId"" = s.""EmpresaId""
                FROM public.sucursales s
                WHERE t.""SucursalOrigenId"" = s.""Id"" AND s.""EmpresaId"" IS NOT NULL;

                UPDATE public.documentos_electronicos d
                SET ""EmpresaId"" = s.""EmpresaId""
                FROM public.sucursales s
                WHERE d.""SucursalId"" = s.""Id"" AND s.""EmpresaId"" IS NOT NULL;

                UPDATE public.devoluciones_venta dv
                SET ""EmpresaId"" = v.""EmpresaId""
                FROM public.ventas v
                WHERE dv.""VentaId"" = v.""Id"" AND v.""EmpresaId"" IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmpresaId",
                schema: "public",
                table: "ventas");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                schema: "public",
                table: "traslados");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                schema: "public",
                table: "ordenes_compra");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                schema: "public",
                table: "documentos_electronicos");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                schema: "public",
                table: "devoluciones_venta");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                schema: "public",
                table: "cajas");
        }
    }
}
