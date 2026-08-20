using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeacherContractsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    PlaceholdersJson = table.Column<string>(type: "text", nullable: false),
                    SignToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TokenInvalidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ViewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefusedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefusedSectionKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RefusalComment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TeacherTypedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SignaturePngBase64 = table.Column<string>(type: "text", nullable: true),
                    TeacherIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TeacherUserAgent = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    PdfUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DocumentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    VerificationCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ReplacedByContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReplacesContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherContractsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherContractsSet_ExpertGroupsSet_ExpertGroupId",
                        column: x => x.ExpertGroupId,
                        principalTable: "ExpertGroupsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherContractsSet_TenantsSet_TenantId",
                        column: x => x.TenantId,
                        principalTable: "TenantsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeacherContractAuditEventsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    ActorUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DetailsJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherContractAuditEventsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherContractAuditEventsSet_TeacherContractsSet_ContractId",
                        column: x => x.ContractId,
                        principalTable: "TeacherContractsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeacherContractSectionDecisionsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Accepted = table.Column<bool>(type: "boolean", nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherContractSectionDecisionsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherContractSectionDecisionsSet_TeacherContractsSet_Cont~",
                        column: x => x.ContractId,
                        principalTable: "TeacherContractsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherContractAuditEventsSet_ContractId",
                table: "TeacherContractAuditEventsSet",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherContractAuditEventsSet_CreatedAt",
                table: "TeacherContractAuditEventsSet",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherContractSectionDecisionsSet_ContractId_SectionKey",
                table: "TeacherContractSectionDecisionsSet",
                columns: new[] { "ContractId", "SectionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherContractsSet_ContractNumber",
                table: "TeacherContractsSet",
                column: "ContractNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherContractsSet_ExpertGroupId",
                table: "TeacherContractsSet",
                column: "ExpertGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherContractsSet_SignToken",
                table: "TeacherContractsSet",
                column: "SignToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherContractsSet_Status",
                table: "TeacherContractsSet",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherContractsSet_TenantId",
                table: "TeacherContractsSet",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeacherContractAuditEventsSet");

            migrationBuilder.DropTable(
                name: "TeacherContractSectionDecisionsSet");

            migrationBuilder.DropTable(
                name: "TeacherContractsSet");
        }
    }
}
