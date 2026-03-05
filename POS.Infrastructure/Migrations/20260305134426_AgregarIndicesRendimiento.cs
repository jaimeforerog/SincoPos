using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarIndicesRendimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ventas_cliente_id",
                schema: "public",
                table: "ventas");

            migrationBuilder.RenameIndex(
                name: "IX_productos_categoria_id",
                schema: "public",
                table: "productos",
                newName: "ix_productos_categoria_id");

            migrationBuilder.RenameIndex(
                name: "IX_precios_sucursal_sucursal_id",
                schema: "public",
                table: "precios_sucursal",
                newName: "ix_precios_sucursal_sucursal_id");

            migrationBuilder.RenameIndex(
                name: "IX_detalle_ventas_venta_id",
                schema: "public",
                table: "detalle_ventas",
                newName: "ix_detalle_ventas_venta_id");

            migrationBuilder.RenameIndex(
                name: "IX_detalle_ventas_producto_id",
                schema: "public",
                table: "detalle_ventas",
                newName: "ix_detalle_ventas_producto_id");

            migrationBuilder.CreateIndex(
                name: "ix_ventas_cliente_id",
                schema: "public",
                table: "ventas",
                column: "cliente_id",
                filter: "cliente_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_ventas_estado",
                schema: "public",
                table: "ventas",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_traslados_origen_estado",
                schema: "public",
                table: "traslados",
                columns: new[] { "sucursal_origen_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_terceros_activo",
                schema: "public",
                table: "terceros",
                column: "activo",
                filter: "activo = true");

            migrationBuilder.CreateIndex(
                name: "ix_terceros_tipo_tercero",
                schema: "public",
                table: "terceros",
                column: "tipo_tercero");

            migrationBuilder.CreateIndex(
                name: "ix_productos_activo",
                schema: "public",
                table: "productos",
                column: "activo",
                filter: "activo = true");

            migrationBuilder.CreateIndex(
                name: "ix_lotes_disponibles",
                schema: "public",
                table: "lotes_inventario",
                columns: new[] { "producto_id", "sucursal_id", "cantidad_disponible" },
                filter: "cantidad_disponible > 0");

            migrationBuilder.CreateIndex(
                name: "ix_cajas_sucursal_estado",
                schema: "public",
                table: "cajas",
                columns: new[] { "sucursal_id", "estado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ventas_cliente_id",
                schema: "public",
                table: "ventas");

            migrationBuilder.DropIndex(
                name: "ix_ventas_estado",
                schema: "public",
                table: "ventas");

            migrationBuilder.DropIndex(
                name: "ix_traslados_origen_estado",
                schema: "public",
                table: "traslados");

            migrationBuilder.DropIndex(
                name: "ix_terceros_activo",
                schema: "public",
                table: "terceros");

            migrationBuilder.DropIndex(
                name: "ix_terceros_tipo_tercero",
                schema: "public",
                table: "terceros");

            migrationBuilder.DropIndex(
                name: "ix_productos_activo",
                schema: "public",
                table: "productos");

            migrationBuilder.DropIndex(
                name: "ix_lotes_disponibles",
                schema: "public",
                table: "lotes_inventario");

            migrationBuilder.DropIndex(
                name: "ix_cajas_sucursal_estado",
                schema: "public",
                table: "cajas");

            migrationBuilder.RenameIndex(
                name: "ix_productos_categoria_id",
                schema: "public",
                table: "productos",
                newName: "IX_productos_categoria_id");

            migrationBuilder.RenameIndex(
                name: "ix_precios_sucursal_sucursal_id",
                schema: "public",
                table: "precios_sucursal",
                newName: "IX_precios_sucursal_sucursal_id");

            migrationBuilder.RenameIndex(
                name: "ix_detalle_ventas_venta_id",
                schema: "public",
                table: "detalle_ventas",
                newName: "IX_detalle_ventas_venta_id");

            migrationBuilder.RenameIndex(
                name: "ix_detalle_ventas_producto_id",
                schema: "public",
                table: "detalle_ventas",
                newName: "IX_detalle_ventas_producto_id");

            migrationBuilder.CreateIndex(
                name: "IX_ventas_cliente_id",
                schema: "public",
                table: "ventas",
                column: "cliente_id");
        }
    }
}
