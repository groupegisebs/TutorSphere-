using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpertMembershipGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdmissionMethod",
                table: "ExpertGroupMembersSet",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "AdmittedAtUtc",
                table: "ExpertGroupMembersSet",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalCount",
                table: "ExpertGroupMembersSet",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedByAdminId",
                table: "ExpertGroupMembersSet",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredApprovalCount",
                table: "ExpertGroupMembersSet",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Specialty",
                table: "ExpertGroupMembersSet",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "ExpertGroupMembersSet",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ExpertMembershipInvitesSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    LastName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Specialty = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IntendedRole = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Presentation = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Justification = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PersonalMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InviteExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VoteOpenedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoteExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CandidateUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    EligibleVoterUserIdsCsv = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    RequiredApprovalCount = table.Column<int>(type: "integer", nullable: false),
                    ConductAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    PrivacyAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    CandidateSubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AdminClosedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    AdminNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpertMembershipInvitesSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpertMembershipInvitesSet_ExpertGroupsSet_ExpertGroupId",
                        column: x => x.ExpertGroupId,
                        principalTable: "ExpertGroupsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpertMembershipVotesSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InviteId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoterUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Choice = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    VotedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpertMembershipVotesSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpertMembershipVotesSet_ExpertMembershipInvitesSet_InviteId",
                        column: x => x.InviteId,
                        principalTable: "ExpertMembershipInvitesSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpertMembershipInvitesSet_Email",
                table: "ExpertMembershipInvitesSet",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertMembershipInvitesSet_ExpertGroupId",
                table: "ExpertMembershipInvitesSet",
                column: "ExpertGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertMembershipInvitesSet_Status",
                table: "ExpertMembershipInvitesSet",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertMembershipInvitesSet_Token",
                table: "ExpertMembershipInvitesSet",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpertMembershipVotesSet_InviteId_VoterUserId",
                table: "ExpertMembershipVotesSet",
                columns: new[] { "InviteId", "VoterUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpertMembershipVotesSet");

            migrationBuilder.DropTable(
                name: "ExpertMembershipInvitesSet");

            migrationBuilder.DropColumn(
                name: "AdmissionMethod",
                table: "ExpertGroupMembersSet");

            migrationBuilder.DropColumn(
                name: "AdmittedAtUtc",
                table: "ExpertGroupMembersSet");

            migrationBuilder.DropColumn(
                name: "ApprovalCount",
                table: "ExpertGroupMembersSet");

            migrationBuilder.DropColumn(
                name: "ApprovedByAdminId",
                table: "ExpertGroupMembersSet");

            migrationBuilder.DropColumn(
                name: "RequiredApprovalCount",
                table: "ExpertGroupMembersSet");

            migrationBuilder.DropColumn(
                name: "Specialty",
                table: "ExpertGroupMembersSet");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ExpertGroupMembersSet");
        }
    }
}
