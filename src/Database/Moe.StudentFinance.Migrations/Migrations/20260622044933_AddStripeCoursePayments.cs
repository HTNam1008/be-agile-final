using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

/// <inheritdoc />
public partial class AddStripeCoursePayments : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "InstallmentNumber",
            schema: "payment",
            table: "Payment",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "ProviderChargeId",
            schema: "payment",
            table: "Payment",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProviderInvoiceId",
            schema: "payment",
            table: "Payment",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProviderPaymentIntentId",
            schema: "payment",
            table: "Payment",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "CoursePaymentPlan",
            schema: "payment",
            columns: table => new
            {
                CoursePaymentPlanId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                CourseId = table.Column<long>(type: "bigint", nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                PlanTypeCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                CurrencyCode = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                InstallmentCount = table.Column<int>(type: "int", nullable: false),
                IntervalMonths = table.Column<int>(type: "int", nullable: false),
                Version = table.Column<int>(type: "int", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CoursePaymentPlan", x => x.CoursePaymentPlanId);
            });

        migrationBuilder.CreateTable(
            name: "PaymentCheckoutSession",
            schema: "payment",
            columns: table => new
            {
                PaymentCheckoutSessionId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                BillId = table.Column<long>(type: "bigint", nullable: false),
                CourseEnrollmentId = table.Column<long>(type: "bigint", nullable: false),
                CourseId = table.Column<long>(type: "bigint", nullable: false),
                PersonId = table.Column<long>(type: "bigint", nullable: false),
                CoursePaymentPlanId = table.Column<long>(type: "bigint", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                CurrencyCode = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                RequiredInstallmentCount = table.Column<int>(type: "int", nullable: false),
                PaidInstallmentCount = table.Column<int>(type: "int", nullable: false),
                CheckoutStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                IdempotencyKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                ProviderCheckoutSessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ProviderPaymentIntentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ProviderSubscriptionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ProviderSubscriptionScheduleId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ProviderPriceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                LastPaymentEventAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PaymentCheckoutSession", x => x.PaymentCheckoutSessionId);
            });

        migrationBuilder.CreateTable(
            name: "ProcessedWebhookEvent",
            schema: "payment",
            columns: table => new
            {
                ProcessedWebhookEventId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ProviderEventId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ProcessingStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                AttemptCount = table.Column<int>(type: "int", nullable: false),
                FailureMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProcessedWebhookEvent", x => x.ProcessedWebhookEventId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Payment_ProviderChargeId",
            schema: "payment",
            table: "Payment",
            column: "ProviderChargeId",
            filter: "[ProviderChargeId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Payment_ProviderInvoiceId",
            schema: "payment",
            table: "Payment",
            column: "ProviderInvoiceId",
            unique: true,
            filter: "[ProviderInvoiceId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Payment_ProviderPaymentIntentId",
            schema: "payment",
            table: "Payment",
            column: "ProviderPaymentIntentId",
            unique: true,
            filter: "[ProviderPaymentIntentId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_CoursePaymentPlan_CourseId_IsActive",
            schema: "payment",
            table: "CoursePaymentPlan",
            columns: new[] { "CourseId", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_CoursePaymentPlan_CourseId_Version",
            schema: "payment",
            table: "CoursePaymentPlan",
            columns: new[] { "CourseId", "Version" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PaymentCheckoutSession_BillId_PersonId",
            schema: "payment",
            table: "PaymentCheckoutSession",
            columns: new[] { "BillId", "PersonId" });

        migrationBuilder.CreateIndex(
            name: "IX_PaymentCheckoutSession_IdempotencyKey",
            schema: "payment",
            table: "PaymentCheckoutSession",
            column: "IdempotencyKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PaymentCheckoutSession_ProviderCheckoutSessionId",
            schema: "payment",
            table: "PaymentCheckoutSession",
            column: "ProviderCheckoutSessionId",
            unique: true,
            filter: "[ProviderCheckoutSessionId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_PaymentCheckoutSession_ProviderPaymentIntentId",
            schema: "payment",
            table: "PaymentCheckoutSession",
            column: "ProviderPaymentIntentId",
            filter: "[ProviderPaymentIntentId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_PaymentCheckoutSession_ProviderSubscriptionId",
            schema: "payment",
            table: "PaymentCheckoutSession",
            column: "ProviderSubscriptionId",
            unique: true,
            filter: "[ProviderSubscriptionId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_ProcessedWebhookEvent_ProviderEventId",
            schema: "payment",
            table: "ProcessedWebhookEvent",
            column: "ProviderEventId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "CoursePaymentPlan",
            schema: "payment");

        migrationBuilder.DropTable(
            name: "PaymentCheckoutSession",
            schema: "payment");

        migrationBuilder.DropTable(
            name: "ProcessedWebhookEvent",
            schema: "payment");

        migrationBuilder.DropIndex(
            name: "IX_Payment_ProviderChargeId",
            schema: "payment",
            table: "Payment");

        migrationBuilder.DropIndex(
            name: "IX_Payment_ProviderInvoiceId",
            schema: "payment",
            table: "Payment");

        migrationBuilder.DropIndex(
            name: "IX_Payment_ProviderPaymentIntentId",
            schema: "payment",
            table: "Payment");

        migrationBuilder.DropColumn(
            name: "InstallmentNumber",
            schema: "payment",
            table: "Payment");

        migrationBuilder.DropColumn(
            name: "ProviderChargeId",
            schema: "payment",
            table: "Payment");

        migrationBuilder.DropColumn(
            name: "ProviderInvoiceId",
            schema: "payment",
            table: "Payment");

        migrationBuilder.DropColumn(
            name: "ProviderPaymentIntentId",
            schema: "payment",
            table: "Payment");
    }
}
