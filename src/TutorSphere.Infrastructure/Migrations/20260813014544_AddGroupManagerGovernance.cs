using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupManagerGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActiveManagerMandateId",
                table: "ExpertGroupsSet",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ExpertGroupsSet",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LifecycleStatus",
                table: "ExpertGroupsSet",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndedAtUtc",
                table: "ExpertGroupMembersSet",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MemberRole",
                table: "ExpertGroupMembersSet",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ExpertGroupManagerMandatesSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FunctionTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MandateStartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MandateEndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppointedByAdminId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    EndedByAdminId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    EndReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsTemporary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpertGroupManagerMandatesSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpertGroupManagerMandatesSet_ExpertGroupMembersSet_Members~",
                        column: x => x.MembershipId,
                        principalTable: "ExpertGroupMembersSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExpertGroupManagerMandatesSet_ExpertGroupsSet_ExpertGroupId",
                        column: x => x.ExpertGroupId,
                        principalTable: "ExpertGroupsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupAdminConversationsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByManagerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    AssignedAdminUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupAdminConversationsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupAdminConversationsSet_ExpertGroupsSet_ExpertGroupId",
                        column: x => x.ExpertGroupId,
                        principalTable: "ExpertGroupsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupOffersSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisciplineId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ShortDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FullDescription = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SchoolCycle = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LevelsCsv = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LanguagesCsv = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    VisibleCountryCodes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PricingModel = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    FixedPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    MinimumPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    RecommendedPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    MaximumPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ApprovedByManagerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupOffersSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupOffersSet_DisciplinesSet_DisciplineId",
                        column: x => x.DisciplineId,
                        principalTable: "DisciplinesSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GroupOffersSet_ExpertGroupsSet_ExpertGroupId",
                        column: x => x.ExpertGroupId,
                        principalTable: "ExpertGroupsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeacherInterestRequestsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Disciplines = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Experience = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RoutedExpertGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    HandledByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    TeacherInviteId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherInterestRequestsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherInterestRequestsSet_ExpertGroupsSet_RoutedExpertGrou~",
                        column: x => x.RoutedExpertGroupId,
                        principalTable: "ExpertGroupsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GroupAdminMessagesSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Body = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    AttachmentReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EditedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PreviousBody = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupAdminMessagesSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupAdminMessagesSet_GroupAdminConversationsSet_Conversati~",
                        column: x => x.ConversationId,
                        principalTable: "GroupAdminConversationsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupOfferTeachersSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupOfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentStatus = table.Column<int>(type: "integer", nullable: false),
                    TeacherPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    Capacity = table.Column<int>(type: "integer", nullable: true),
                    AvailableFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupOfferTeachersSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupOfferTeachersSet_GroupOffersSet_GroupOfferId",
                        column: x => x.GroupOfferId,
                        principalTable: "GroupOffersSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupOfferTeachersSet_TenantsSet_TeacherTenantId",
                        column: x => x.TeacherTenantId,
                        principalTable: "TenantsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpertGroupsSet_ActiveManagerMandateId",
                table: "ExpertGroupsSet",
                column: "ActiveManagerMandateId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertGroupMembersSet_ExpertGroupId_MemberRole",
                table: "ExpertGroupMembersSet",
                columns: new[] { "ExpertGroupId", "MemberRole" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpertGroupManagerMandatesSet_ExpertGroupId",
                table: "ExpertGroupManagerMandatesSet",
                column: "ExpertGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertGroupManagerMandatesSet_ExpertGroupId_Status",
                table: "ExpertGroupManagerMandatesSet",
                columns: new[] { "ExpertGroupId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpertGroupManagerMandatesSet_MembershipId",
                table: "ExpertGroupManagerMandatesSet",
                column: "MembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupAdminConversationsSet_ExpertGroupId",
                table: "GroupAdminConversationsSet",
                column: "ExpertGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupAdminConversationsSet_Reference",
                table: "GroupAdminConversationsSet",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupAdminConversationsSet_Status",
                table: "GroupAdminConversationsSet",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GroupAdminMessagesSet_ConversationId",
                table: "GroupAdminMessagesSet",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupOffersSet_DisciplineId",
                table: "GroupOffersSet",
                column: "DisciplineId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupOffersSet_ExpertGroupId",
                table: "GroupOffersSet",
                column: "ExpertGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupOffersSet_ExpertGroupId_Status",
                table: "GroupOffersSet",
                columns: new[] { "ExpertGroupId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupOfferTeachersSet_GroupOfferId_TeacherTenantId",
                table: "GroupOfferTeachersSet",
                columns: new[] { "GroupOfferId", "TeacherTenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupOfferTeachersSet_TeacherTenantId",
                table: "GroupOfferTeachersSet",
                column: "TeacherTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherInterestRequestsSet_Email",
                table: "TeacherInterestRequestsSet",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherInterestRequestsSet_RoutedExpertGroupId",
                table: "TeacherInterestRequestsSet",
                column: "RoutedExpertGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherInterestRequestsSet_Status",
                table: "TeacherInterestRequestsSet",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpertGroupManagerMandatesSet");

            migrationBuilder.DropTable(
                name: "GroupAdminMessagesSet");

            migrationBuilder.DropTable(
                name: "GroupOfferTeachersSet");

            migrationBuilder.DropTable(
                name: "TeacherInterestRequestsSet");

            migrationBuilder.DropTable(
                name: "GroupAdminConversationsSet");

            migrationBuilder.DropTable(
                name: "GroupOffersSet");

            migrationBuilder.DropIndex(
                name: "IX_ExpertGroupsSet_ActiveManagerMandateId",
                table: "ExpertGroupsSet");

            migrationBuilder.DropIndex(
                name: "IX_ExpertGroupMembersSet_ExpertGroupId_MemberRole",
                table: "ExpertGroupMembersSet");

            migrationBuilder.DropColumn(
                name: "ActiveManagerMandateId",
                table: "ExpertGroupsSet");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ExpertGroupsSet");

            migrationBuilder.DropColumn(
                name: "LifecycleStatus",
                table: "ExpertGroupsSet");

            migrationBuilder.DropColumn(
                name: "EndedAtUtc",
                table: "ExpertGroupMembersSet");

            migrationBuilder.DropColumn(
                name: "MemberRole",
                table: "ExpertGroupMembersSet");
        }
    }
}
