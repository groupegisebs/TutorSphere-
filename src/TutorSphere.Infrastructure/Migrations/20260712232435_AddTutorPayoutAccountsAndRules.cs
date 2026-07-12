using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorPayoutAccountsAndRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PayoutAccountId",
                table: "TutorPayoutsSet",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProviderKind",
                table: "TutorPayoutsSet",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayPalEmail",
                table: "TenantsSet",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PayoutHoldingStartedAt",
                table: "TenantsSet",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TutorPayoutAccountsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    ProviderKind = table.Column<int>(type: "integer", nullable: false),
                    CountryCode = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AccountHolderName = table.Column<string>(type: "text", nullable: false),
                    EmailOrAccountId = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PaymentDetails = table.Column<string>(type: "text", nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorPayoutAccountsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorPayoutAccountsSet_TenantsSet_TenantId",
                        column: x => x.TenantId,
                        principalTable: "TenantsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TutorPayoutsSet_PayoutAccountId",
                table: "TutorPayoutsSet",
                column: "PayoutAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorPayoutAccountsSet_TenantId",
                table: "TutorPayoutAccountsSet",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorPayoutAccountsSet_TenantId_IsPrimary",
                table: "TutorPayoutAccountsSet",
                columns: new[] { "TenantId", "IsPrimary" });

            migrationBuilder.AddForeignKey(
                name: "FK_TutorPayoutsSet_TutorPayoutAccountsSet_PayoutAccountId",
                table: "TutorPayoutsSet",
                column: "PayoutAccountId",
                principalTable: "TutorPayoutAccountsSet",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TutorPayoutsSet_TutorPayoutAccountsSet_PayoutAccountId",
                table: "TutorPayoutsSet");

            migrationBuilder.DropTable(
                name: "TutorPayoutAccountsSet");

            migrationBuilder.DropIndex(
                name: "IX_TutorPayoutsSet_PayoutAccountId",
                table: "TutorPayoutsSet");

            migrationBuilder.DropColumn(
                name: "PayoutAccountId",
                table: "TutorPayoutsSet");

            migrationBuilder.DropColumn(
                name: "ProviderKind",
                table: "TutorPayoutsSet");

            migrationBuilder.DropColumn(
                name: "PayPalEmail",
                table: "TenantsSet");

            migrationBuilder.DropColumn(
                name: "PayoutHoldingStartedAt",
                table: "TenantsSet");
        }
    }
}
