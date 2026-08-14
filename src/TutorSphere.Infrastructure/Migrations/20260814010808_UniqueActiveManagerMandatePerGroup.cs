using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UniqueActiveManagerMandatePerGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExpertGroupManagerMandatesSet_ExpertGroupId",
                table: "ExpertGroupManagerMandatesSet");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertGroupManagerMandates_OneActivePerGroup",
                table: "ExpertGroupManagerMandatesSet",
                column: "ExpertGroupId",
                unique: true,
                filter: "\"Status\" = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExpertGroupManagerMandates_OneActivePerGroup",
                table: "ExpertGroupManagerMandatesSet");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertGroupManagerMandatesSet_ExpertGroupId",
                table: "ExpertGroupManagerMandatesSet",
                column: "ExpertGroupId");
        }
    }
}
