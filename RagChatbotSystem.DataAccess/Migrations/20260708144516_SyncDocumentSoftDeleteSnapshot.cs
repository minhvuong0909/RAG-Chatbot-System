using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RagChatbotSystem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class SyncDocumentSoftDeleteSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_DatasetId",
                table: "Documents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Documents_DatasetId",
                table: "Documents",
                column: "DatasetId");
        }
    }
}
