using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "categorias",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categorias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "productos",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_barras = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    categoria_id = table.Column<int>(type: "integer", nullable: true),
                    precio_venta = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    precio_costo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productos", x => x.id);
                    table.ForeignKey(
                        name: "FK_productos_categorias_categoria_id",
                        column: x => x.categoria_id,
                        principalSchema: "public",
                        principalTable: "categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_productos_categoria_id",
                schema: "public",
                table: "productos",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "ix_productos_codigo_barras",
                schema: "public",
                table: "productos",
                column: "codigo_barras",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "productos",
                schema: "public");

            migrationBuilder.DropTable(
                name: "categorias",
                schema: "public");
        }
    }
}
