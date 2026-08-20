using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations;

/// <inheritdoc />
public partial class UniqueActiveExpertGroupTerritory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS "IX_ExpertGroupsSet_CountryCode";
            DROP INDEX IF EXISTS "IX_ExpertGroupsSet_IsInternational";

            CREATE UNIQUE INDEX "IX_ExpertGroupsSet_CountryCode"
                ON "ExpertGroupsSet" ("CountryCode")
                WHERE "IsInternational" = FALSE AND "CountryCode" IS NOT NULL AND "IsActive" = TRUE;

            CREATE UNIQUE INDEX "IX_ExpertGroupsSet_IsInternational"
                ON "ExpertGroupsSet" ("IsInternational")
                WHERE "IsInternational" = TRUE AND "IsActive" = TRUE;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS "IX_ExpertGroupsSet_CountryCode";
            DROP INDEX IF EXISTS "IX_ExpertGroupsSet_IsInternational";

            CREATE UNIQUE INDEX "IX_ExpertGroupsSet_CountryCode"
                ON "ExpertGroupsSet" ("CountryCode")
                WHERE "IsInternational" = FALSE AND "CountryCode" IS NOT NULL;

            CREATE UNIQUE INDEX "IX_ExpertGroupsSet_IsInternational"
                ON "ExpertGroupsSet" ("IsInternational")
                WHERE "IsInternational" = TRUE;
            """);
    }
}
