using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPackLifecycleReminderFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LowSessionsReminderSentAt",
                table: "StudentSubscriptionsSet",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LessonAccessReminderSentAt",
                table: "StudentSubscriptionsSet",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LowSessionsReminderSentAt",
                table: "StudentSubscriptionsSet");

            migrationBuilder.DropColumn(
                name: "LessonAccessReminderSentAt",
                table: "StudentSubscriptionsSet");
        }
    }
}
