using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MeetingsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizerGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Agenda = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StartAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TimeZoneId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Visibility = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsImmediate = table.Column<bool>(type: "boolean", nullable: false),
                    WaitingRoomEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessCodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AllowMic = table.Column<bool>(type: "boolean", nullable: false),
                    AllowCamera = table.Column<bool>(type: "boolean", nullable: false),
                    AllowScreenShare = table.Column<bool>(type: "boolean", nullable: false),
                    RecordingEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    TranscriptionEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AiEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AiActivatedByOrganizer = table.Column<bool>(type: "boolean", nullable: false),
                    Language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Remind24h = table.Column<bool>(type: "boolean", nullable: false),
                    Remind1h = table.Column<bool>(type: "boolean", nullable: false),
                    Remind10m = table.Column<bool>(type: "boolean", nullable: false),
                    SendEmailInvites = table.Column<bool>(type: "boolean", nullable: false),
                    Locked = table.Column<bool>(type: "boolean", nullable: false),
                    LiveStartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MinutesShare = table.Column<int>(type: "integer", nullable: false),
                    MinutesApproved = table.Column<bool>(type: "boolean", nullable: false),
                    RetentionPolicy = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingsSet_ExpertGroupsSet_OrganizerGroupId",
                        column: x => x.OrganizerGroupId,
                        principalTable: "ExpertGroupsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MeetingActionItemsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    AssigneeUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    AssigneeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DueAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FromAi = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingActionItemsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingActionItemsSet_MeetingsSet_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "MeetingsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingAiConsentsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectKey = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Consented = table.Column<bool>(type: "boolean", nullable: false),
                    RespondedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingAiConsentsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingAiConsentsSet_MeetingsSet_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "MeetingsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingAiSummariesSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Overview = table.Column<string>(type: "text", nullable: true),
                    TopicsJson = table.Column<string>(type: "text", nullable: true),
                    OpenQuestionsJson = table.Column<string>(type: "text", nullable: true),
                    RisksJson = table.Column<string>(type: "text", nullable: true),
                    NextSteps = table.Column<string>(type: "text", nullable: true),
                    IsDraft = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingAiSummariesSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingAiSummariesSet_MeetingsSet_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "MeetingsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingAuditLogsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingAuditLogsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingAuditLogsSet_MeetingsSet_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "MeetingsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingDecisionsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FromAi = table.Column<bool>(type: "boolean", nullable: false),
                    Accepted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingDecisionsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingDecisionsSet_MeetingsSet_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "MeetingsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingExternalGuestsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EmailVerifyCodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TokenExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingExternalGuestsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingExternalGuestsSet_MeetingsSet_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "MeetingsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingFilesSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UploadedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingFilesSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingFilesSet_MeetingsSet_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "MeetingsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingGroupsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingGroupsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingGroupsSet_ExpertGroupsSet_ExpertGroupId",
                        column: x => x.ExpertGroupId,
                        principalTable: "ExpertGroupsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingGroupsSet_MeetingsSet_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "MeetingsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingInvitationsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RecipientUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    ExternalGuestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingInvitationsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingInvitationsSet_MeetingsSet_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "MeetingsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingNotificationsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    RecipientUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    RecipientEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Failed = table.Column<bool>(type: "boolean", nullable: false),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingNotificationsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingNotificationsSet_MeetingsSet_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "MeetingsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingRecordingsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingRecordingsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingRecordingsSet_MeetingsSet_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "MeetingsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingRecurrencesSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Frequency = table.Column<int>(type: "integer", nullable: false),
                    Interval = table.Column<int>(type: "integer", nullable: false),
                    ByDaysCsv = table.Column<string>(type: "text", nullable: true),
                    UntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingRecurrencesSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingRecurrencesSet_MeetingsSet_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "MeetingsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingSessionsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingSessionsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingSessionsSet_MeetingsSet_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "MeetingsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingTranscriptsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingTranscriptsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingTranscriptsSet_MeetingsSet_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "MeetingsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingParticipantsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    ExternalGuestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeftAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    HandRaised = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingParticipantsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingParticipantsSet_MeetingExternalGuestsSet_ExternalGue~",
                        column: x => x.ExternalGuestId,
                        principalTable: "MeetingExternalGuestsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MeetingParticipantsSet_MeetingsSet_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "MeetingsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingMessagesSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    SenderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingMessagesSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingMessagesSet_MeetingSessionsSet_SessionId",
                        column: x => x.SessionId,
                        principalTable: "MeetingSessionsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingAttendancesSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeftAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingAttendancesSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingAttendancesSet_MeetingParticipantsSet_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "MeetingParticipantsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingAttendancesSet_MeetingSessionsSet_SessionId",
                        column: x => x.SessionId,
                        principalTable: "MeetingSessionsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingActionItemsSet_MeetingId",
                table: "MeetingActionItemsSet",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAiConsentsSet_MeetingId_SubjectKey",
                table: "MeetingAiConsentsSet",
                columns: new[] { "MeetingId", "SubjectKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAiSummariesSet_MeetingId",
                table: "MeetingAiSummariesSet",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAttendancesSet_ParticipantId",
                table: "MeetingAttendancesSet",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAttendancesSet_SessionId",
                table: "MeetingAttendancesSet",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAuditLogsSet_MeetingId",
                table: "MeetingAuditLogsSet",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingDecisionsSet_MeetingId",
                table: "MeetingDecisionsSet",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingExternalGuestsSet_MeetingId",
                table: "MeetingExternalGuestsSet",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingExternalGuestsSet_TokenHash",
                table: "MeetingExternalGuestsSet",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingFilesSet_MeetingId",
                table: "MeetingFilesSet",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingGroupsSet_ExpertGroupId",
                table: "MeetingGroupsSet",
                column: "ExpertGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingGroupsSet_MeetingId_ExpertGroupId",
                table: "MeetingGroupsSet",
                columns: new[] { "MeetingId", "ExpertGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingInvitationsSet_MeetingId",
                table: "MeetingInvitationsSet",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMessagesSet_SessionId",
                table: "MeetingMessagesSet",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingNotificationsSet_MeetingId",
                table: "MeetingNotificationsSet",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingParticipantsSet_ExternalGuestId",
                table: "MeetingParticipantsSet",
                column: "ExternalGuestId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingParticipantsSet_MeetingId",
                table: "MeetingParticipantsSet",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingParticipantsSet_MeetingId_UserId",
                table: "MeetingParticipantsSet",
                columns: new[] { "MeetingId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingRecordingsSet_MeetingId",
                table: "MeetingRecordingsSet",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingRecurrencesSet_MeetingId",
                table: "MeetingRecurrencesSet",
                column: "MeetingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingSessionsSet_MeetingId",
                table: "MeetingSessionsSet",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingsSet_OrganizerGroupId",
                table: "MeetingsSet",
                column: "OrganizerGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingsSet_OrganizerUserId",
                table: "MeetingsSet",
                column: "OrganizerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingsSet_StartAtUtc",
                table: "MeetingsSet",
                column: "StartAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingsSet_Status",
                table: "MeetingsSet",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingTranscriptsSet_MeetingId",
                table: "MeetingTranscriptsSet",
                column: "MeetingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeetingActionItemsSet");

            migrationBuilder.DropTable(
                name: "MeetingAiConsentsSet");

            migrationBuilder.DropTable(
                name: "MeetingAiSummariesSet");

            migrationBuilder.DropTable(
                name: "MeetingAttendancesSet");

            migrationBuilder.DropTable(
                name: "MeetingAuditLogsSet");

            migrationBuilder.DropTable(
                name: "MeetingDecisionsSet");

            migrationBuilder.DropTable(
                name: "MeetingFilesSet");

            migrationBuilder.DropTable(
                name: "MeetingGroupsSet");

            migrationBuilder.DropTable(
                name: "MeetingInvitationsSet");

            migrationBuilder.DropTable(
                name: "MeetingMessagesSet");

            migrationBuilder.DropTable(
                name: "MeetingNotificationsSet");

            migrationBuilder.DropTable(
                name: "MeetingRecordingsSet");

            migrationBuilder.DropTable(
                name: "MeetingRecurrencesSet");

            migrationBuilder.DropTable(
                name: "MeetingTranscriptsSet");

            migrationBuilder.DropTable(
                name: "MeetingParticipantsSet");

            migrationBuilder.DropTable(
                name: "MeetingSessionsSet");

            migrationBuilder.DropTable(
                name: "MeetingExternalGuestsSet");

            migrationBuilder.DropTable(
                name: "MeetingsSet");
        }
    }
}
