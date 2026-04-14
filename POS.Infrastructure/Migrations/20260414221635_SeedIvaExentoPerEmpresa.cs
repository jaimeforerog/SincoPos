using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedIvaExentoPerEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed: un registro "Exento 0%" de IVA por cada empresa.
            // Los registros seeded con HasData tienen EmpresaId = null y el filtro
            // global de la entidad Impuesto requiere EmpresaId == _empresaProvider.EmpresaId,
            // por lo que no son visibles para usuarios ligados a una empresa concreta.
            // Este script crea la variante per-empresa para que aparezca en los dropdowns.
            migrationBuilder.Sql(@"
                INSERT INTO public.impuestos
                    (""Nombre"", ""Tipo"", ""Porcentaje"", ""ValorFijo"", ""Activo"",
                     ""AplicaSobreBase"", ""CodigoPais"", ""CodigoCuentaContable"",
                     ""Descripcion"", ""FechaCreacion"", ""EmpresaId"")
                SELECT
                    'Exento 0%',
                    0,
                    0.00,
                    NULL,
                    true,
                    true,
                    'CO',
                    '2408',
                    'Bienes y servicios exentos de IVA',
                    '2026-01-01 00:00:00'::timestamp,
                    e.""Id""
                FROM public.""Empresas"" e
                WHERE NOT EXISTS (
                    SELECT 1 FROM public.impuestos i
                    WHERE i.""EmpresaId"" = e.""Id""
                      AND i.""Nombre"" = 'Exento 0%'
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM public.impuestos
                WHERE ""Nombre"" = 'Exento 0%'
                  AND ""EmpresaId"" IS NOT NULL;
            ");
        }
    }
}
