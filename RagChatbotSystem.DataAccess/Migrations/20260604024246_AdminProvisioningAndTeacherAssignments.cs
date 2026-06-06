using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RagChatbotSystem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AdminProvisioningAndTeacherAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DatasetPermissions_DatasetId",
                table: "DatasetPermissions");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByAdminId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPasswordChangedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "TemporaryPasswordExpiresAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                WITH normalized AS (
                    SELECT
                        "UserId",
                        COALESCE(
                            NULLIF(lower(regexp_replace(split_part("Email", '@', 1), '[^a-z0-9._-]', '', 'g')), ''),
                            'user'
                        ) AS base_username,
                        row_number() OVER (
                            PARTITION BY COALESCE(
                                NULLIF(lower(regexp_replace(split_part("Email", '@', 1), '[^a-z0-9._-]', '', 'g')), ''),
                                'user'
                            )
                            ORDER BY "CreatedAt", "UserId"
                        ) AS duplicate_index
                    FROM "Users"
                )
                UPDATE "Users" AS u
                SET "Username" = CASE
                    WHEN n.duplicate_index = 1 THEN n.base_username
                    ELSE n.base_username || n.duplicate_index::text
                END
                FROM normalized AS n
                WHERE u."UserId" = n."UserId";
                """);

            migrationBuilder.CreateTable(
                name: "TeacherSubjectAssignments",
                columns: table => new
                {
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    DatasetId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherSubjectAssignments", x => x.AssignmentId);
                    table.ForeignKey(
                        name: "FK_TeacherSubjectAssignments_Datasets_DatasetId",
                        column: x => x.DatasetId,
                        principalTable: "Datasets",
                        principalColumn: "DatasetId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeacherSubjectAssignments_Users_AssignedBy",
                        column: x => x.AssignedBy,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherSubjectAssignments_Users_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DatasetPermissions_DatasetId_UserId",
                table: "DatasetPermissions",
                columns: new[] { "DatasetId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherSubjectAssignments_AssignedBy",
                table: "TeacherSubjectAssignments",
                column: "AssignedBy");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherSubjectAssignments_DatasetId",
                table: "TeacherSubjectAssignments",
                column: "DatasetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherSubjectAssignments_TeacherId_DatasetId",
                table: "TeacherSubjectAssignments",
                columns: new[] { "TeacherId", "DatasetId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeacherSubjectAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_DatasetPermissions_DatasetId_UserId",
                table: "DatasetPermissions");

            migrationBuilder.DropColumn(
                name: "CreatedByAdminId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastLoginAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastPasswordChangedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TemporaryPasswordExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_DatasetPermissions_DatasetId",
                table: "DatasetPermissions",
                column: "DatasetId");
        }
    }
}
