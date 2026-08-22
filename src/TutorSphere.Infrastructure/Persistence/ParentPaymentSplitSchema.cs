using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TutorSphere.Infrastructure.Persistence;

/// <summary>
/// Filet schéma du split parent. Idempotent : appelé au démarrage et avant chaque enregistrement de paiement.
/// Sans ces colonnes, EF lève uniquement « An error occurred while saving the entity changes ».
/// </summary>
internal static class ParentPaymentSplitSchema
{
    public static async Task EnsureAsync(
        ApplicationDbContext db,
        CancellationToken ct = default,
        ILogger? logger = null)
    {
        await db.Database.ExecuteSqlRawAsync(
            """ALTER TABLE "ExpertGroupsSet" ADD COLUMN IF NOT EXISTS "PlatformCommissionPercent" numeric(5,2) NOT NULL DEFAULT 30;""",
            ct);
        await db.Database.ExecuteSqlRawAsync(
            """ALTER TABLE "PaymentsSet" ADD COLUMN IF NOT EXISTS "ProcessorFee" numeric(18,2) NOT NULL DEFAULT 0;""",
            ct);
        await db.Database.ExecuteSqlRawAsync(
            """ALTER TABLE "PaymentsSet" ADD COLUMN IF NOT EXISTS "GroupAmount" numeric(18,2) NOT NULL DEFAULT 0;""",
            ct);
        await db.Database.ExecuteSqlRawAsync(
            """ALTER TABLE "PaymentsSet" ADD COLUMN IF NOT EXISTS "ExpertGroupId" uuid;""",
            ct);
        await db.Database.ExecuteSqlRawAsync(
            """ALTER TABLE "PaymentsSet" ADD COLUMN IF NOT EXISTS "CommissionPercent" numeric(5,2) NOT NULL DEFAULT 0;""",
            ct);
        await db.Database.ExecuteSqlRawAsync(
            """CREATE INDEX IF NOT EXISTS "IX_PaymentsSet_ExpertGroupId" ON "PaymentsSet" ("ExpertGroupId");""",
            ct);
        await db.Database.ExecuteSqlRawAsync(
            """ALTER TABLE "PaymentsSet" ADD COLUMN IF NOT EXISTS "Channel" character varying(20);""",
            ct);
        await db.Database.ExecuteSqlRawAsync(
            """ALTER TABLE "PaymentsSet" ADD COLUMN IF NOT EXISTS "PhoneMasked" character varying(40);""",
            ct);
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "PlatformPaymentSettingsSet" (
                "Id" uuid NOT NULL,
                "DefaultCommissionPercent" numeric(5,2) NOT NULL,
                "CardFeePercent" numeric(5,2) NOT NULL,
                "CardFeeFixed" numeric(18,2) NOT NULL,
                "PayPalFeePercent" numeric(5,2) NOT NULL,
                "PayPalFeeFixed" numeric(18,2) NOT NULL,
                "MobileMoneyFeePercent" numeric(5,2) NOT NULL DEFAULT 2,
                "MobileMoneyFeeFixed" numeric(18,2) NOT NULL DEFAULT 0,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone,
                CONSTRAINT "PK_PlatformPaymentSettingsSet" PRIMARY KEY ("Id")
            );
            """,
            ct);
        await db.Database.ExecuteSqlRawAsync(
            """ALTER TABLE "PlatformPaymentSettingsSet" ADD COLUMN IF NOT EXISTS "MobileMoneyFeePercent" numeric(5,2) NOT NULL DEFAULT 2;""",
            ct);
        await db.Database.ExecuteSqlRawAsync(
            """ALTER TABLE "PlatformPaymentSettingsSet" ADD COLUMN IF NOT EXISTS "MobileMoneyFeeFixed" numeric(18,2) NOT NULL DEFAULT 0;""",
            ct);
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "PlatformPaymentSettingsSet" (
                "Id",
                "DefaultCommissionPercent",
                "CardFeePercent",
                "CardFeeFixed",
                "PayPalFeePercent",
                "PayPalFeeFixed",
                "MobileMoneyFeePercent",
                "MobileMoneyFeeFixed",
                "CreatedAt")
            SELECT
                '00000000-0000-0000-0000-000000000001'::uuid,
                30, 2.9, 0.30, 2.9, 0.30, 2.0, 0,
                NOW()
            WHERE NOT EXISTS (SELECT 1 FROM "PlatformPaymentSettingsSet");
            """,
            ct);

        logger?.LogInformation("Parent payment split schema is present.");
    }
}
