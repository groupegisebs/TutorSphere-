using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TutorSphere.Infrastructure.Persistence;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260815220000_AddPackLifecycleReminderFields")]
    public partial class AddPackLifecycleReminderFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "StudentSubscriptionsSet" ADD COLUMN IF NOT EXISTS "LowSessionsReminderSentAt" timestamp with time zone;
                ALTER TABLE "StudentSubscriptionsSet" ADD COLUMN IF NOT EXISTS "LessonAccessReminderSentAt" timestamp with time zone;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "StudentSubscriptionsSet" DROP COLUMN IF EXISTS "LowSessionsReminderSentAt";
                ALTER TABLE "StudentSubscriptionsSet" DROP COLUMN IF EXISTS "LessonAccessReminderSentAt";
                """);
        }
    }
}
