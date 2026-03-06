using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarFacturacionElectronica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "configuracion_emisor",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    sucursal_id = table.Column<int>(type: "integer", nullable: false),
                    nit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    digito_verificacion = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    razon_social = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    nombre_comercial = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    direccion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    codigo_municipio = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    codigo_departamento = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    codigo_ciiu = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    perfil_tributario = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "REGIMEN_ORDINARIO"),
                    numero_resolucion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fecha_resolucion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    prefijo = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false, defaultValue: "FV"),
                    numero_desde = table.Column<long>(type: "bigint", nullable: false),
                    numero_hasta = table.Column<long>(type: "bigint", nullable: false),
                    numero_actual = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    fecha_vigencia_desde = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_vigencia_hasta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ambiente = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false, defaultValue: "2"),
                    pin_software = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    id_software = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    certificado_base64 = table.Column<string>(type: "text", nullable: false),
                    certificado_password = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracion_emisor", x => x.id);
                    table.ForeignKey(
                        name: "fk_configuracion_emisor_sucursal",
                        column: x => x.sucursal_id,
                        principalSchema: "public",
                        principalTable: "sucursales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_configuracion_emisor_sucursal_id",
                schema: "public",
                table: "configuracion_emisor",
                column: "sucursal_id",
                unique: true);

            migrationBuilder.CreateTable(
                name: "documentos_electronicos",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    venta_id = table.Column<int>(type: "integer", nullable: true),
                    sucursal_id = table.Column<int>(type: "integer", nullable: false),
                    tipo_documento = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    prefijo = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    numero = table.Column<long>(type: "bigint", nullable: false),
                    numero_completo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cufe = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    fecha_emision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xml_ubl = table.Column<string>(type: "text", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    fecha_envio_dian = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    codigo_respuesta_dian = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    mensaje_respuesta_dian = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    intentos = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    creado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, defaultValue: "sistema"),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modificado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documentos_electronicos", x => x.id);
                    table.ForeignKey(
                        name: "fk_documentos_electronicos_venta",
                        column: x => x.venta_id,
                        principalSchema: "public",
                        principalTable: "ventas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_documentos_electronicos_sucursal",
                        column: x => x.sucursal_id,
                        principalSchema: "public",
                        principalTable: "sucursales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_documentos_electronicos_cufe",
                schema: "public",
                table: "documentos_electronicos",
                column: "cufe",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_documentos_electronicos_estado",
                schema: "public",
                table: "documentos_electronicos",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_electronicos_sucursal_fecha",
                schema: "public",
                table: "documentos_electronicos",
                columns: new[] { "sucursal_id", "fecha_emision" });

            migrationBuilder.CreateIndex(
                name: "ix_documentos_electronicos_venta_id",
                schema: "public",
                table: "documentos_electronicos",
                column: "venta_id",
                filter: "venta_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "documentos_electronicos", schema: "public");
            migrationBuilder.DropTable(name: "configuracion_emisor", schema: "public");
        }
    }
}
