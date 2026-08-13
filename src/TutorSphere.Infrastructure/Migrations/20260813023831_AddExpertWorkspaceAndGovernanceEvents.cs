using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpertWorkspaceAndGovernanceEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExpertGovernanceEventsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    ActorUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RelatedTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayloadJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsNotification = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpertGovernanceEventsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpertGovernanceEventsSet_ExpertGroupsSet_ExpertGroupId",
                        column: x => x.ExpertGroupId,
                        principalTable: "ExpertGroupsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpertWorkspaceItemsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RelatedTeacherTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    AssignedToUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    ScheduledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OutcomeNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpertWorkspaceItemsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpertWorkspaceItemsSet_ExpertGroupsSet_ExpertGroupId",
                        column: x => x.ExpertGroupId,
                        principalTable: "ExpertGroupsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExpertWorkspaceItemsSet_TenantsSet_RelatedTeacherTenantId",
                        column: x => x.RelatedTeacherTenantId,
                        principalTable: "TenantsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpertGovernanceEventsSet_CreatedAt",
                table: "ExpertGovernanceEventsSet",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertGovernanceEventsSet_ExpertGroupId",
                table: "ExpertGovernanceEventsSet",
                column: "ExpertGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertWorkspaceItemsSet_ExpertGroupId_ItemType_Status",
                table: "ExpertWorkspaceItemsSet",
                columns: new[] { "ExpertGroupId", "ItemType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpertWorkspaceItemsSet_RelatedTeacherTenantId",
                table: "ExpertWorkspaceItemsSet",
                column: "RelatedTeacherTenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpertGovernanceEventsSet");

            migrationBuilder.DropTable(
                name: "ExpertWorkspaceItemsSet");
        }
    }
}
