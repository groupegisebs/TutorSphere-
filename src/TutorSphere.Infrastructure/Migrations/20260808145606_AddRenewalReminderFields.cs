using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRenewalReminderFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LicenseRenewalReminderSentAt",
                table: "TenantsSet",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RenewalReminderSentAt",
                table: "StudentSubscriptionsSet",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LicenseRenewalReminderSentAt",
                table: "TenantsSet");

            migrationBuilder.DropColumn(
                name: "RenewalReminderSentAt",
                table: "StudentSubscriptionsSet");
        }
    }
}
