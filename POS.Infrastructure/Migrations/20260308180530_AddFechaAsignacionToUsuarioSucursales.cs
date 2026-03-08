using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFechaAsignacionToUsuarioSucursales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Columna puede existir si fue agregada manualmente antes de esta migración
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'usuario_sucursales'
                          AND column_name = 'fecha_asignacion'
                    ) THEN
                        ALTER TABLE public.usuario_sucursales
                            ADD COLUMN fecha_asignacion timestamp with time zone NOT NULL DEFAULT NOW();
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fecha_asignacion",
                schema: "public",
                table: "usuario_sucursales");
        }
    }
}
