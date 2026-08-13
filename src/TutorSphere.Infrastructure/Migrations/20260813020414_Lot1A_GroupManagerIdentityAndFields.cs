using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Lot1A_GroupManagerIdentityAndFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GroupManagerMembershipId",
                table: "ExpertGroupsSet",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ManagerAssignedAtUtc",
                table: "ExpertGroupsSet",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagerAssignedByAdminId",
                table: "ExpertGroupsSet",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpertGroupsSet_GroupManagerMembershipId",
                table: "ExpertGroupsSet",
                column: "GroupManagerMembershipId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExpertGroupsSet_GroupManagerMembershipId",
                table: "ExpertGroupsSet");

            migrationBuilder.DropColumn(
                name: "GroupManagerMembershipId",
                table: "ExpertGroupsSet");

            migrationBuilder.DropColumn(
                name: "ManagerAssignedAtUtc",
                table: "ExpertGroupsSet");

            migrationBuilder.DropColumn(
                name: "ManagerAssignedByAdminId",
                table: "ExpertGroupsSet");
        }
    }
}
