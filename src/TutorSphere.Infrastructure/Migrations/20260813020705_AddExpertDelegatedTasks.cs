using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpertDelegatedTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExpertDelegatedTasksSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByManagerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    AssigneeExpertUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DueAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletionNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpertDelegatedTasksSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpertDelegatedTasksSet_ExpertGroupsSet_ExpertGroupId",
                        column: x => x.ExpertGroupId,
                        principalTable: "ExpertGroupsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExpertDelegatedTasksSet_TenantsSet_TeacherTenantId",
                        column: x => x.TeacherTenantId,
                        principalTable: "TenantsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpertDelegatedTasksSet_AssigneeExpertUserId",
                table: "ExpertDelegatedTasksSet",
                column: "AssigneeExpertUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertDelegatedTasksSet_ExpertGroupId",
                table: "ExpertDelegatedTasksSet",
                column: "ExpertGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertDelegatedTasksSet_Status",
                table: "ExpertDelegatedTasksSet",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertDelegatedTasksSet_TeacherTenantId",
                table: "ExpertDelegatedTasksSet",
                column: "TeacherTenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpertDelegatedTasksSet");
        }
    }
}
