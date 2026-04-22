using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityLogArchivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activity_logs_archivo",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    usuario_id = table.Column<int>(type: "integer", nullable: true),
                    fecha_hora = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    accion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    sucursal_id = table.Column<int>(type: "integer", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tipo_entidad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    entidad_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    entidad_nombre = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    datos_anteriores = table.Column<string>(type: "jsonb", nullable: true),
                    datos_nuevos = table.Column<string>(type: "jsonb", nullable: true),
                    metadatos = table.Column<string>(type: "jsonb", nullable: true),
                    exitosa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    mensaje_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    fecha_archivado = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_logs_archivo", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_archivo_entidad",
                schema: "public",
                table: "activity_logs_archivo",
                columns: new[] { "tipo_entidad", "entidad_id" });

            migrationBuilder.CreateIndex(
                name: "idx_archivo_fecha",
                schema: "public",
                table: "activity_logs_archivo",
                column: "fecha_hora");

            migrationBuilder.CreateIndex(
                name: "idx_archivo_fecha_archivado",
                schema: "public",
                table: "activity_logs_archivo",
                column: "fecha_archivado");

            migrationBuilder.CreateIndex(
                name: "idx_archivo_usuario",
                schema: "public",
                table: "activity_logs_archivo",
                column: "usuario_email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_logs_archivo",
                schema: "public");
        }
    }
}
