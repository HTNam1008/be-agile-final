using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddStripePaymentEvidenceUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderHostedInvoiceUrl",
                schema: "payment",
                table: "Payment",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderInvoicePdfUrl",
                schema: "payment",
                table: "Payment",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderReceiptUrl",
                schema: "payment",
                table: "Payment",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProviderHostedInvoiceUrl",
                schema: "payment",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "ProviderInvoicePdfUrl",
                schema: "payment",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "ProviderReceiptUrl",
                schema: "payment",
                table: "Payment");
        }
    }
}
