using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueEmailIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: Remove duplicates first (keep the oldest record per email),
            // then drop the old non-unique index if it exists, and create a unique one.
            migrationBuilder.Sql(@"
                -- Remove duplicate emails keeping the record with the smallest id
                DELETE FROM public.usuarios
                WHERE id NOT IN (
                    SELECT MIN(id) FROM public.usuarios GROUP BY LOWER(email)
                );

                -- Drop the old non-unique index if it exists
                DROP INDEX IF EXISTS public.ix_usuarios_email;

                -- Create the unique index if it does not already exist
                CREATE UNIQUE INDEX IF NOT EXISTS ix_usuarios_email
                    ON public.usuarios (email);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS public.ix_usuarios_email;

                CREATE INDEX IF NOT EXISTS ix_usuarios_email
                    ON public.usuarios (email);
            ");
        }
    }
}
