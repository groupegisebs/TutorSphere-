using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonRemindersAndCalendarFeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderSentAt",
                table: "LessonsSet",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalendarFeedToken",
                table: "AspNetUsers",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmailLessonReminders",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CalendarFeedToken",
                table: "AspNetUsers",
                column: "CalendarFeedToken");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_CalendarFeedToken",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                table: "LessonsSet");

            migrationBuilder.DropColumn(
                name: "CalendarFeedToken",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EmailLessonReminders",
                table: "AspNetUsers");
        }
    }
}
