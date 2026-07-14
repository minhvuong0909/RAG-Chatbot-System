using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RagChatbotSystem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddPayOsPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckoutUrl",
                table: "CreditPurchases",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProviderOrderCode",
                table: "CreditPurchases",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditPurchases_ProviderOrderCode",
                table: "CreditPurchases",
                column: "ProviderOrderCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CreditPurchases_ProviderOrderCode",
                table: "CreditPurchases");

            migrationBuilder.DropColumn(
                name: "CheckoutUrl",
                table: "CreditPurchases");

            migrationBuilder.DropColumn(
                name: "ProviderOrderCode",
                table: "CreditPurchases");
        }
    }
}
