using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformAnnualLicense : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LicenseExpiresAt",
                table: "TenantsSet",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlatformLicensePaymentsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    GatewayPaymentCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformLicensePaymentsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformLicensePaymentsSet_TenantsSet_TenantId",
                        column: x => x.TenantId,
                        principalTable: "TenantsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformLicensePaymentsSet_GatewayPaymentCode",
                table: "PlatformLicensePaymentsSet",
                column: "GatewayPaymentCode");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformLicensePaymentsSet_TenantId",
                table: "PlatformLicensePaymentsSet",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformLicensePaymentsSet");

            migrationBuilder.DropColumn(
                name: "LicenseExpiresAt",
                table: "TenantsSet");
        }
    }
}
