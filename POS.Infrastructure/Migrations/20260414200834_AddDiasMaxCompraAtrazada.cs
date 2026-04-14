using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDiasMaxCompraAtrazada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed: variable que controla hasta cuántos días atrás se puede registrar
            // la fecha de una orden de compra.
            // 0 = el campo de fecha no se muestra y se usa la fecha/hora actual del servidor.
            // N = se muestra el campo y se permite una fecha con hasta N días de atraso.
            migrationBuilder.Sql(@"
                INSERT INTO public.configuracion_variables
                    (nombre, valor, descripcion, empresa_id, activo, creado_por, fecha_creacion)
                SELECT
                    'DiasMax_CompraAtrazada',
                    '0',
                    'Cantidad máxima de días hacia atrás permitidos al registrar la fecha de una orden de compra. ' ||
                    '0 = el campo de fecha no se muestra y se usa la fecha/hora actual del servidor. ' ||
                    'Un valor positivo habilita el campo y restringe la fecha mínima seleccionable.',
                    ""Id"",
                    true,
                    'system',
                    NOW()
                FROM public.""Empresas""
                ORDER BY ""Id""
                LIMIT 1
                ON CONFLICT DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM public.configuracion_variables
                WHERE nombre = 'DiasMax_CompraAtrazada';
            ");
        }
    }
}
