using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RagChatbotSystem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddEmbeddingScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Faithfulness",
                table: "ModelComparisonResults",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Relevance",
                table: "ModelComparisonResults",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Faithfulness",
                table: "ModelComparisonResults");

            migrationBuilder.DropColumn(
                name: "Relevance",
                table: "ModelComparisonResults");
        }
    }
}
