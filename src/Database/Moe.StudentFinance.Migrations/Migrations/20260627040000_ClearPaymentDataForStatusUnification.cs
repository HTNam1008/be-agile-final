using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moe.StudentFinance.Persistence;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

[Migration("20260627040000_ClearPaymentDataForStatusUnification")]
[DbContextAttribute(typeof(MoeDbContext))]
public partial class ClearPaymentDataForStatusUnification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE [account].[AccountHold]
            SET [PaymentPartId] = NULL
            WHERE [PaymentPartId] IS NOT NULL;

            DELETE FROM [payment].[PaymentRefund];
            DELETE FROM [payment].[PaymentCheckoutSession];
            DELETE FROM [payment].[PaymentAllocation];
            DELETE FROM [payment].[EnrollmentRefundPart];
            DELETE FROM [payment].[EnrollmentRefund];
            DELETE FROM [payment].[PaymentPart];
            DELETE FROM [payment].[ProcessedWebhookEvent];
            DELETE FROM [payment].[Payment];
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
