using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCamposFiscalesTerceros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PerfilTributario",
                schema: "public",
                table: "terceros",
                newName: "perfil_tributario");

            migrationBuilder.AlterColumn<string>(
                name: "perfil_tributario",
                schema: "public",
                table: "terceros",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "REGIMEN_COMUN",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "codigo_departamento",
                schema: "public",
                table: "terceros",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "codigo_municipio",
                schema: "public",
                table: "terceros",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "digito_verificacion",
                schema: "public",
                table: "terceros",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "es_autorretenedor",
                schema: "public",
                table: "terceros",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "es_gran_contribuyente",
                schema: "public",
                table: "terceros",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "es_responsable_iva",
                schema: "public",
                table: "terceros",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "tercero_actividades",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    tercero_id = table.Column<int>(type: "integer", nullable: false),
                    codigo_ciiu = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    es_principal = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tercero_actividades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tercero_actividades_terceros_tercero_id",
                        column: x => x.tercero_id,
                        principalSchema: "public",
                        principalTable: "terceros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tercero_actividades_tercero_ciiu",
                schema: "public",
                table: "tercero_actividades",
                columns: new[] { "tercero_id", "codigo_ciiu" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tercero_actividades",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "codigo_departamento",
                schema: "public",
                table: "terceros");

            migrationBuilder.DropColumn(
                name: "codigo_municipio",
                schema: "public",
                table: "terceros");

            migrationBuilder.DropColumn(
                name: "digito_verificacion",
                schema: "public",
                table: "terceros");

            migrationBuilder.DropColumn(
                name: "es_autorretenedor",
                schema: "public",
                table: "terceros");

            migrationBuilder.DropColumn(
                name: "es_gran_contribuyente",
                schema: "public",
                table: "terceros");

            migrationBuilder.DropColumn(
                name: "es_responsable_iva",
                schema: "public",
                table: "terceros");

            migrationBuilder.RenameColumn(
                name: "perfil_tributario",
                schema: "public",
                table: "terceros",
                newName: "PerfilTributario");

            migrationBuilder.AlterColumn<string>(
                name: "PerfilTributario",
                schema: "public",
                table: "terceros",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "REGIMEN_COMUN");
        }
    }
}
