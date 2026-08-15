using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TutorSphere.Infrastructure.Persistence;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260815153000_AddGroupDefinedRoles")]
    public partial class AddGroupDefinedRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefinedRoleId",
                table: "ExpertGroupMembersSet",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExpertGroupDefinedRolesSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BadgeColor = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PermissionsJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SystemKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SuperAdminOnly = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpertGroupDefinedRolesSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpertGroupDefinedRolesSet_ExpertGroupsSet_ExpertGroupId",
                        column: x => x.ExpertGroupId,
                        principalTable: "ExpertGroupsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpertGroupDefinedRolesSet_ExpertGroupId_NormalizedName",
                table: "ExpertGroupDefinedRolesSet",
                columns: new[] { "ExpertGroupId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpertGroupDefinedRolesSet_ExpertGroupId_SystemKey",
                table: "ExpertGroupDefinedRolesSet",
                columns: new[] { "ExpertGroupId", "SystemKey" },
                unique: true,
                filter: "\"SystemKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertGroupMembersSet_DefinedRoleId",
                table: "ExpertGroupMembersSet",
                column: "DefinedRoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExpertGroupMembersSet_ExpertGroupDefinedRolesSet_DefinedRoleId",
                table: "ExpertGroupMembersSet",
                column: "DefinedRoleId",
                principalTable: "ExpertGroupDefinedRolesSet",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpertGroupMembersSet_ExpertGroupDefinedRolesSet_DefinedRoleId",
                table: "ExpertGroupMembersSet");

            migrationBuilder.DropTable(
                name: "ExpertGroupDefinedRolesSet");

            migrationBuilder.DropIndex(
                name: "IX_ExpertGroupMembersSet_DefinedRoleId",
                table: "ExpertGroupMembersSet");

            migrationBuilder.DropColumn(
                name: "DefinedRoleId",
                table: "ExpertGroupMembersSet");
        }
    }
}
