using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminMailboxFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Subject",
                table: "MessagesSet",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "SenderUserId",
                table: "MessagesSet",
                type: "character varying(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "RecipientUserId",
                table: "MessagesSet",
                type: "character varying(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Body",
                table: "MessagesSet",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "EmailError",
                table: "MessagesSet",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmailSent",
                table: "MessagesSet",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailSentAt",
                table: "MessagesSet",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalRecipientEmail",
                table: "MessagesSet",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InReplyToMessageId",
                table: "MessagesSet",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDraft",
                table: "MessagesSet",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsStarred",
                table: "MessagesSet",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RecipientArchived",
                table: "MessagesSet",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RecipientDeleted",
                table: "MessagesSet",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SenderArchived",
                table: "MessagesSet",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SenderDeleted",
                table: "MessagesSet",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_MessagesSet_CreatedAt",
                table: "MessagesSet",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MessagesSet_RecipientUserId",
                table: "MessagesSet",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessagesSet_RecipientUserId_IsRead",
                table: "MessagesSet",
                columns: new[] { "RecipientUserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_MessagesSet_SenderUserId",
                table: "MessagesSet",
                column: "SenderUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MessagesSet_CreatedAt",
                table: "MessagesSet");

            migrationBuilder.DropIndex(
                name: "IX_MessagesSet_RecipientUserId",
                table: "MessagesSet");

            migrationBuilder.DropIndex(
                name: "IX_MessagesSet_RecipientUserId_IsRead",
                table: "MessagesSet");

            migrationBuilder.DropIndex(
                name: "IX_MessagesSet_SenderUserId",
                table: "MessagesSet");

            migrationBuilder.DropColumn(
                name: "EmailError",
                table: "MessagesSet");

            migrationBuilder.DropColumn(
                name: "EmailSent",
                table: "MessagesSet");

            migrationBuilder.DropColumn(
                name: "EmailSentAt",
                table: "MessagesSet");

            migrationBuilder.DropColumn(
                name: "ExternalRecipientEmail",
                table: "MessagesSet");

            migrationBuilder.DropColumn(
                name: "InReplyToMessageId",
                table: "MessagesSet");

            migrationBuilder.DropColumn(
                name: "IsDraft",
                table: "MessagesSet");

            migrationBuilder.DropColumn(
                name: "IsStarred",
                table: "MessagesSet");

            migrationBuilder.DropColumn(
                name: "RecipientArchived",
                table: "MessagesSet");

            migrationBuilder.DropColumn(
                name: "RecipientDeleted",
                table: "MessagesSet");

            migrationBuilder.DropColumn(
                name: "SenderArchived",
                table: "MessagesSet");

            migrationBuilder.DropColumn(
                name: "SenderDeleted",
                table: "MessagesSet");

            migrationBuilder.AlterColumn<string>(
                name: "Subject",
                table: "MessagesSet",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "SenderUserId",
                table: "MessagesSet",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(450)",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<string>(
                name: "RecipientUserId",
                table: "MessagesSet",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(450)",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<string>(
                name: "Body",
                table: "MessagesSet",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(8000)",
                oldMaxLength: 8000);
        }
    }
}
