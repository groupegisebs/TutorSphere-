using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupOfferTeacherSubscriptionOfferingId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SubscriptionOfferingId",
                table: "GroupOfferTeachersSet",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupOfferTeachersSet_SubscriptionOfferingId",
                table: "GroupOfferTeachersSet",
                column: "SubscriptionOfferingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GroupOfferTeachersSet_SubscriptionOfferingId",
                table: "GroupOfferTeachersSet");

            migrationBuilder.DropColumn(
                name: "SubscriptionOfferingId",
                table: "GroupOfferTeachersSet");
        }
    }
}
