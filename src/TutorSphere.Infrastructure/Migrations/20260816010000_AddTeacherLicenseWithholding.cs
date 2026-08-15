using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TutorSphere.Infrastructure.Persistence;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260816010000_AddTeacherLicenseWithholding")]
    public partial class AddTeacherLicenseWithholding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LicenseFeeWithholdingRemainingUsd",
                table: "TenantsSet",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "LicenseSettlementKind",
                table: "TenantsSet",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LicenseFeeWithholdingRemainingUsd",
                table: "TenantsSet");

            migrationBuilder.DropColumn(
                name: "LicenseSettlementKind",
                table: "TenantsSet");
        }
    }
}
