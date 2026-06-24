using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

/// <inheritdoc />
public partial class RestoreMonthlyBillingAndAllowReissuedBillSequences : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Bill_CourseEnrollmentId",
            schema: "billing",
            table: "Bill");

        migrationBuilder.DropIndex(
            name: "IX_AccountHold_PaymentPartId",
            schema: "account",
            table: "AccountHold");

        migrationBuilder.AddColumn<long>(
            name: "AccountHoldId",
            schema: "payment",
            table: "PaymentPart",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "CompletedAt",
            schema: "payment",
            table: "PaymentPart",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            schema: "payment",
            table: "PaymentPart",
            type: "datetime2",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

        migrationBuilder.AddColumn<long>(
            name: "BillingStatementId",
            schema: "payment",
            table: "PaymentCheckoutSession",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "PaymentId",
            schema: "payment",
            table: "PaymentCheckoutSession",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "BillingStatementId",
            schema: "payment",
            table: "Payment",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "EducationAccountAmount",
            schema: "payment",
            table: "Payment",
            type: "decimal(19,2)",
            precision: 19,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<DateTime>(
            name: "ExpiredAt",
            schema: "payment",
            table: "Payment",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "FailedAt",
            schema: "payment",
            table: "Payment",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "OnlinePaymentAmount",
            schema: "payment",
            table: "Payment",
            type: "decimal(19,2)",
            precision: 19,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<string>(
            name: "PaymentModeCode",
            schema: "payment",
            table: "Payment",
            type: "varchar(50)",
            unicode: false,
            maxLength: 50,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            schema: "payment",
            table: "Payment",
            type: "rowversion",
            rowVersion: true,
            nullable: false,
            defaultValue: new byte[0]);

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAt",
            schema: "payment",
            table: "Payment",
            type: "datetime2",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

        migrationBuilder.AddColumn<long>(
            name: "CoursePaymentPlanId",
            schema: "course",
            table: "CourseEnrollment",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            schema: "course",
            table: "CourseEnrollment",
            type: "rowversion",
            rowVersion: true,
            nullable: false,
            defaultValue: new byte[0]);

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            schema: "billing",
            table: "Bill",
            type: "datetime2",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

        migrationBuilder.AddColumn<DateOnly>(
            name: "CurrentDueDate",
            schema: "billing",
            table: "Bill",
            type: "date",
            nullable: false,
            defaultValue: new DateOnly(1, 1, 1));

        migrationBuilder.AddColumn<int>(
            name: "DeferralCount",
            schema: "billing",
            table: "Bill",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<decimal>(
            name: "DeferredAmount",
            schema: "billing",
            table: "Bill",
            type: "decimal(19,2)",
            precision: 19,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<DateOnly>(
            name: "OriginalDueDate",
            schema: "billing",
            table: "Bill",
            type: "date",
            nullable: false,
            defaultValue: new DateOnly(1, 1, 1));

        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            schema: "billing",
            table: "Bill",
            type: "rowversion",
            rowVersion: true,
            nullable: false,
            defaultValue: new byte[0]);

        migrationBuilder.AddColumn<int>(
            name: "SequenceNumber",
            schema: "billing",
            table: "Bill",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAt",
            schema: "billing",
            table: "Bill",
            type: "datetime2",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

        migrationBuilder.CreateTable(
            name: "BillDeferral",
            schema: "billing",
            columns: table => new
            {
                BillDeferralId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                BillId = table.Column<long>(type: "bigint", nullable: false),
                CourseEnrollmentId = table.Column<long>(type: "bigint", nullable: false),
                FailedPaymentId = table.Column<long>(type: "bigint", nullable: false),
                FromDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                ToDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                DeferredAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                DeferralSequenceNumber = table.Column<int>(type: "int", nullable: false),
                ReasonCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                CreatedByLoginAccountId = table.Column<long>(type: "bigint", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BillDeferral", x => x.BillDeferralId);
            });

        migrationBuilder.CreateTable(
            name: "BillingStatement",
            schema: "billing",
            columns: table => new
            {
                BillingStatementId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                PersonId = table.Column<long>(type: "bigint", nullable: false),
                StatementYear = table.Column<int>(type: "int", nullable: false),
                StatementMonth = table.Column<int>(type: "int", nullable: false),
                CurrencyCode = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                TotalAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                PaidAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                OutstandingAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                StatementStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BillingStatement", x => x.BillingStatementId);
            });

        migrationBuilder.CreateTable(
            name: "BillingStatementItem",
            schema: "billing",
            columns: table => new
            {
                BillingStatementItemId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                BillingStatementId = table.Column<long>(type: "bigint", nullable: false),
                BillId = table.Column<long>(type: "bigint", nullable: false),
                IncludedAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                PaidAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                ItemStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BillingStatementItem", x => x.BillingStatementItemId);
            });

        migrationBuilder.CreateTable(
            name: "PaymentAllocation",
            schema: "payment",
            columns: table => new
            {
                PaymentAllocationId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                PaymentId = table.Column<long>(type: "bigint", nullable: false),
                BillId = table.Column<long>(type: "bigint", nullable: false),
                BillingStatementItemId = table.Column<long>(type: "bigint", nullable: false),
                AllocatedAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                AllocationStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PaymentAllocation", x => x.PaymentAllocationId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PaymentCheckoutSession_PaymentId",
            schema: "payment",
            table: "PaymentCheckoutSession",
            column: "PaymentId",
            unique: true,
            filter: "[PaymentId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Payment_BillingStatementId",
            schema: "payment",
            table: "Payment",
            column: "BillingStatementId");

        migrationBuilder.CreateIndex(
            name: "IX_CourseEnrollment_CoursePaymentPlanId",
            schema: "course",
            table: "CourseEnrollment",
            column: "CoursePaymentPlanId");

        migrationBuilder.Sql(
            """
            ;WITH SequencedBills AS
            (
                SELECT
                    [BillId],
                    ROW_NUMBER() OVER
                    (
                        PARTITION BY [CourseEnrollmentId]
                        ORDER BY [DueDate], [BillId]
                    ) AS [NewSequence]
                FROM [billing].[Bill]
            )
            UPDATE bill
            SET
                bill.[SequenceNumber] = sequence.[NewSequence],
                bill.[OriginalDueDate] = bill.[DueDate],
                bill.[CurrentDueDate] = bill.[DueDate],
                bill.[CreatedAt] = bill.[IssuedAt],
                bill.[UpdatedAt] = bill.[IssuedAt]
            FROM [billing].[Bill] bill
            INNER JOIN SequencedBills sequence
                ON sequence.[BillId] = bill.[BillId];

            UPDATE enrollment
            SET enrollment.[CoursePaymentPlanId] = selectedPlan.[CoursePaymentPlanId]
            FROM [course].[CourseEnrollment] enrollment
            CROSS APPLY
            (
                SELECT TOP (1) paymentPlan.[CoursePaymentPlanId]
                FROM [payment].[CoursePaymentPlan] paymentPlan
                WHERE paymentPlan.[CourseId] = enrollment.[CourseId]
                  AND paymentPlan.[InstallmentCount] =
                  (
                      SELECT COUNT(*)
                      FROM [billing].[Bill] bill
                      WHERE bill.[CourseEnrollmentId] = enrollment.[CourseEnrollmentId]
                        AND bill.[BillStatusCode] <> 'CANCELLED'
                  )
                ORDER BY paymentPlan.[IsActive] DESC, paymentPlan.[Version] DESC
            ) selectedPlan
            WHERE enrollment.[CoursePaymentPlanId] = 0;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_Bill_CourseEnrollmentId_SequenceNumber",
            schema: "billing",
            table: "Bill",
            columns: new[] { "CourseEnrollmentId", "SequenceNumber" },
            unique: true,
            filter: "[BillStatusCode] <> 'CANCELLED'");

        migrationBuilder.CreateIndex(
            name: "IX_AccountHold_PaymentPartId",
            schema: "account",
            table: "AccountHold",
            column: "PaymentPartId",
            unique: true,
            filter: "[PaymentPartId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_BillDeferral_BillId_DeferralSequenceNumber",
            schema: "billing",
            table: "BillDeferral",
            columns: new[] { "BillId", "DeferralSequenceNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_BillDeferral_BillId_FailedPaymentId",
            schema: "billing",
            table: "BillDeferral",
            columns: new[] { "BillId", "FailedPaymentId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_BillingStatement_PersonId_StatementYear_StatementMonth",
            schema: "billing",
            table: "BillingStatement",
            columns: new[] { "PersonId", "StatementYear", "StatementMonth" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_BillingStatementItem_BillingStatementId_BillId",
            schema: "billing",
            table: "BillingStatementItem",
            columns: new[] { "BillingStatementId", "BillId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PaymentAllocation_PaymentId_BillId",
            schema: "payment",
            table: "PaymentAllocation",
            columns: new[] { "PaymentId", "BillId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "BillDeferral",
            schema: "billing");

        migrationBuilder.DropTable(
            name: "BillingStatement",
            schema: "billing");

        migrationBuilder.DropTable(
            name: "BillingStatementItem",
            schema: "billing");

        migrationBuilder.DropTable(
            name: "PaymentAllocation",
            schema: "payment");

        migrationBuilder.DropIndex(
            name: "IX_PaymentCheckoutSession_PaymentId",
            schema: "payment",
            table: "PaymentCheckoutSession");

        migrationBuilder.DropIndex(
            name: "IX_Payment_BillingStatementId",
            schema: "payment",
            table: "Payment");

        migrationBuilder.DropIndex(
            name: "IX_CourseEnrollment_CoursePaymentPlanId",
            schema: "course",
            table: "CourseEnrollment");

        migrationBuilder.DropIndex(
            name: "IX_Bill_CourseEnrollmentId_SequenceNumber",
            schema: "billing",
            table: "Bill");

        migrationBuilder.DropIndex(
            name: "IX_AccountHold_PaymentPartId",
            schema: "account",
            table: "AccountHold");

        migrationBuilder.DropColumn(
            name: "AccountHoldId",
            schema: "payment",
            table: "PaymentPart");

        migrationBuilder.DropColumn(
            name: "CompletedAt",
            schema: "payment",
            table: "PaymentPart");

        migrationBuilder.DropColumn(
            name: "CreatedAt",
            schema: "payment",
            table: "PaymentPart");

        migrationBuilder.DropColumn(
            name: "BillingStatementId",
            schema: "payment",
            table: "PaymentCheckoutSession");

        migrationBuilder.DropColumn(
            name: "PaymentId",
            schema: "payment",
            table: "PaymentCheckoutSession");

        migrationBuilder.DropColumn(
            name: "BillingStatementId",
            schema: "payment",
            table: "Payment");

        migrationBuilder.DropColumn(
            name: "EducationAccountAmount",
            schema: "payment",
            table: "Payment");

        migrationBuilder.DropColumn(
            name: "ExpiredAt",
            schema: "payment",
            table: "Payment");

        migrationBuilder.DropColumn(
            name: "FailedAt",
            schema: "payment",
            table: "Payment");

        migrationBuilder.DropColumn(
            name: "OnlinePaymentAmount",
            schema: "payment",
            table: "Payment");

        migrationBuilder.DropColumn(
            name: "PaymentModeCode",
            schema: "payment",
            table: "Payment");

        migrationBuilder.DropColumn(
            name: "RowVersion",
            schema: "payment",
            table: "Payment");

        migrationBuilder.DropColumn(
            name: "UpdatedAt",
            schema: "payment",
            table: "Payment");

        migrationBuilder.DropColumn(
            name: "CoursePaymentPlanId",
            schema: "course",
            table: "CourseEnrollment");

        migrationBuilder.DropColumn(
            name: "RowVersion",
            schema: "course",
            table: "CourseEnrollment");

        migrationBuilder.DropColumn(
            name: "CreatedAt",
            schema: "billing",
            table: "Bill");

        migrationBuilder.DropColumn(
            name: "CurrentDueDate",
            schema: "billing",
            table: "Bill");

        migrationBuilder.DropColumn(
            name: "DeferralCount",
            schema: "billing",
            table: "Bill");

        migrationBuilder.DropColumn(
            name: "DeferredAmount",
            schema: "billing",
            table: "Bill");

        migrationBuilder.DropColumn(
            name: "OriginalDueDate",
            schema: "billing",
            table: "Bill");

        migrationBuilder.DropColumn(
            name: "RowVersion",
            schema: "billing",
            table: "Bill");

        migrationBuilder.DropColumn(
            name: "SequenceNumber",
            schema: "billing",
            table: "Bill");

        migrationBuilder.DropColumn(
            name: "UpdatedAt",
            schema: "billing",
            table: "Bill");

        migrationBuilder.CreateIndex(
            name: "IX_Bill_CourseEnrollmentId",
            schema: "billing",
            table: "Bill",
            column: "CourseEnrollmentId");

        migrationBuilder.CreateIndex(
            name: "IX_AccountHold_PaymentPartId",
            schema: "account",
            table: "AccountHold",
            column: "PaymentPartId",
            filter: "[PaymentPartId] IS NOT NULL");
    }
}
