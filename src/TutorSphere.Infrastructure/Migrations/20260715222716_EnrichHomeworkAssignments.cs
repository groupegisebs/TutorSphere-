using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnrichHomeworkAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignmentGroupId",
                table: "HomeworksSet",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentJson",
                table: "HomeworksSet",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CriteriaJson",
                table: "HomeworksSet",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedMinutes",
                table: "HomeworksSet",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Instructions",
                table: "HomeworksSet",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDraft",
                table: "HomeworksSet",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "HomeworksSet",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubmissionModes",
                table: "HomeworksSet",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignmentGroupId",
                table: "HomeworksSet");

            migrationBuilder.DropColumn(
                name: "ContentJson",
                table: "HomeworksSet");

            migrationBuilder.DropColumn(
                name: "CriteriaJson",
                table: "HomeworksSet");

            migrationBuilder.DropColumn(
                name: "EstimatedMinutes",
                table: "HomeworksSet");

            migrationBuilder.DropColumn(
                name: "Instructions",
                table: "HomeworksSet");

            migrationBuilder.DropColumn(
                name: "IsDraft",
                table: "HomeworksSet");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "HomeworksSet");

            migrationBuilder.DropColumn(
                name: "SubmissionModes",
                table: "HomeworksSet");
        }
    }
}
