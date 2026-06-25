using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

/// <inheritdoc />
public partial class AddResumableStripeCheckout : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CheckoutUrl",
            schema: "payment",
            table: "PaymentCheckoutSession",
            type: "nvarchar(2048)",
            maxLength: 2048,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ExpiresAt",
            schema: "payment",
            table: "PaymentCheckoutSession",
            type: "datetime2",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CheckoutUrl",
            schema: "payment",
            table: "PaymentCheckoutSession");

        migrationBuilder.DropColumn(
            name: "ExpiresAt",
            schema: "payment",
            table: "PaymentCheckoutSession");
    }
}
