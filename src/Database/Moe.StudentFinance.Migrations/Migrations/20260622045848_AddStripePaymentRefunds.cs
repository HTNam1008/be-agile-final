using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

/// <inheritdoc />
public partial class AddStripePaymentRefunds : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PaymentRefund",
            schema: "payment",
            columns: table => new
            {
                PaymentRefundId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                PaymentId = table.Column<long>(type: "bigint", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                RefundStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                ProviderRefundId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                RequestedByUserAccountId = table.Column<long>(type: "bigint", nullable: false),
                RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PaymentRefund", x => x.PaymentRefundId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PaymentRefund_PaymentId",
            schema: "payment",
            table: "PaymentRefund",
            column: "PaymentId");

        migrationBuilder.CreateIndex(
            name: "IX_PaymentRefund_ProviderRefundId",
            schema: "payment",
            table: "PaymentRefund",
            column: "ProviderRefundId",
            unique: true,
            filter: "[ProviderRefundId] IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PaymentRefund",
            schema: "payment");
    }
}
