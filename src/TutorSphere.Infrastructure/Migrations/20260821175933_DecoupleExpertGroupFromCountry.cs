using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DecoupleExpertGroupFromCountry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExpertGroupsSet_CountryCode",
                table: "ExpertGroupsSet");

            migrationBuilder.DropIndex(
                name: "IX_ExpertGroupsSet_IsInternational",
                table: "ExpertGroupsSet");

            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultReviewGroup",
                table: "ExpertGroupsSet",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Sans désignation, les candidatures spontanées n'auraient plus de destinataire du jour
            // au lendemain. Le groupe international actif jouait ce rôle jusqu'ici : il le conserve,
            // à défaut le plus ancien groupe actif, et l'administrateur pourra en changer.
            migrationBuilder.Sql("""
                UPDATE "ExpertGroupsSet"
                SET "IsDefaultReviewGroup" = TRUE
                WHERE "Id" = (
                    SELECT "Id" FROM "ExpertGroupsSet"
                    WHERE "IsActive" = TRUE
                    ORDER BY "IsInternational" DESC, "CreatedAt"
                    LIMIT 1
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ExpertGroupsSet_CountryCode",
                table: "ExpertGroupsSet",
                column: "CountryCode");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertGroupsSet_IsDefaultReviewGroup",
                table: "ExpertGroupsSet",
                column: "IsDefaultReviewGroup",
                unique: true,
                filter: "\"IsDefaultReviewGroup\" = TRUE AND \"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertGroupsSet_IsInternational",
                table: "ExpertGroupsSet",
                column: "IsInternational");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExpertGroupsSet_CountryCode",
                table: "ExpertGroupsSet");

            migrationBuilder.DropIndex(
                name: "IX_ExpertGroupsSet_IsDefaultReviewGroup",
                table: "ExpertGroupsSet");

            migrationBuilder.DropIndex(
                name: "IX_ExpertGroupsSet_IsInternational",
                table: "ExpertGroupsSet");

            migrationBuilder.DropColumn(
                name: "IsDefaultReviewGroup",
                table: "ExpertGroupsSet");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertGroupsSet_CountryCode",
                table: "ExpertGroupsSet",
                column: "CountryCode",
                unique: true,
                filter: "\"IsInternational\" = FALSE AND \"CountryCode\" IS NOT NULL AND \"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertGroupsSet_IsInternational",
                table: "ExpertGroupsSet",
                column: "IsInternational",
                unique: true,
                filter: "\"IsInternational\" = TRUE AND \"IsActive\" = TRUE");
        }
    }
}
