using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherProfileVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VisibleCountryCodes",
                table: "TenantsSet",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "ParentProfilesSet",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            // Défaut : pays d'origine de l'enseignant (2 premiers caractères normalisés).
            migrationBuilder.Sql(
                """
                UPDATE "TenantsSet"
                SET "VisibleCountryCodes" = UPPER(LEFT(TRIM("Country"), 2))
                WHERE "Country" IS NOT NULL
                  AND LENGTH(TRIM("Country")) >= 2
                  AND ("VisibleCountryCodes" IS NULL OR BTRIM("VisibleCountryCodes") = '');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VisibleCountryCodes",
                table: "TenantsSet");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "ParentProfilesSet");
        }
    }
}
