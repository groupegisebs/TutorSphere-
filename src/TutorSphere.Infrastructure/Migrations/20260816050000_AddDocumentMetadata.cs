using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "DocumentsSet" ADD COLUMN IF NOT EXISTS "Title" character varying(200);
                ALTER TABLE "DocumentsSet" ADD COLUMN IF NOT EXISTS "Subject" character varying(120);
                ALTER TABLE "DocumentsSet" ADD COLUMN IF NOT EXISTS "SchoolLevel" character varying(40);
                ALTER TABLE "DocumentsSet" ADD COLUMN IF NOT EXISTS "Summary" character varying(2000);
                ALTER TABLE "DocumentsSet" ADD COLUMN IF NOT EXISTS "SharedStudentIds" character varying(4000);
                ALTER TABLE "DocumentsSet" ADD COLUMN IF NOT EXISTS "SharedByExpertGroupId" uuid;
                ALTER TABLE "DocumentsSet" ADD COLUMN IF NOT EXISTS "LibraryBatchId" uuid;
                """);

            migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_DocumentsSet_LibraryBatchId" ON "DocumentsSet" ("LibraryBatchId");""");
            migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_DocumentsSet_SharedByExpertGroupId" ON "DocumentsSet" ("SharedByExpertGroupId");""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_DocumentsSet_LibraryBatchId";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_DocumentsSet_SharedByExpertGroupId";""");
            migrationBuilder.Sql("""
                ALTER TABLE "DocumentsSet" DROP COLUMN IF EXISTS "Title";
                ALTER TABLE "DocumentsSet" DROP COLUMN IF EXISTS "Subject";
                ALTER TABLE "DocumentsSet" DROP COLUMN IF EXISTS "SchoolLevel";
                ALTER TABLE "DocumentsSet" DROP COLUMN IF EXISTS "Summary";
                ALTER TABLE "DocumentsSet" DROP COLUMN IF EXISTS "SharedStudentIds";
                ALTER TABLE "DocumentsSet" DROP COLUMN IF EXISTS "SharedByExpertGroupId";
                ALTER TABLE "DocumentsSet" DROP COLUMN IF EXISTS "LibraryBatchId";
                """);
        }
    }
}
