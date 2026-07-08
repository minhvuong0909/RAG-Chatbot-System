using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RagChatbotSystem.DataAccess.Data;

#nullable disable

namespace RagChatbotSystem.DataAccess.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260708000000_AddDocumentSoftDeleteAndHash")]
    public partial class AddDocumentSoftDeleteAndHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "Documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "Documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ProcessError",
                table: "Documents",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DatasetId_FileHash",
                table: "Documents",
                columns: new[] { "DatasetId", "FileHash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_DatasetId_FileHash",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ProcessError",
                table: "Documents");
        }
    }
}
