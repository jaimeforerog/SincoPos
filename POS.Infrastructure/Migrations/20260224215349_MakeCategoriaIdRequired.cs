using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeCategoriaIdRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Seed a default "General" category if none exist
            migrationBuilder.Sql(@"
                INSERT INTO public.categorias OVERRIDING SYSTEM VALUE
                VALUES (1, 'General', NULL, true)
                ON CONFLICT (""Id"") DO NOTHING;
            ");

            // Reset identity sequence to max Id + 1
            migrationBuilder.Sql(@"
                SELECT setval(pg_get_serial_sequence('public.categorias', 'Id'),
                    COALESCE((SELECT MAX(""Id"") FROM public.categorias), 0) + 1, false);
            ");

            // 2. Update any existing products with NULL or 0 categoria_id
            migrationBuilder.Sql(@"
                UPDATE public.productos SET categoria_id = 1 WHERE categoria_id IS NULL OR categoria_id = 0;
            ");

            // 3. Drop old FK
            migrationBuilder.DropForeignKey(
                name: "FK_productos_categorias_categoria_id",
                schema: "public",
                table: "productos");

            // 4. Make column non-nullable
            migrationBuilder.AlterColumn<int>(
                name: "categoria_id",
                schema: "public",
                table: "productos",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            // 5. Re-add FK with Restrict
            migrationBuilder.AddForeignKey(
                name: "FK_productos_categorias_categoria_id",
                schema: "public",
                table: "productos",
                column: "categoria_id",
                principalSchema: "public",
                principalTable: "categorias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_productos_categorias_categoria_id",
                schema: "public",
                table: "productos");

            migrationBuilder.AlterColumn<int>(
                name: "categoria_id",
                schema: "public",
                table: "productos",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_productos_categorias_categoria_id",
                schema: "public",
                table: "productos",
                column: "categoria_id",
                principalSchema: "public",
                principalTable: "categorias",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
