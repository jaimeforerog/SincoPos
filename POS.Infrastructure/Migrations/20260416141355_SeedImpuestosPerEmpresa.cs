using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedImpuestosPerEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed todos los impuestos estándar por empresa.
            // Los registros globales originales (IDs 1-5) quedaron asignados solo a la
            // primera empresa en MakeEmpresaIdRequired. Esta migración garantiza que
            // TODAS las empresas tengan los impuestos base. NOT EXISTS evita duplicados.
            migrationBuilder.Sql(@"
                INSERT INTO public.impuestos
                    (""Nombre"", ""Tipo"", ""Porcentaje"", ""ValorFijo"", ""Activo"",
                     ""AplicaSobreBase"", ""CodigoPais"", ""CodigoCuentaContable"",
                     ""Descripcion"", ""FechaCreacion"", ""CreadoPor"", ""EmpresaId"")
                SELECT
                    imp.nombre,
                    imp.tipo,
                    imp.porcentaje,
                    imp.valor_fijo,
                    true,
                    imp.aplica_sobre_base,
                    'CO',
                    imp.cuenta_contable,
                    imp.descripcion,
                    '2026-01-01 00:00:00'::timestamp,
                    'sistema',
                    e.""Id""
                FROM public.""Empresas"" e
                CROSS JOIN (VALUES
                    ('IVA 5%',     0::int, 0.05::numeric, NULL::numeric,  true::bool,  '2408', 'IVA tarifa diferencial 5%'),
                    ('IVA 19%',    0::int, 0.19::numeric, NULL::numeric,  true::bool,  '2408', 'IVA tarifa general 19%'),
                    ('INC 8%',     1::int, 0.08::numeric, NULL::numeric,  false::bool, '2412', 'INC restaurantes, bares, cafeterías (Art. 512-1 ET)'),
                    ('Bolsa $66',  3::int, 0.00::numeric, 66.00::numeric, false::bool, '2424', 'Impuesto bolsas plásticas 2026 (Ley 1819/2016)')
                ) AS imp(nombre, tipo, porcentaje, valor_fijo, aplica_sobre_base, cuenta_contable, descripcion)
                WHERE NOT EXISTS (
                    SELECT 1 FROM public.impuestos i
                    WHERE i.""EmpresaId"" = e.""Id""
                      AND i.""Nombre"" = imp.nombre
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM public.impuestos
                WHERE ""Nombre"" IN ('IVA 5%', 'IVA 19%', 'INC 8%', 'Bolsa $66')
                  AND ""Id"" NOT IN (1, 2, 3, 4, 5);
            ");
        }
    }
}
