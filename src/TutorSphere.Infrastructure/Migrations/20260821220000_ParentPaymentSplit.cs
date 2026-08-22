using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations;

/// <inheritdoc />
public partial class ParentPaymentSplit : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "ExpertGroupsSet" ADD COLUMN IF NOT EXISTS "PlatformCommissionPercent" numeric(5,2) NOT NULL DEFAULT 30;
            ALTER TABLE "PaymentsSet" ADD COLUMN IF NOT EXISTS "ProcessorFee" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "PaymentsSet" ADD COLUMN IF NOT EXISTS "GroupAmount" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "PaymentsSet" ADD COLUMN IF NOT EXISTS "ExpertGroupId" uuid;
            ALTER TABLE "PaymentsSet" ADD COLUMN IF NOT EXISTS "CommissionPercent" numeric(5,2) NOT NULL DEFAULT 0;
            CREATE INDEX IF NOT EXISTS "IX_PaymentsSet_ExpertGroupId" ON "PaymentsSet" ("ExpertGroupId");
            CREATE TABLE IF NOT EXISTS "PlatformPaymentSettingsSet" (
                "Id" uuid NOT NULL,
                "DefaultCommissionPercent" numeric(5,2) NOT NULL,
                "CardFeePercent" numeric(5,2) NOT NULL,
                "CardFeeFixed" numeric(18,2) NOT NULL,
                "PayPalFeePercent" numeric(5,2) NOT NULL,
                "PayPalFeeFixed" numeric(18,2) NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone,
                CONSTRAINT "PK_PlatformPaymentSettingsSet" PRIMARY KEY ("Id")
            );
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS "PlatformPaymentSettingsSet";
            DROP INDEX IF EXISTS "IX_PaymentsSet_ExpertGroupId";
            ALTER TABLE "PaymentsSet" DROP COLUMN IF EXISTS "ProcessorFee";
            ALTER TABLE "PaymentsSet" DROP COLUMN IF EXISTS "GroupAmount";
            ALTER TABLE "PaymentsSet" DROP COLUMN IF EXISTS "ExpertGroupId";
            ALTER TABLE "PaymentsSet" DROP COLUMN IF EXISTS "CommissionPercent";
            ALTER TABLE "ExpertGroupsSet" DROP COLUMN IF EXISTS "PlatformCommissionPercent";
            """);
    }
}
