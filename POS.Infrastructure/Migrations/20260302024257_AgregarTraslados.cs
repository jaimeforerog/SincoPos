using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTraslados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "traslados",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    numero_traslado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sucursal_origen_id = table.Column<int>(type: "integer", nullable: false),
                    sucursal_destino_id = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    fecha_traslado = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_envio = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_recepcion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    recibido_por_usuario_id = table.Column<int>(type: "integer", nullable: true),
                    observaciones = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    motivo_rechazo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreadoPor = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoPor = table.Column<string>(type: "text", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_traslados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_traslados_sucursales_sucursal_destino_id",
                        column: x => x.sucursal_destino_id,
                        principalSchema: "public",
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_traslados_sucursales_sucursal_origen_id",
                        column: x => x.sucursal_origen_id,
                        principalSchema: "public",
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "detalle_traslados",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    traslado_id = table.Column<int>(type: "integer", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_producto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cantidad_solicitada = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    cantidad_recibida = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    costo_unitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    costo_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    observaciones = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detalle_traslados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_detalle_traslados_productos_producto_id",
                        column: x => x.producto_id,
                        principalSchema: "public",
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_detalle_traslados_traslados_traslado_id",
                        column: x => x.traslado_id,
                        principalSchema: "public",
                        principalTable: "traslados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_detalle_traslados_producto_id",
                schema: "public",
                table: "detalle_traslados",
                column: "producto_id");

            migrationBuilder.CreateIndex(
                name: "IX_detalle_traslados_traslado_id",
                schema: "public",
                table: "detalle_traslados",
                column: "traslado_id");

            migrationBuilder.CreateIndex(
                name: "ix_traslados_destino_estado",
                schema: "public",
                table: "traslados",
                columns: new[] { "sucursal_destino_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_traslados_fecha",
                schema: "public",
                table: "traslados",
                column: "fecha_traslado");

            migrationBuilder.CreateIndex(
                name: "ix_traslados_numero",
                schema: "public",
                table: "traslados",
                column: "numero_traslado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_traslados_origen_fecha",
                schema: "public",
                table: "traslados",
                columns: new[] { "sucursal_origen_id", "fecha_traslado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "detalle_traslados",
                schema: "public");

            migrationBuilder.DropTable(
                name: "traslados",
                schema: "public");
        }
    }
}
