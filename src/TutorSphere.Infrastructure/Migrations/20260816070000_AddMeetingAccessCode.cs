using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddMeetingAccessCode : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "MeetingsSet" ADD COLUMN IF NOT EXISTS "AccessCode" character varying(16);
            ALTER TABLE "MeetingExternalGuestsSet" ADD COLUMN IF NOT EXISTS "AccessCode" character varying(16);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "MeetingsSet" DROP COLUMN IF EXISTS "AccessCode";
            ALTER TABLE "MeetingExternalGuestsSet" DROP COLUMN IF EXISTS "AccessCode";
            """);
    }
}
