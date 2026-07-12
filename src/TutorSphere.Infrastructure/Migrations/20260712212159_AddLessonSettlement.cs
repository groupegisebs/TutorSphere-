using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "LessonsSet",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "LessonsSet",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SessionCounted",
                table: "LessonsSet",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SettlementStatus",
                table: "LessonsSet",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TutorLiabilityResolution",
                table: "LessonsSet",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TutorLiabilityResolvedAt",
                table: "LessonsSet",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TutorLiable",
                table: "LessonsSet",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "LessonsSet");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "LessonsSet");

            migrationBuilder.DropColumn(
                name: "SessionCounted",
                table: "LessonsSet");

            migrationBuilder.DropColumn(
                name: "SettlementStatus",
                table: "LessonsSet");

            migrationBuilder.DropColumn(
                name: "TutorLiabilityResolution",
                table: "LessonsSet");

            migrationBuilder.DropColumn(
                name: "TutorLiabilityResolvedAt",
                table: "LessonsSet");

            migrationBuilder.DropColumn(
                name: "TutorLiable",
                table: "LessonsSet");
        }
    }
}
