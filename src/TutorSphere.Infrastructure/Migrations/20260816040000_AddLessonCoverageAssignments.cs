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

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "LessonCoverageAssignmentsSet" (
                    "Id" uuid NOT NULL,
                    "ExpertGroupId" uuid NOT NULL,
                    "OriginalTenantId" uuid NOT NULL,
                    "SubstituteTenantId" uuid NOT NULL,
                    "LessonId" uuid NOT NULL,
                    "UnavailabilityId" uuid,
                    "Reason" character varying(500) NOT NULL,
                    "ProposedByUserId" character varying(450) NOT NULL,
                    "Status" integer NOT NULL,
                    "RespondedAt" timestamp with time zone,
                    "RespondedByUserId" character varying(450),
                    "TransferredAt" timestamp with time zone,
                    "TransferredTutorAmount" numeric(18,2),
                    "TransferCurrency" character varying(8) NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone,
                    CONSTRAINT "PK_LessonCoverageAssignmentsSet" PRIMARY KEY ("Id")
                );
                """);

            migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_LessonCoverageAssignmentsSet_ExpertGroupId" ON "LessonCoverageAssignmentsSet" ("ExpertGroupId");""");
            migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_LessonCoverageAssignmentsSet_LessonId" ON "LessonCoverageAssignmentsSet" ("LessonId");""");
            migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_LessonCoverageAssignmentsSet_OriginalTenantId" ON "LessonCoverageAssignmentsSet" ("OriginalTenantId");""");
            migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_LessonCoverageAssignmentsSet_Status" ON "LessonCoverageAssignmentsSet" ("Status");""");
            migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_LessonCoverageAssignmentsSet_SubstituteTenantId" ON "LessonCoverageAssignmentsSet" ("SubstituteTenantId");""");
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
