using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddParentMailboxFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StudentId",
                table: "MessagesSet",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentChannel",
                table: "MessagesSet",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentReason",
                table: "MessagesSet",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CaseNumber",
                table: "MessagesSet",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentType",
                table: "MessagesSet",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AttachmentId",
                table: "MessagesSet",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentLabel",
                table: "MessagesSet",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessagesSet_ParentChannel_StudentId",
                table: "MessagesSet",
                columns: new[] { "ParentChannel", "StudentId" });

            migrationBuilder.AddColumn<string>(
                name: "CaseNumber",
                table: "ParentSupportRequestsSet",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StudentId",
                table: "ParentSupportRequestsSet",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "ParentSupportRequestsSet",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParentSupportRequestsSet_CaseNumber",
                table: "ParentSupportRequestsSet",
                column: "CaseNumber",
                unique: true,
                filter: "\"CaseNumber\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MessagesSet_ParentChannel_StudentId",
                table: "MessagesSet");

            migrationBuilder.DropIndex(
                name: "IX_ParentSupportRequestsSet_CaseNumber",
                table: "ParentSupportRequestsSet");

            migrationBuilder.DropColumn(name: "StudentId", table: "MessagesSet");
            migrationBuilder.DropColumn(name: "ParentChannel", table: "MessagesSet");
            migrationBuilder.DropColumn(name: "ParentReason", table: "MessagesSet");
            migrationBuilder.DropColumn(name: "CaseNumber", table: "MessagesSet");
            migrationBuilder.DropColumn(name: "AttachmentType", table: "MessagesSet");
            migrationBuilder.DropColumn(name: "AttachmentId", table: "MessagesSet");
            migrationBuilder.DropColumn(name: "AttachmentLabel", table: "MessagesSet");
            migrationBuilder.DropColumn(name: "CaseNumber", table: "ParentSupportRequestsSet");
            migrationBuilder.DropColumn(name: "StudentId", table: "ParentSupportRequestsSet");
            migrationBuilder.DropColumn(name: "Reason", table: "ParentSupportRequestsSet");
        }
    }
}
