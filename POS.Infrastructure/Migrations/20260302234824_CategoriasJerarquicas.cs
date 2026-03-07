using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CategoriasJerarquicas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS public.detalle_agrupaciones CASCADE;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS public.agrupaciones CASCADE;");

            migrationBuilder.AddColumn<int>(
                name: "categoria_padre_id",
                schema: "public",
                table: "categorias",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "nivel",
                schema: "public",
                table: "categorias",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ruta_completa",
                schema: "public",
                table: "categorias",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_categorias_categoria_padre_id",
                schema: "public",
                table: "categorias",
                column: "categoria_padre_id");

            migrationBuilder.AddForeignKey(
                name: "FK_categorias_categorias_categoria_padre_id",
                schema: "public",
                table: "categorias",
                column: "categoria_padre_id",
                principalSchema: "public",
                principalTable: "categorias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_categorias_categorias_categoria_padre_id",
                schema: "public",
                table: "categorias");

            migrationBuilder.DropIndex(
                name: "ix_categorias_categoria_padre_id",
                schema: "public",
                table: "categorias");

            migrationBuilder.DropColumn(
                name: "categoria_padre_id",
                schema: "public",
                table: "categorias");

            migrationBuilder.DropColumn(
                name: "nivel",
                schema: "public",
                table: "categorias");

            migrationBuilder.DropColumn(
                name: "ruta_completa",
                schema: "public",
                table: "categorias");

            migrationBuilder.CreateTable(
                name: "agrupaciones",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fecha_actualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    precio_venta = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agrupaciones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "detalle_agrupaciones",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    agrupacion_id = table.Column<int>(type: "integer", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detalle_agrupaciones", x => x.id);
                    table.ForeignKey(
                        name: "FK_detalle_agrupaciones_agrupaciones_agrupacion_id",
                        column: x => x.agrupacion_id,
                        principalSchema: "public",
                        principalTable: "agrupaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_detalle_agrupaciones_productos_producto_id",
                        column: x => x.producto_id,
                        principalSchema: "public",
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agrupaciones_codigo",
                schema: "public",
                table: "agrupaciones",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_detalle_agrupaciones_agrupacion_id_producto_id",
                schema: "public",
                table: "detalle_agrupaciones",
                columns: new[] { "agrupacion_id", "producto_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_detalle_agrupaciones_producto_id",
                schema: "public",
                table: "detalle_agrupaciones",
                column: "producto_id");
        }
    }
}
