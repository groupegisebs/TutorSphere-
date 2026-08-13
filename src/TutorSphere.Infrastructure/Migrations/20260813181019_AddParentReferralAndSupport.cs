using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddParentReferralAndSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferralCode",
                table: "ParentProfilesSet",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReferralRewardMonths",
                table: "ParentProfilesSet",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferredByParentProfileId",
                table: "ParentProfilesSet",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ParentSupportRequestsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentSupportRequestsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentSupportRequestsSet_ParentProfilesSet_ParentProfileId",
                        column: x => x.ParentProfileId,
                        principalTable: "ParentProfilesSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParentProfilesSet_ReferralCode",
                table: "ParentProfilesSet",
                column: "ReferralCode",
                unique: true,
                filter: "\"ReferralCode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ParentProfilesSet_ReferredByParentProfileId",
                table: "ParentProfilesSet",
                column: "ReferredByParentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentSupportRequestsSet_ParentProfileId",
                table: "ParentSupportRequestsSet",
                column: "ParentProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_ParentProfilesSet_ParentProfilesSet_ReferredByParentProfile~",
                table: "ParentProfilesSet",
                column: "ReferredByParentProfileId",
                principalTable: "ParentProfilesSet",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ParentProfilesSet_ParentProfilesSet_ReferredByParentProfile~",
                table: "ParentProfilesSet");

            migrationBuilder.DropTable(
                name: "ParentSupportRequestsSet");

            migrationBuilder.DropIndex(
                name: "IX_ParentProfilesSet_ReferralCode",
                table: "ParentProfilesSet");

            migrationBuilder.DropIndex(
                name: "IX_ParentProfilesSet_ReferredByParentProfileId",
                table: "ParentProfilesSet");

            migrationBuilder.DropColumn(
                name: "ReferralCode",
                table: "ParentProfilesSet");

            migrationBuilder.DropColumn(
                name: "ReferralRewardMonths",
                table: "ParentProfilesSet");

            migrationBuilder.DropColumn(
                name: "ReferredByParentProfileId",
                table: "ParentProfilesSet");
        }
    }
}
