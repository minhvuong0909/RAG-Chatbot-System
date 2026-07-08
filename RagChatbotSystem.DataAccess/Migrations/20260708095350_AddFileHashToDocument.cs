using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RagChatbotSystem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddFileHashToDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "Documents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "Documents");
        }
    }
}
