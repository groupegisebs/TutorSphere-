using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorPayoutDisbursementFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalDisbursementId",
                table: "TutorPayoutsSet",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureMessage",
                table: "TutorPayoutsSet",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "TutorPayoutsSet",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderPayoutId",
                table: "TutorPayoutsSet",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutorPayoutsSet_IdempotencyKey",
                table: "TutorPayoutsSet",
                column: "IdempotencyKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TutorPayoutsSet_IdempotencyKey",
                table: "TutorPayoutsSet");

            migrationBuilder.DropColumn(
                name: "ExternalDisbursementId",
                table: "TutorPayoutsSet");

            migrationBuilder.DropColumn(
                name: "FailureMessage",
                table: "TutorPayoutsSet");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "TutorPayoutsSet");

            migrationBuilder.DropColumn(
                name: "ProviderPayoutId",
                table: "TutorPayoutsSet");
        }
    }
}
