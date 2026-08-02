using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingMinutesAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialMeetingPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Meetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProcessingErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProcessingErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meetings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AudioFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ByteLength = table.Column<long>(type: "bigint", nullable: false),
                    DurationMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    Sha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioFiles_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessingRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    RequestedMode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ErrorRetryable = table.Column<bool>(type: "bit", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TotalDurationMilliseconds = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessingRuns_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CleanedTranscripts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessingRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CleanedTranscripts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CleanedTranscripts_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CleanedTranscripts_ProcessingRuns_ProcessingRunId",
                        column: x => x.ProcessingRunId,
                        principalTable: "ProcessingRuns",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MeetingMinutes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessingRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DateText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingMinutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingMinutes_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MeetingMinutes_ProcessingRuns_ProcessingRunId",
                        column: x => x.ProcessingRunId,
                        principalTable: "ProcessingRuns",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProcessingStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessingRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PrimaryProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PrimaryModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActualProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActualModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FallbackUsed = table.Column<bool>(type: "bit", nullable: false),
                    FallbackReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PromptVersion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DurationMilliseconds = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessingStages_ProcessingRuns_ProcessingRunId",
                        column: x => x.ProcessingRunId,
                        principalTable: "ProcessingRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RawTranscripts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessingRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawTranscripts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RawTranscripts_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RawTranscripts_ProcessingRuns_ProcessingRunId",
                        column: x => x.ProcessingRunId,
                        principalTable: "ProcessingRuns",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CleanedTranscriptSegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CleanedTranscriptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CleanedTranscriptSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CleanedTranscriptSegments_CleanedTranscripts_CleanedTranscriptId",
                        column: x => x.CleanedTranscriptId,
                        principalTable: "CleanedTranscripts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MinutesActionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingMinutesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Task = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Assignee = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Deadline = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinutesActionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MinutesActionItems_MeetingMinutes_MeetingMinutesId",
                        column: x => x.MeetingMinutesId,
                        principalTable: "MeetingMinutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MinutesDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingMinutesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinutesDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MinutesDecisions_MeetingMinutes_MeetingMinutesId",
                        column: x => x.MeetingMinutesId,
                        principalTable: "MeetingMinutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MinutesOpenQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingMinutesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinutesOpenQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MinutesOpenQuestions_MeetingMinutes_MeetingMinutesId",
                        column: x => x.MeetingMinutesId,
                        principalTable: "MeetingMinutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MinutesParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingMinutesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinutesParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MinutesParticipants_MeetingMinutes_MeetingMinutesId",
                        column: x => x.MeetingMinutesId,
                        principalTable: "MeetingMinutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MinutesTopics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingMinutesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinutesTopics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MinutesTopics_MeetingMinutes_MeetingMinutesId",
                        column: x => x.MeetingMinutesId,
                        principalTable: "MeetingMinutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MinutesUncertainties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingMinutesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinutesUncertainties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MinutesUncertainties_MeetingMinutes_MeetingMinutesId",
                        column: x => x.MeetingMinutesId,
                        principalTable: "MeetingMinutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RawTranscriptSegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RawTranscriptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Speaker = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StartMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    EndMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawTranscriptSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RawTranscriptSegments_RawTranscripts_RawTranscriptId",
                        column: x => x.RawTranscriptId,
                        principalTable: "RawTranscripts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActionItemEvidence",
                columns: table => new
                {
                    MinutesActionItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RawTranscriptSegmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionItemEvidence", x => new { x.MinutesActionItemId, x.RawTranscriptSegmentId });
                    table.ForeignKey(
                        name: "FK_ActionItemEvidence_MinutesActionItems_MinutesActionItemId",
                        column: x => x.MinutesActionItemId,
                        principalTable: "MinutesActionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActionItemEvidence_RawTranscriptSegments_RawTranscriptSegmentId",
                        column: x => x.RawTranscriptSegmentId,
                        principalTable: "RawTranscriptSegments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CleanedSegmentSources",
                columns: table => new
                {
                    CleanedTranscriptSegmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RawTranscriptSegmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CleanedSegmentSources", x => new { x.CleanedTranscriptSegmentId, x.RawTranscriptSegmentId });
                    table.ForeignKey(
                        name: "FK_CleanedSegmentSources_CleanedTranscriptSegments_CleanedTranscriptSegmentId",
                        column: x => x.CleanedTranscriptSegmentId,
                        principalTable: "CleanedTranscriptSegments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CleanedSegmentSources_RawTranscriptSegments_RawTranscriptSegmentId",
                        column: x => x.RawTranscriptSegmentId,
                        principalTable: "RawTranscriptSegments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DecisionEvidence",
                columns: table => new
                {
                    MinutesDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RawTranscriptSegmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecisionEvidence", x => new { x.MinutesDecisionId, x.RawTranscriptSegmentId });
                    table.ForeignKey(
                        name: "FK_DecisionEvidence_MinutesDecisions_MinutesDecisionId",
                        column: x => x.MinutesDecisionId,
                        principalTable: "MinutesDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DecisionEvidence_RawTranscriptSegments_RawTranscriptSegmentId",
                        column: x => x.RawTranscriptSegmentId,
                        principalTable: "RawTranscriptSegments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OpenQuestionEvidence",
                columns: table => new
                {
                    MinutesOpenQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RawTranscriptSegmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenQuestionEvidence", x => new { x.MinutesOpenQuestionId, x.RawTranscriptSegmentId });
                    table.ForeignKey(
                        name: "FK_OpenQuestionEvidence_MinutesOpenQuestions_MinutesOpenQuestionId",
                        column: x => x.MinutesOpenQuestionId,
                        principalTable: "MinutesOpenQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OpenQuestionEvidence_RawTranscriptSegments_RawTranscriptSegmentId",
                        column: x => x.RawTranscriptSegmentId,
                        principalTable: "RawTranscriptSegments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ParticipantEvidence",
                columns: table => new
                {
                    MinutesParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RawTranscriptSegmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParticipantEvidence", x => new { x.MinutesParticipantId, x.RawTranscriptSegmentId });
                    table.ForeignKey(
                        name: "FK_ParticipantEvidence_MinutesParticipants_MinutesParticipantId",
                        column: x => x.MinutesParticipantId,
                        principalTable: "MinutesParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParticipantEvidence_RawTranscriptSegments_RawTranscriptSegmentId",
                        column: x => x.RawTranscriptSegmentId,
                        principalTable: "RawTranscriptSegments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TopicEvidence",
                columns: table => new
                {
                    MinutesTopicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RawTranscriptSegmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicEvidence", x => new { x.MinutesTopicId, x.RawTranscriptSegmentId });
                    table.ForeignKey(
                        name: "FK_TopicEvidence_MinutesTopics_MinutesTopicId",
                        column: x => x.MinutesTopicId,
                        principalTable: "MinutesTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TopicEvidence_RawTranscriptSegments_RawTranscriptSegmentId",
                        column: x => x.RawTranscriptSegmentId,
                        principalTable: "RawTranscriptSegments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UncertaintyEvidence",
                columns: table => new
                {
                    MinutesUncertaintyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RawTranscriptSegmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UncertaintyEvidence", x => new { x.MinutesUncertaintyId, x.RawTranscriptSegmentId });
                    table.ForeignKey(
                        name: "FK_UncertaintyEvidence_MinutesUncertainties_MinutesUncertaintyId",
                        column: x => x.MinutesUncertaintyId,
                        principalTable: "MinutesUncertainties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UncertaintyEvidence_RawTranscriptSegments_RawTranscriptSegmentId",
                        column: x => x.RawTranscriptSegmentId,
                        principalTable: "RawTranscriptSegments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionItemEvidence_RawTranscriptSegmentId",
                table: "ActionItemEvidence",
                column: "RawTranscriptSegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AudioFiles_MeetingId",
                table: "AudioFiles",
                column: "MeetingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AudioFiles_StorageKey",
                table: "AudioFiles",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CleanedSegmentSources_RawTranscriptSegmentId",
                table: "CleanedSegmentSources",
                column: "RawTranscriptSegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CleanedTranscripts_MeetingId",
                table: "CleanedTranscripts",
                column: "MeetingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CleanedTranscripts_ProcessingRunId",
                table: "CleanedTranscripts",
                column: "ProcessingRunId");

            migrationBuilder.CreateIndex(
                name: "IX_CleanedTranscriptSegments_CleanedTranscriptId_ExternalId",
                table: "CleanedTranscriptSegments",
                columns: new[] { "CleanedTranscriptId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CleanedTranscriptSegments_CleanedTranscriptId_Position",
                table: "CleanedTranscriptSegments",
                columns: new[] { "CleanedTranscriptId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DecisionEvidence_RawTranscriptSegmentId",
                table: "DecisionEvidence",
                column: "RawTranscriptSegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMinutes_MeetingId_Kind",
                table: "MeetingMinutes",
                columns: new[] { "MeetingId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMinutes_ProcessingRunId",
                table: "MeetingMinutes",
                column: "ProcessingRunId");

            migrationBuilder.CreateIndex(
                name: "IX_MinutesActionItems_MeetingMinutesId_Position",
                table: "MinutesActionItems",
                columns: new[] { "MeetingMinutesId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MinutesDecisions_MeetingMinutesId_Position",
                table: "MinutesDecisions",
                columns: new[] { "MeetingMinutesId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MinutesOpenQuestions_MeetingMinutesId_Position",
                table: "MinutesOpenQuestions",
                columns: new[] { "MeetingMinutesId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MinutesParticipants_MeetingMinutesId_Position",
                table: "MinutesParticipants",
                columns: new[] { "MeetingMinutesId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MinutesTopics_MeetingMinutesId_Position",
                table: "MinutesTopics",
                columns: new[] { "MeetingMinutesId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MinutesUncertainties_MeetingMinutesId_Position",
                table: "MinutesUncertainties",
                columns: new[] { "MeetingMinutesId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenQuestionEvidence_RawTranscriptSegmentId",
                table: "OpenQuestionEvidence",
                column: "RawTranscriptSegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantEvidence_RawTranscriptSegmentId",
                table: "ParticipantEvidence",
                column: "RawTranscriptSegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingRuns_CorrelationId",
                table: "ProcessingRuns",
                column: "CorrelationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingRuns_MeetingId_AttemptNumber",
                table: "ProcessingRuns",
                columns: new[] { "MeetingId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingStages_ProcessingRunId_Stage",
                table: "ProcessingStages",
                columns: new[] { "ProcessingRunId", "Stage" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RawTranscripts_MeetingId",
                table: "RawTranscripts",
                column: "MeetingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RawTranscripts_ProcessingRunId",
                table: "RawTranscripts",
                column: "ProcessingRunId");

            migrationBuilder.CreateIndex(
                name: "IX_RawTranscriptSegments_RawTranscriptId_ExternalId",
                table: "RawTranscriptSegments",
                columns: new[] { "RawTranscriptId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RawTranscriptSegments_RawTranscriptId_Position",
                table: "RawTranscriptSegments",
                columns: new[] { "RawTranscriptId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopicEvidence_RawTranscriptSegmentId",
                table: "TopicEvidence",
                column: "RawTranscriptSegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_UncertaintyEvidence_RawTranscriptSegmentId",
                table: "UncertaintyEvidence",
                column: "RawTranscriptSegmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionItemEvidence");

            migrationBuilder.DropTable(
                name: "AudioFiles");

            migrationBuilder.DropTable(
                name: "CleanedSegmentSources");

            migrationBuilder.DropTable(
                name: "DecisionEvidence");

            migrationBuilder.DropTable(
                name: "OpenQuestionEvidence");

            migrationBuilder.DropTable(
                name: "ParticipantEvidence");

            migrationBuilder.DropTable(
                name: "ProcessingStages");

            migrationBuilder.DropTable(
                name: "TopicEvidence");

            migrationBuilder.DropTable(
                name: "UncertaintyEvidence");

            migrationBuilder.DropTable(
                name: "MinutesActionItems");

            migrationBuilder.DropTable(
                name: "CleanedTranscriptSegments");

            migrationBuilder.DropTable(
                name: "MinutesDecisions");

            migrationBuilder.DropTable(
                name: "MinutesOpenQuestions");

            migrationBuilder.DropTable(
                name: "MinutesParticipants");

            migrationBuilder.DropTable(
                name: "MinutesTopics");

            migrationBuilder.DropTable(
                name: "MinutesUncertainties");

            migrationBuilder.DropTable(
                name: "RawTranscriptSegments");

            migrationBuilder.DropTable(
                name: "CleanedTranscripts");

            migrationBuilder.DropTable(
                name: "MeetingMinutes");

            migrationBuilder.DropTable(
                name: "RawTranscripts");

            migrationBuilder.DropTable(
                name: "ProcessingRuns");

            migrationBuilder.DropTable(
                name: "Meetings");
        }
    }
}
