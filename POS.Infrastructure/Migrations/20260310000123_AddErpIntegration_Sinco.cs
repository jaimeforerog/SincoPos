using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddErpIntegration_Sinco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErpReferencia",
                schema: "public",
                table: "ordenes_compra",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorSincronizacion",
                schema: "public",
                table: "ordenes_compra",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaSincronizacionErp",
                schema: "public",
                table: "ordenes_compra",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SincronizadoErp",
                schema: "public",
                table: "ordenes_compra",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "cuenta_costo",
                schema: "public",
                table: "categorias",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cuenta_ingreso",
                schema: "public",
                table: "categorias",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cuenta_inventario",
                schema: "public",
                table: "categorias",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_id",
                schema: "public",
                table: "categorias",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "origen_datos",
                schema: "public",
                table: "categorias",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Local");

            migrationBuilder.CreateTable(
                name: "erp_outbox_messages",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    tipo_documento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entidad_id = table.Column<int>(type: "integer", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_procesamiento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    intentos = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ultimo_error = table.Column<string>(type: "text", nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_erp_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_erp_outbox_estado_fecha",
                schema: "public",
                table: "erp_outbox_messages",
                columns: new[] { "estado", "fecha_creacion" });

            migrationBuilder.CreateIndex(
                name: "ix_erp_outbox_tipo_entidad",
                schema: "public",
                table: "erp_outbox_messages",
                columns: new[] { "tipo_documento", "entidad_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "erp_outbox_messages",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "ErpReferencia",
                schema: "public",
                table: "ordenes_compra");

            migrationBuilder.DropColumn(
                name: "ErrorSincronizacion",
                schema: "public",
                table: "ordenes_compra");

            migrationBuilder.DropColumn(
                name: "FechaSincronizacionErp",
                schema: "public",
                table: "ordenes_compra");

            migrationBuilder.DropColumn(
                name: "SincronizadoErp",
                schema: "public",
                table: "ordenes_compra");

            migrationBuilder.DropColumn(
                name: "cuenta_costo",
                schema: "public",
                table: "categorias");

            migrationBuilder.DropColumn(
                name: "cuenta_ingreso",
                schema: "public",
                table: "categorias");

            migrationBuilder.DropColumn(
                name: "cuenta_inventario",
                schema: "public",
                table: "categorias");

            migrationBuilder.DropColumn(
                name: "external_id",
                schema: "public",
                table: "categorias");

            migrationBuilder.DropColumn(
                name: "origen_datos",
                schema: "public",
                table: "categorias");
        }
    }
}
