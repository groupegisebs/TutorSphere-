using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupMemberAdminFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvitedByUserId",
                table: "ExpertGroupMembersSet",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PermissionsJson",
                table: "ExpertGroupMembersSet",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedAtUtc",
                table: "ExpertGroupMembersSet",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspensionReason",
                table: "ExpertGroupMembersSet",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvitedByUserId",
                table: "ExpertGroupMembersSet");

            migrationBuilder.DropColumn(
                name: "PermissionsJson",
                table: "ExpertGroupMembersSet");

            migrationBuilder.DropColumn(
                name: "SuspendedAtUtc",
                table: "ExpertGroupMembersSet");

            migrationBuilder.DropColumn(
                name: "SuspensionReason",
                table: "ExpertGroupMembersSet");
        }
    }
}
