using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDisciplines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DisciplinesSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Cycle = table.Column<int>(type: "integer", nullable: false),
                    WorkMethod = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisciplinesSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisciplinesSet_ExpertGroupsSet_ExpertGroupId",
                        column: x => x.ExpertGroupId,
                        principalTable: "ExpertGroupsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DisciplineServiceItemsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisciplineId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisciplineServiceItemsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisciplineServiceItemsSet_DisciplinesSet_DisciplineId",
                        column: x => x.DisciplineId,
                        principalTable: "DisciplinesSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeacherDisciplineAssignmentsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisciplineId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherDisciplineAssignmentsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherDisciplineAssignmentsSet_DisciplinesSet_DisciplineId",
                        column: x => x.DisciplineId,
                        principalTable: "DisciplinesSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeacherDisciplineAssignmentsSet_TenantsSet_TenantId",
                        column: x => x.TenantId,
                        principalTable: "TenantsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DisciplineServiceItemsSet_DisciplineId",
                table: "DisciplineServiceItemsSet",
                column: "DisciplineId");

            migrationBuilder.CreateIndex(
                name: "IX_DisciplinesSet_ExpertGroupId",
                table: "DisciplinesSet",
                column: "ExpertGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_DisciplinesSet_ExpertGroupId_Name",
                table: "DisciplinesSet",
                columns: new[] { "ExpertGroupId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherDisciplineAssignmentsSet_DisciplineId_TenantId",
                table: "TeacherDisciplineAssignmentsSet",
                columns: new[] { "DisciplineId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherDisciplineAssignmentsSet_TenantId",
                table: "TeacherDisciplineAssignmentsSet",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DisciplineServiceItemsSet");

            migrationBuilder.DropTable(
                name: "TeacherDisciplineAssignmentsSet");

            migrationBuilder.DropTable(
                name: "DisciplinesSet");
        }
    }
}
