using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TutorSphere.Infrastructure.Persistence;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260815203000_AddParentMailboxFields")]
    public partial class AddParentMailboxFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "MessagesSet" ADD COLUMN IF NOT EXISTS "StudentId" uuid;
                ALTER TABLE "MessagesSet" ADD COLUMN IF NOT EXISTS "ParentChannel" character varying(20);
                ALTER TABLE "MessagesSet" ADD COLUMN IF NOT EXISTS "ParentReason" character varying(40);
                ALTER TABLE "MessagesSet" ADD COLUMN IF NOT EXISTS "CaseNumber" character varying(20);
                ALTER TABLE "MessagesSet" ADD COLUMN IF NOT EXISTS "AttachmentType" character varying(20);
                ALTER TABLE "MessagesSet" ADD COLUMN IF NOT EXISTS "AttachmentId" uuid;
                ALTER TABLE "MessagesSet" ADD COLUMN IF NOT EXISTS "AttachmentLabel" character varying(200);
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_MessagesSet_ParentChannel_StudentId"
                    ON "MessagesSet" ("ParentChannel", "StudentId");
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "ParentSupportRequestsSet" ADD COLUMN IF NOT EXISTS "CaseNumber" character varying(20);
                ALTER TABLE "ParentSupportRequestsSet" ADD COLUMN IF NOT EXISTS "StudentId" uuid;
                ALTER TABLE "ParentSupportRequestsSet" ADD COLUMN IF NOT EXISTS "Reason" character varying(40);
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_ParentSupportRequestsSet_CaseNumber"
                    ON "ParentSupportRequestsSet" ("CaseNumber") WHERE "CaseNumber" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_MessagesSet_ParentChannel_StudentId";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_ParentSupportRequestsSet_CaseNumber";""");

            migrationBuilder.Sql("""
                ALTER TABLE "MessagesSet" DROP COLUMN IF EXISTS "StudentId";
                ALTER TABLE "MessagesSet" DROP COLUMN IF EXISTS "ParentChannel";
                ALTER TABLE "MessagesSet" DROP COLUMN IF EXISTS "ParentReason";
                ALTER TABLE "MessagesSet" DROP COLUMN IF EXISTS "CaseNumber";
                ALTER TABLE "MessagesSet" DROP COLUMN IF EXISTS "AttachmentType";
                ALTER TABLE "MessagesSet" DROP COLUMN IF EXISTS "AttachmentId";
                ALTER TABLE "MessagesSet" DROP COLUMN IF EXISTS "AttachmentLabel";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "ParentSupportRequestsSet" DROP COLUMN IF EXISTS "CaseNumber";
                ALTER TABLE "ParentSupportRequestsSet" DROP COLUMN IF EXISTS "StudentId";
                ALTER TABLE "ParentSupportRequestsSet" DROP COLUMN IF EXISTS "Reason";
                """);
        }
    }
}
