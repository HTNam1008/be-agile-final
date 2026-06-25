using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

/// <inheritdoc />
public partial class AddEnrollmentRefundLedger : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EnrollmentRefund",
            schema: "payment",
            columns: table => new
            {
                EnrollmentRefundId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                CourseEnrollmentId = table.Column<long>(type: "bigint", nullable: false),
                PersonId = table.Column<long>(type: "bigint", nullable: false),
                PaidAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                RefundPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                RefundAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                EducationAccountRefundAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                OnlineRefundAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                PolicyPeriodCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                RefundStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                IdempotencyKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                RequestedByUserAccountId = table.Column<long>(type: "bigint", nullable: false),
                RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EnrollmentRefund", x => x.EnrollmentRefundId);
            });

        migrationBuilder.CreateTable(
            name: "EnrollmentRefundPart",
            schema: "payment",
            columns: table => new
            {
                EnrollmentRefundPartId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                EnrollmentRefundId = table.Column<long>(type: "bigint", nullable: false),
                PaymentId = table.Column<long>(type: "bigint", nullable: true),
                PaymentPartId = table.Column<long>(type: "bigint", nullable: true),
                RefundMethodCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                RefundAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                RefundStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                ProviderRefundId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                AccountTransactionId = table.Column<long>(type: "bigint", nullable: true),
                IdempotencyKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EnrollmentRefundPart", x => x.EnrollmentRefundPartId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EnrollmentRefund_CourseEnrollmentId",
            schema: "payment",
            table: "EnrollmentRefund",
            column: "CourseEnrollmentId");

        migrationBuilder.CreateIndex(
            name: "IX_EnrollmentRefund_IdempotencyKey",
            schema: "payment",
            table: "EnrollmentRefund",
            column: "IdempotencyKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EnrollmentRefundPart_EnrollmentRefundId",
            schema: "payment",
            table: "EnrollmentRefundPart",
            column: "EnrollmentRefundId");

        migrationBuilder.CreateIndex(
            name: "IX_EnrollmentRefundPart_IdempotencyKey",
            schema: "payment",
            table: "EnrollmentRefundPart",
            column: "IdempotencyKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EnrollmentRefundPart_ProviderRefundId",
            schema: "payment",
            table: "EnrollmentRefundPart",
            column: "ProviderRefundId",
            unique: true,
            filter: "[ProviderRefundId] IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "EnrollmentRefund",
            schema: "payment");

        migrationBuilder.DropTable(
            name: "EnrollmentRefundPart",
            schema: "payment");
    }
}
