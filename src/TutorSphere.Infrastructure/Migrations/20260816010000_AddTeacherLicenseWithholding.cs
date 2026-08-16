using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherLicenseWithholding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: older builds shipped this class without [Migration], so prod
            // may already have been patched (or still missing the columns).
            migrationBuilder.Sql("""
                ALTER TABLE "TenantsSet" ADD COLUMN IF NOT EXISTS "LicenseFeeWithholdingRemainingUsd" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE "TenantsSet" ADD COLUMN IF NOT EXISTS "LicenseSettlementKind" text;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "TenantsSet" DROP COLUMN IF EXISTS "LicenseFeeWithholdingRemainingUsd";
                ALTER TABLE "TenantsSet" DROP COLUMN IF EXISTS "LicenseSettlementKind";
                """);
        }
    }
}
