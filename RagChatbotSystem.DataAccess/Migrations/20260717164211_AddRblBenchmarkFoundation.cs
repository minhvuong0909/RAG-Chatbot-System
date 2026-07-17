using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RagChatbotSystem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddRblBenchmarkFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BenchmarkDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DatasetId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenchmarkDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BenchmarkDefinitions_Datasets_DatasetId",
                        column: x => x.DatasetId,
                        principalTable: "Datasets",
                        principalColumn: "DatasetId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BenchmarkDefinitions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ChunkingStrategy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ChunkSize = table.Column<int>(type: "integer", nullable: false),
                    ChunkOverlap = table.Column<int>(type: "integer", nullable: false),
                    EmbeddingModel = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TopK = table.Column<int>(type: "integer", nullable: false),
                    SemanticWeight = table.Column<double>(type: "double precision", nullable: false),
                    LexicalWeight = table.Column<double>(type: "double precision", nullable: false),
                    EnableRerank = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BenchmarkQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BenchmarkDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Question = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReferenceAnswer = table.Column<string>(type: "text", nullable: false),
                    EvidenceNote = table.Column<string>(type: "text", nullable: true),
                    RelevantChunkIdsJson = table.Column<string>(type: "jsonb", nullable: true),
                    SourceReference = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsHoldout = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenchmarkQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BenchmarkQuestions_BenchmarkDefinitions_BenchmarkDefinition~",
                        column: x => x.BenchmarkDefinitionId,
                        principalTable: "BenchmarkDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DatasetId = table.Column<Guid>(type: "uuid", nullable: false),
                    BenchmarkDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluationProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ModelName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TotalQuestions = table.Column<int>(type: "integer", nullable: false),
                    CompletedQuestions = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationRuns_BenchmarkDefinitions_BenchmarkDefinitionId",
                        column: x => x.BenchmarkDefinitionId,
                        principalTable: "BenchmarkDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationRuns_Datasets_DatasetId",
                        column: x => x.DatasetId,
                        principalTable: "Datasets",
                        principalColumn: "DatasetId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvaluationRuns_EvaluationProfiles_EvaluationProfileId",
                        column: x => x.EvaluationProfileId,
                        principalTable: "EvaluationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationRuns_Users_RunByUserId",
                        column: x => x.RunByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluationRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    BenchmarkQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Answer = table.Column<string>(type: "text", nullable: false),
                    RetrievalLatencyMs = table.Column<long>(type: "bigint", nullable: false),
                    GenerationLatencyMs = table.Column<long>(type: "bigint", nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    TotalTokens = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Faithfulness = table.Column<double>(type: "double precision", nullable: true),
                    AnswerRelevancy = table.Column<double>(type: "double precision", nullable: true),
                    ContextPrecision = table.Column<double>(type: "double precision", nullable: true),
                    ContextRecall = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationResults_BenchmarkQuestions_BenchmarkQuestionId",
                        column: x => x.BenchmarkQuestionId,
                        principalTable: "BenchmarkQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationResults_EvaluationRuns_EvaluationRunId",
                        column: x => x.EvaluationRunId,
                        principalTable: "EvaluationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluationResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChunkId = table.Column<Guid>(type: "uuid", nullable: true),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: true),
                    Content = table.Column<string>(type: "text", nullable: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationEvidence_Chunks_ChunkId",
                        column: x => x.ChunkId,
                        principalTable: "Chunks",
                        principalColumn: "ChunkId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EvaluationEvidence_EvaluationResults_EvaluationResultId",
                        column: x => x.EvaluationResultId,
                        principalTable: "EvaluationResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkDefinitions_CreatedByUserId",
                table: "BenchmarkDefinitions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkDefinitions_DatasetId_Version",
                table: "BenchmarkDefinitions",
                columns: new[] { "DatasetId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkQuestions_BenchmarkDefinitionId_SortOrder",
                table: "BenchmarkQuestions",
                columns: new[] { "BenchmarkDefinitionId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationEvidence_ChunkId",
                table: "EvaluationEvidence",
                column: "ChunkId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationEvidence_EvaluationResultId_Rank",
                table: "EvaluationEvidence",
                columns: new[] { "EvaluationResultId", "Rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationProfiles_Slug",
                table: "EvaluationProfiles",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationResults_BenchmarkQuestionId",
                table: "EvaluationResults",
                column: "BenchmarkQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationResults_EvaluationRunId_BenchmarkQuestionId",
                table: "EvaluationResults",
                columns: new[] { "EvaluationRunId", "BenchmarkQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationRuns_BenchmarkDefinitionId_EvaluationProfileId_Cr~",
                table: "EvaluationRuns",
                columns: new[] { "BenchmarkDefinitionId", "EvaluationProfileId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationRuns_DatasetId_CreatedAt",
                table: "EvaluationRuns",
                columns: new[] { "DatasetId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationRuns_EvaluationProfileId",
                table: "EvaluationRuns",
                column: "EvaluationProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationRuns_RunByUserId",
                table: "EvaluationRuns",
                column: "RunByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvaluationEvidence");

            migrationBuilder.DropTable(
                name: "EvaluationResults");

            migrationBuilder.DropTable(
                name: "BenchmarkQuestions");

            migrationBuilder.DropTable(
                name: "EvaluationRuns");

            migrationBuilder.DropTable(
                name: "BenchmarkDefinitions");

            migrationBuilder.DropTable(
                name: "EvaluationProfiles");
        }
    }
}
