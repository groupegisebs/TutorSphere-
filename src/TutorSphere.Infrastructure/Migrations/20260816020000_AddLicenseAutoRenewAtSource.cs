using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLicenseAutoRenewAtSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LicenseAutoRenewAtSource",
                table: "TenantsSet",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LicenseAutoRenewAtSource",
                table: "TenantsSet");
        }
    }
}
