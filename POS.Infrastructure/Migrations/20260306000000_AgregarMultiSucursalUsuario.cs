using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMultiSucursalUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "usuario_sucursales",
                schema: "public",
                columns: table => new
                {
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    sucursal_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_sucursales", x => new { x.usuario_id, x.sucursal_id });
                    table.ForeignKey(
                        name: "fk_usuario_sucursales_sucursal",
                        column: x => x.sucursal_id,
                        principalSchema: "public",
                        principalTable: "sucursales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_usuario_sucursales_usuario",
                        column: x => x.usuario_id,
                        principalSchema: "public",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_usuario_sucursales_sucursal_id",
                schema: "public",
                table: "usuario_sucursales",
                column: "sucursal_id");

            // Migrar datos existentes: insertar la sucursal default actual de cada usuario
            migrationBuilder.Sql(@"
                INSERT INTO public.usuario_sucursales (usuario_id, sucursal_id)
                SELECT id, sucursal_default_id
                FROM public.usuarios
                WHERE sucursal_default_id IS NOT NULL
                ON CONFLICT DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "usuario_sucursales",
                schema: "public");
        }
    }
}
