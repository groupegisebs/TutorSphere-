using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpertReviewAssignmentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewAssignedAt",
                table: "TenantsSet",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewAssignedToUserId",
                table: "TenantsSet",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewPriority",
                table: "TenantsSet",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReviewRequestNotes",
                table: "TenantsSet",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantsSet_ReviewAssignedToUserId",
                table: "TenantsSet",
                column: "ReviewAssignedToUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenantsSet_ReviewAssignedToUserId",
                table: "TenantsSet");

            migrationBuilder.DropColumn(
                name: "ReviewAssignedAt",
                table: "TenantsSet");

            migrationBuilder.DropColumn(
                name: "ReviewAssignedToUserId",
                table: "TenantsSet");

            migrationBuilder.DropColumn(
                name: "ReviewPriority",
                table: "TenantsSet");

            migrationBuilder.DropColumn(
                name: "ReviewRequestNotes",
                table: "TenantsSet");
        }
    }
}
