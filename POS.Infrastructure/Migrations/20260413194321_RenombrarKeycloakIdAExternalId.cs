using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenombrarKeycloakIdAExternalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "keycloak_id",
                schema: "public",
                table: "usuarios",
                newName: "external_id");

            migrationBuilder.RenameIndex(
                name: "ix_usuarios_keycloak_id",
                schema: "public",
                table: "usuarios",
                newName: "ix_usuarios_external_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "external_id",
                schema: "public",
                table: "usuarios",
                newName: "keycloak_id");

            migrationBuilder.RenameIndex(
                name: "ix_usuarios_external_id",
                schema: "public",
                table: "usuarios",
                newName: "ix_usuarios_keycloak_id");
        }
    }
}
