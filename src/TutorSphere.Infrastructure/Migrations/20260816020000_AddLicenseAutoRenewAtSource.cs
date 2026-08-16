using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLicenseAutoRenewAtSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: production restarted in a crash loop when this class was
            // deployed without [Migration], so EF skipped it while the model already
            // mapped Tenant.LicenseAutoRenewAtSource.
            migrationBuilder.Sql("""
                ALTER TABLE "TenantsSet" ADD COLUMN IF NOT EXISTS "LicenseAutoRenewAtSource" boolean NOT NULL DEFAULT false;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "TenantsSet" DROP COLUMN IF EXISTS "LicenseAutoRenewAtSource";
                """);
        }
    }
}
