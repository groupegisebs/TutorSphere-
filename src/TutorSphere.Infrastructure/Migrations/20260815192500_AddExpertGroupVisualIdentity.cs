using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TutorSphere.Infrastructure.Persistence;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260815192500_AddExpertGroupVisualIdentity")]
    public partial class AddExpertGroupVisualIdentity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BannerUrl",
                table: "ExpertGroupsSet",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryColor",
                table: "ExpertGroupsSet",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryColor",
                table: "ExpertGroupsSet",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "BannerUrl", table: "ExpertGroupsSet");
            migrationBuilder.DropColumn(name: "PrimaryColor", table: "ExpertGroupsSet");
            migrationBuilder.DropColumn(name: "SecondaryColor", table: "ExpertGroupsSet");
        }
    }
}
