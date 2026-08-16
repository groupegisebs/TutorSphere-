using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonCoverageAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "LessonsSet" ADD COLUMN IF NOT EXISTS "DeliveredByTenantId" uuid;
                """);

            migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_LessonsSet_DeliveredByTenantId" ON "LessonsSet" ("DeliveredByTenantId");""");

            migrationBuilder.CreateTable(
                name: "LessonCoverageAssignmentsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubstituteTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnavailabilityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProposedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RespondedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    TransferredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TransferredTutorAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TransferCurrency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonCoverageAssignmentsSet", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LessonCoverageAssignmentsSet_ExpertGroupId",
                table: "LessonCoverageAssignmentsSet",
                column: "ExpertGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonCoverageAssignmentsSet_LessonId",
                table: "LessonCoverageAssignmentsSet",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonCoverageAssignmentsSet_OriginalTenantId",
                table: "LessonCoverageAssignmentsSet",
                column: "OriginalTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonCoverageAssignmentsSet_Status",
                table: "LessonCoverageAssignmentsSet",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LessonCoverageAssignmentsSet_SubstituteTenantId",
                table: "LessonCoverageAssignmentsSet",
                column: "SubstituteTenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "LessonCoverageAssignmentsSet");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_LessonsSet_DeliveredByTenantId";""");
            migrationBuilder.Sql("""ALTER TABLE "LessonsSet" DROP COLUMN IF EXISTS "DeliveredByTenantId";""");
        }
    }
}
