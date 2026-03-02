using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "movimientos_inventario",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sucursal_id = table.Column<int>(type: "integer", nullable: false),
                    tipo_movimiento = table.Column<int>(type: "integer", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    costo_unitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    costo_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    referencia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    observaciones = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tercero_id = table.Column<int>(type: "integer", nullable: true),
                    sucursal_destino_id = table.Column<int>(type: "integer", nullable: true),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    fecha_movimiento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimientos_inventario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_movimientos_inventario_productos_producto_id",
                        column: x => x.producto_id,
                        principalSchema: "public",
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimientos_inventario_sucursales_sucursal_destino_id",
                        column: x => x.sucursal_destino_id,
                        principalSchema: "public",
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_movimientos_inventario_sucursales_sucursal_id",
                        column: x => x.sucursal_id,
                        principalSchema: "public",
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimientos_inventario_terceros_tercero_id",
                        column: x => x.tercero_id,
                        principalSchema: "public",
                        principalTable: "terceros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "stock",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sucursal_id = table.Column<int>(type: "integer", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    stock_minimo = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    costo_promedio = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ultima_actualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_productos_producto_id",
                        column: x => x.producto_id,
                        principalSchema: "public",
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_sucursales_sucursal_id",
                        column: x => x.sucursal_id,
                        principalSchema: "public",
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_inventario_sucursal_destino_id",
                schema: "public",
                table: "movimientos_inventario",
                column: "sucursal_destino_id");

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_inventario_sucursal_id",
                schema: "public",
                table: "movimientos_inventario",
                column: "sucursal_id");

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_inventario_tercero_id",
                schema: "public",
                table: "movimientos_inventario",
                column: "tercero_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimientos_producto_sucursal_fecha",
                schema: "public",
                table: "movimientos_inventario",
                columns: new[] { "producto_id", "sucursal_id", "fecha_movimiento" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_producto_sucursal",
                schema: "public",
                table: "stock",
                columns: new[] { "producto_id", "sucursal_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_sucursal_id",
                schema: "public",
                table: "stock",
                column: "sucursal_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "movimientos_inventario",
                schema: "public");

            migrationBuilder.DropTable(
                name: "stock",
                schema: "public");
        }
    }
}
