using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpertRemarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExpertRemarksSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RelatedHomeworkId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReadByTeacherAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpertRemarksSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpertRemarksSet_DocumentsSet_RelatedDocumentId",
                        column: x => x.RelatedDocumentId,
                        principalTable: "DocumentsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ExpertRemarksSet_ExpertGroupsSet_ExpertGroupId",
                        column: x => x.ExpertGroupId,
                        principalTable: "ExpertGroupsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ExpertRemarksSet_HomeworksSet_RelatedHomeworkId",
                        column: x => x.RelatedHomeworkId,
                        principalTable: "HomeworksSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ExpertRemarksSet_TenantsSet_TenantId",
                        column: x => x.TenantId,
                        principalTable: "TenantsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpertRemarksSet_CreatedAt",
                table: "ExpertRemarksSet",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertRemarksSet_ExpertGroupId",
                table: "ExpertRemarksSet",
                column: "ExpertGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertRemarksSet_RelatedDocumentId",
                table: "ExpertRemarksSet",
                column: "RelatedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertRemarksSet_RelatedHomeworkId",
                table: "ExpertRemarksSet",
                column: "RelatedHomeworkId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertRemarksSet_TenantId",
                table: "ExpertRemarksSet",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpertRemarksSet");
        }
    }
}
