using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moe.StudentFinance.Persistence;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

[Migration("20260627050000_SplitPaymentCheckoutSessions")]
[DbContextAttribute(typeof(MoeDbContext))]
public partial class SplitPaymentCheckoutSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CheckoutSessionTypeCode",
            schema: "payment",
            table: "PaymentCheckoutSession",
            type: "varchar(30)",
            unicode: false,
            maxLength: 30,
            nullable: false,
            defaultValue: "BILL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CheckoutSessionTypeCode",
            schema: "payment",
            table: "PaymentCheckoutSession");
    }
}
