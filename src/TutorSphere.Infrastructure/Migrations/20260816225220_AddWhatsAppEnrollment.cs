using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppEnrollment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WhatsAppEnrollmentsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    PhoneE164 = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    VerificationCodeHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    VerificationSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerificationExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerificationAttempts = table.Column<int>(type: "integer", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConsentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConsentSource = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OptOutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LessonReminders = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppEnrollmentsSet", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppEnrollmentsSet_PhoneE164",
                table: "WhatsAppEnrollmentsSet",
                column: "PhoneE164");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppEnrollmentsSet_UserId",
                table: "WhatsAppEnrollmentsSet",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhatsAppEnrollmentsSet");
        }
    }
}
