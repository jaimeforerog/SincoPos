using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLotesInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lotes_inventario",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sucursal_id = table.Column<int>(type: "integer", nullable: false),
                    cantidad_inicial = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    cantidad_disponible = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    costo_unitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    referencia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tercero_id = table.Column<int>(type: "integer", nullable: true),
                    fecha_entrada = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lotes_inventario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lotes_inventario_productos_producto_id",
                        column: x => x.producto_id,
                        principalSchema: "public",
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lotes_inventario_sucursales_sucursal_id",
                        column: x => x.sucursal_id,
                        principalSchema: "public",
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lotes_inventario_terceros_tercero_id",
                        column: x => x.tercero_id,
                        principalSchema: "public",
                        principalTable: "terceros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lotes_inventario_sucursal_id",
                schema: "public",
                table: "lotes_inventario",
                column: "sucursal_id");

            migrationBuilder.CreateIndex(
                name: "IX_lotes_inventario_tercero_id",
                schema: "public",
                table: "lotes_inventario",
                column: "tercero_id");

            migrationBuilder.CreateIndex(
                name: "ix_lotes_producto_sucursal_fecha",
                schema: "public",
                table: "lotes_inventario",
                columns: new[] { "producto_id", "sucursal_id", "fecha_entrada" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lotes_inventario",
                schema: "public");
        }
    }
}
