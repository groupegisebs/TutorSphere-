using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherApplicationInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeacherApplicationInvitesSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PersonalMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InvitedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ExpertGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AcceptedTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherApplicationInvitesSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherApplicationInvitesSet_ExpertGroupsSet_ExpertGroupId",
                        column: x => x.ExpertGroupId,
                        principalTable: "ExpertGroupsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherApplicationInvitesSet_Email",
                table: "TeacherApplicationInvitesSet",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherApplicationInvitesSet_ExpertGroupId",
                table: "TeacherApplicationInvitesSet",
                column: "ExpertGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherApplicationInvitesSet_SentAt",
                table: "TeacherApplicationInvitesSet",
                column: "SentAt");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherApplicationInvitesSet_Token",
                table: "TeacherApplicationInvitesSet",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeacherApplicationInvitesSet");
        }
    }
}
