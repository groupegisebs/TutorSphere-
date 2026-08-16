using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddTutorPayoutGroupInvoice : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "TutorPayoutsSet" ADD COLUMN IF NOT EXISTS "InvoiceNumber" character varying(40);
            ALTER TABLE "TutorPayoutsSet" ADD COLUMN IF NOT EXISTS "ExpertGroupId" uuid;
            ALTER TABLE "TutorPayoutsSet" ADD COLUMN IF NOT EXISTS "PaymentMethodSnapshot" character varying(4000);
            ALTER TABLE "TutorPayoutsSet" ADD COLUMN IF NOT EXISTS "ProcessingAt" timestamp with time zone;
            ALTER TABLE "TutorPayoutsSet" ADD COLUMN IF NOT EXISTS "PaidByUserId" character varying(450);
            """);
        migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_TutorPayoutsSet_ExpertGroupId" ON "TutorPayoutsSet" ("ExpertGroupId");""");
        migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_TutorPayoutsSet_InvoiceNumber" ON "TutorPayoutsSet" ("InvoiceNumber");""");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_TutorPayoutsSet_ExpertGroupId";""");
        migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_TutorPayoutsSet_InvoiceNumber";""");
        migrationBuilder.Sql("""
            ALTER TABLE "TutorPayoutsSet" DROP COLUMN IF EXISTS "InvoiceNumber";
            ALTER TABLE "TutorPayoutsSet" DROP COLUMN IF EXISTS "ExpertGroupId";
            ALTER TABLE "TutorPayoutsSet" DROP COLUMN IF EXISTS "PaymentMethodSnapshot";
            ALTER TABLE "TutorPayoutsSet" DROP COLUMN IF EXISTS "ProcessingAt";
            ALTER TABLE "TutorPayoutsSet" DROP COLUMN IF EXISTS "PaidByUserId";
            """);
    }
}
