using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RagChatbotSystem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreditOutputTokenWeight",
                table: "SystemSettings",
                type: "integer",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<int>(
                name: "CreditTokenUnit",
                table: "SystemSettings",
                type: "integer",
                nullable: false,
                defaultValue: 1000);

            migrationBuilder.AddColumn<int>(
                name: "DailyFreeCredits",
                table: "SystemSettings",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<bool>(
                name: "EnableCreditSystem",
                table: "SystemSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "ExamSeasonDailyFreeCredits",
                table: "SystemSettings",
                type: "integer",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.CreateTable(
                name: "CreditBlockedAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DatasetId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChatSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FreeCreditsAtTime = table.Column<int>(type: "integer", nullable: false),
                    PaidCreditsAtTime = table.Column<int>(type: "integer", nullable: false),
                    UsedTokensToday = table.Column<int>(type: "integer", nullable: true),
                    DailyTokenLimit = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MessagePreview = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditBlockedAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditBlockedAttempts_ChatSessions_ChatSessionId",
                        column: x => x.ChatSessionId,
                        principalTable: "ChatSessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CreditBlockedAttempts_Datasets_DatasetId",
                        column: x => x.DatasetId,
                        principalTable: "Datasets",
                        principalColumn: "DatasetId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CreditBlockedAttempts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreditPackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BaseCredits = table.Column<int>(type: "integer", nullable: false),
                    BonusCredits = table.Column<int>(type: "integer", nullable: false),
                    TotalCredits = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    ValidDays = table.Column<int>(type: "integer", nullable: true),
                    ExpiresAfterDays = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditPackages", x => x.Id);
                    table.CheckConstraint("CK_CreditPackage_BaseCredits_Positive", "\"BaseCredits\" > 0");
                    table.CheckConstraint("CK_CreditPackage_BonusCredits_NonNegative", "\"BonusCredits\" >= 0");
                    table.CheckConstraint("CK_CreditPackage_Price_NonNegative", "\"Price\" >= 0");
                    table.CheckConstraint("CK_CreditPackage_TotalCredits_Positive", "\"TotalCredits\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "CreditWallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FreeCredits = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PaidCredits = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastFreeCreditResetDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditWallets", x => x.Id);
                    table.CheckConstraint("CK_CreditWallet_FreeCredits_NonNegative", "\"FreeCredits\" >= 0");
                    table.CheckConstraint("CK_CreditWallet_PaidCredits_NonNegative", "\"PaidCredits\" >= 0");
                    table.ForeignKey(
                        name: "FK_CreditWallets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreditLedgers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DatasetId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChatSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChatMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CalculatedCredits = table.Column<int>(type: "integer", nullable: false),
                    ChargedCredits = table.Column<int>(type: "integer", nullable: false),
                    FreeCreditsUsed = table.Column<int>(type: "integer", nullable: false),
                    PaidCreditsUsed = table.Column<int>(type: "integer", nullable: false),
                    FreeCreditsAdded = table.Column<int>(type: "integer", nullable: false),
                    PaidCreditsAdded = table.Column<int>(type: "integer", nullable: false),
                    BalanceBeforeFree = table.Column<int>(type: "integer", nullable: false),
                    BalanceBeforePaid = table.Column<int>(type: "integer", nullable: false),
                    BalanceAfterFree = table.Column<int>(type: "integer", nullable: false),
                    BalanceAfterPaid = table.Column<int>(type: "integer", nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    TotalTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokenWeight = table.Column<int>(type: "integer", nullable: false),
                    TokenUnit = table.Column<int>(type: "integer", nullable: false),
                    ModelName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    WasActualTokenUsage = table.Column<bool>(type: "boolean", nullable: false),
                    WasInsufficientBalance = table.Column<bool>(type: "boolean", nullable: false),
                    RelatedPackageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditLedgers", x => x.Id);
                    table.CheckConstraint("CK_CreditLedger_BalanceAfterFree_NonNegative", "\"BalanceAfterFree\" >= 0");
                    table.CheckConstraint("CK_CreditLedger_BalanceAfterPaid_NonNegative", "\"BalanceAfterPaid\" >= 0");
                    table.CheckConstraint("CK_CreditLedger_BalanceBeforeFree_NonNegative", "\"BalanceBeforeFree\" >= 0");
                    table.CheckConstraint("CK_CreditLedger_BalanceBeforePaid_NonNegative", "\"BalanceBeforePaid\" >= 0");
                    table.CheckConstraint("CK_CreditLedger_CalculatedCredits_NonNegative", "\"CalculatedCredits\" >= 0");
                    table.CheckConstraint("CK_CreditLedger_ChargedCredits_NonNegative", "\"ChargedCredits\" >= 0");
                    table.CheckConstraint("CK_CreditLedger_FreeCreditsAdded_NonNegative", "\"FreeCreditsAdded\" >= 0");
                    table.CheckConstraint("CK_CreditLedger_FreeCreditsUsed_NonNegative", "\"FreeCreditsUsed\" >= 0");
                    table.CheckConstraint("CK_CreditLedger_PaidCreditsAdded_NonNegative", "\"PaidCreditsAdded\" >= 0");
                    table.CheckConstraint("CK_CreditLedger_PaidCreditsUsed_NonNegative", "\"PaidCreditsUsed\" >= 0");
                    table.CheckConstraint("CK_CreditLedger_Tokens_NonNegative", "\"InputTokens\" >= 0 AND \"OutputTokens\" >= 0 AND \"TotalTokens\" >= 0");
                    table.ForeignKey(
                        name: "FK_CreditLedgers_ChatMessages_ChatMessageId",
                        column: x => x.ChatMessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "MessageId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CreditLedgers_ChatSessions_ChatSessionId",
                        column: x => x.ChatSessionId,
                        principalTable: "ChatSessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CreditLedgers_CreditPackages_RelatedPackageId",
                        column: x => x.RelatedPackageId,
                        principalTable: "CreditPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CreditLedgers_Datasets_DatasetId",
                        column: x => x.DatasetId,
                        principalTable: "Datasets",
                        principalColumn: "DatasetId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CreditLedgers_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CreditLedgers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreditPurchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: true),
                    BaseCredits = table.Column<int>(type: "integer", nullable: false),
                    BonusCredits = table.Column<int>(type: "integer", nullable: false),
                    TotalCredits = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PaymentProvider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ProviderReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditPurchases", x => x.Id);
                    table.CheckConstraint("CK_CreditPurchase_Amount_NonNegative", "\"Amount\" >= 0");
                    table.CheckConstraint("CK_CreditPurchase_BaseCredits_NonNegative", "\"BaseCredits\" >= 0");
                    table.CheckConstraint("CK_CreditPurchase_BonusCredits_NonNegative", "\"BonusCredits\" >= 0");
                    table.CheckConstraint("CK_CreditPurchase_TotalCredits_NonNegative", "\"TotalCredits\" >= 0");
                    table.ForeignKey(
                        name: "FK_CreditPurchases_CreditPackages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "CreditPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CreditPurchases_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CreditPurchases_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CreditPackages",
                columns: new[] { "Id", "BaseCredits", "BonusCredits", "CreatedAt", "Currency", "Description", "DisplayOrder", "ExpiresAfterDays", "IsActive", "Name", "Price", "TotalCredits", "UpdatedAt", "ValidDays" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), 300, 0, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "VND", "Small top-up for regular study.", 1, null, true, "Study Lite", 10000m, 300, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("10000000-0000-0000-0000-000000000002"), 700, 100, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "VND", "Better value for active learners.", 2, null, true, "Study Plus", 25000m, 800, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("10000000-0000-0000-0000-000000000003"), 1700, 300, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "VND", "Recommended for quiz/PE/final preparation.", 3, null, true, "Exam Boost", 59000m, 2000, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("10000000-0000-0000-0000-000000000004"), 4000, 1000, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "VND", "Best value for intensive exam preparation.", 4, null, true, "Final Sprint", 129000m, 5000, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null }
                });

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreditOutputTokenWeight", "CreditTokenUnit", "DailyFreeCredits", "DailyTokenLimit", "EnableCreditSystem", "ExamSeasonDailyFreeCredits" },
                values: new object[] { 4, 1000, 60, 50000, true, 100 });

            migrationBuilder.CreateIndex(
                name: "IX_CreditBlockedAttempts_ChatSessionId",
                table: "CreditBlockedAttempts",
                column: "ChatSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditBlockedAttempts_DatasetId",
                table: "CreditBlockedAttempts",
                column: "DatasetId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditBlockedAttempts_Reason_CreatedAt",
                table: "CreditBlockedAttempts",
                columns: new[] { "Reason", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditBlockedAttempts_UserId_CreatedAt",
                table: "CreditBlockedAttempts",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedgers_ChatMessageId",
                table: "CreditLedgers",
                column: "ChatMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedgers_ChatSessionId",
                table: "CreditLedgers",
                column: "ChatSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedgers_CreatedByUserId",
                table: "CreditLedgers",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedgers_DatasetId_CreatedAt",
                table: "CreditLedgers",
                columns: new[] { "DatasetId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedgers_ModelName_CreatedAt",
                table: "CreditLedgers",
                columns: new[] { "ModelName", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedgers_RelatedPackageId",
                table: "CreditLedgers",
                column: "RelatedPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedgers_Type_CreatedAt",
                table: "CreditLedgers",
                columns: new[] { "Type", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedgers_UserId_CreatedAt",
                table: "CreditLedgers",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditPurchases_CreatedByUserId",
                table: "CreditPurchases",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditPurchases_PackageId",
                table: "CreditPurchases",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditPurchases_Status_CreatedAt",
                table: "CreditPurchases",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditPurchases_UserId_CreatedAt",
                table: "CreditPurchases",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditWallets_UserId",
                table: "CreditWallets",
                column: "UserId",
                unique: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditBlockedAttempts");

            migrationBuilder.DropTable(
                name: "CreditLedgers");

            migrationBuilder.DropTable(
                name: "CreditPurchases");

            migrationBuilder.DropTable(
                name: "CreditWallets");

            migrationBuilder.DropTable(
                name: "CreditPackages");

            migrationBuilder.DropColumn(
                name: "CreditOutputTokenWeight",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "CreditTokenUnit",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "DailyFreeCredits",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "EnableCreditSystem",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "ExamSeasonDailyFreeCredits",
                table: "SystemSettings");
        }
    }
}
