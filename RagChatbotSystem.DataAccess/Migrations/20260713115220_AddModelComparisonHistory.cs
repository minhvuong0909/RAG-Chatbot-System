using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RagChatbotSystem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddModelComparisonHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModelComparisonRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DatasetId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Question = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RetrievedChunkCount = table.Column<int>(type: "integer", nullable: false),
                    RetrievalLatencyMs = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelComparisonRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelComparisonRuns_Datasets_DatasetId",
                        column: x => x.DatasetId,
                        principalTable: "Datasets",
                        principalColumn: "DatasetId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModelComparisonRuns_Users_RunByUserId",
                        column: x => x.RunByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModelComparisonResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelComparisonRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ModelName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Answer = table.Column<string>(type: "text", nullable: false),
                    LatencyMs = table.Column<long>(type: "bigint", nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    TotalTokens = table.Column<int>(type: "integer", nullable: false),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    QualityScore = table.Column<int>(type: "integer", nullable: true),
                    QualityReasoning = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelComparisonResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelComparisonResults_ModelComparisonRuns_ModelComparisonR~",
                        column: x => x.ModelComparisonRunId,
                        principalTable: "ModelComparisonRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelComparisonResults_ModelComparisonRunId",
                table: "ModelComparisonResults",
                column: "ModelComparisonRunId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelComparisonResults_ProviderKey_ModelComparisonRunId",
                table: "ModelComparisonResults",
                columns: new[] { "ProviderKey", "ModelComparisonRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelComparisonRuns_DatasetId_CreatedAt",
                table: "ModelComparisonRuns",
                columns: new[] { "DatasetId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelComparisonRuns_RunByUserId_CreatedAt",
                table: "ModelComparisonRuns",
                columns: new[] { "RunByUserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelComparisonResults");

            migrationBuilder.DropTable(
                name: "ModelComparisonRuns");
        }
    }
}
