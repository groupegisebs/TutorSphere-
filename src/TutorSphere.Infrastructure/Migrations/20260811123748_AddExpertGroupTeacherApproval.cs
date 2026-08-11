using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpertGroupTeacherApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByExpertGroupId",
                table: "TenantsSet",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedByUserId",
                table: "TenantsSet",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpertApprovalNotes",
                table: "TenantsSet",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExpertApprovalStatus",
                table: "TenantsSet",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpertApprovedAt",
                table: "TenantsSet",
                type: "timestamp with time zone",
                nullable: true);

            // Backfill : écoles déjà publiques / actives restent considérées approuvées.
            migrationBuilder.Sql("""
                UPDATE "TenantsSet"
                SET "ExpertApprovalStatus" = 1,
                    "ExpertApprovedAt" = COALESCE("OnboardingCompletedAt", "CreatedAt", NOW() AT TIME ZONE 'UTC'),
                    "ExpertApprovalNotes" = 'Approuvé (migration — compte existant).'
                WHERE "IsPublicProfile" = TRUE
                   OR ("Status" = 1 AND "OnboardingCompletedAt" IS NOT NULL);
                """);

            migrationBuilder.CreateTable(
                name: "ExpertGroupsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    IsInternational = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpertGroupsSet", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeacherDocumentsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    FileUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherDocumentsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherDocumentsSet_TenantsSet_TenantId",
                        column: x => x.TenantId,
                        principalTable: "TenantsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExpertGroupMembersSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpertGroupMembersSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpertGroupMembersSet_ExpertGroupsSet_ExpertGroupId",
                        column: x => x.ExpertGroupId,
                        principalTable: "ExpertGroupsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantsSet_ApprovedByExpertGroupId",
                table: "TenantsSet",
                column: "ApprovedByExpertGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantsSet_ExpertApprovalStatus",
                table: "TenantsSet",
                column: "ExpertApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertGroupMembersSet_ExpertGroupId_UserId",
                table: "ExpertGroupMembersSet",
                columns: new[] { "ExpertGroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpertGroupsSet_CountryCode",
                table: "ExpertGroupsSet",
                column: "CountryCode",
                unique: true,
                filter: "\"IsInternational\" = FALSE AND \"CountryCode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertGroupsSet_IsInternational",
                table: "ExpertGroupsSet",
                column: "IsInternational",
                unique: true,
                filter: "\"IsInternational\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherDocumentsSet_TenantId",
                table: "TeacherDocumentsSet",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_TenantsSet_ExpertGroupsSet_ApprovedByExpertGroupId",
                table: "TenantsSet",
                column: "ApprovedByExpertGroupId",
                principalTable: "ExpertGroupsSet",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TenantsSet_ExpertGroupsSet_ApprovedByExpertGroupId",
                table: "TenantsSet");

            migrationBuilder.DropTable(
                name: "ExpertGroupMembersSet");

            migrationBuilder.DropTable(
                name: "TeacherDocumentsSet");

            migrationBuilder.DropTable(
                name: "ExpertGroupsSet");

            migrationBuilder.DropIndex(
                name: "IX_TenantsSet_ApprovedByExpertGroupId",
                table: "TenantsSet");

            migrationBuilder.DropIndex(
                name: "IX_TenantsSet_ExpertApprovalStatus",
                table: "TenantsSet");

            migrationBuilder.DropColumn(
                name: "ApprovedByExpertGroupId",
                table: "TenantsSet");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "TenantsSet");

            migrationBuilder.DropColumn(
                name: "ExpertApprovalNotes",
                table: "TenantsSet");

            migrationBuilder.DropColumn(
                name: "ExpertApprovalStatus",
                table: "TenantsSet");

            migrationBuilder.DropColumn(
                name: "ExpertApprovedAt",
                table: "TenantsSet");
        }
    }
}
