using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations;

/// <inheritdoc />
public partial class MobileMoneyFeeSettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "PlatformPaymentSettingsSet" ADD COLUMN IF NOT EXISTS "MobileMoneyFeePercent" numeric(5,2) NOT NULL DEFAULT 2;
            ALTER TABLE "PlatformPaymentSettingsSet" ADD COLUMN IF NOT EXISTS "MobileMoneyFeeFixed" numeric(18,2) NOT NULL DEFAULT 0;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "PlatformPaymentSettingsSet" DROP COLUMN IF EXISTS "MobileMoneyFeePercent";
            ALTER TABLE "PlatformPaymentSettingsSet" DROP COLUMN IF EXISTS "MobileMoneyFeeFixed";
            """);
    }
}
