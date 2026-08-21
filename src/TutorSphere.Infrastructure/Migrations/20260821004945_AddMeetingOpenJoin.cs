using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingOpenJoin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxParticipants",
                table: "MeetingsSet",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OpenJoinEnabled",
                table: "MeetingsSet",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OpenJoinToken",
                table: "MeetingsSet",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingsSet_OpenJoinToken",
                table: "MeetingsSet",
                column: "OpenJoinToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MeetingsSet_OpenJoinToken",
                table: "MeetingsSet");

            migrationBuilder.DropColumn(
                name: "MaxParticipants",
                table: "MeetingsSet");

            migrationBuilder.DropColumn(
                name: "OpenJoinEnabled",
                table: "MeetingsSet");

            migrationBuilder.DropColumn(
                name: "OpenJoinToken",
                table: "MeetingsSet");
        }
    }
}
