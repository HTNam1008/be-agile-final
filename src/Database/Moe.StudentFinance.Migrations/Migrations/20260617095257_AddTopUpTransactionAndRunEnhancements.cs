using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTopUpTransactionAndRunEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TopUpTransaction_TopUpRunId_EducationAccountId",
                schema: "topup",
                table: "TopUpTransaction");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                schema: "topup",
                table: "TopUpTransaction");

            migrationBuilder.DropColumn(
                name: "ProcessedByLoginAccountId",
                schema: "topup",
                table: "TopUpTransaction");

            migrationBuilder.DropColumn(
                name: "TopUpAmount",
                schema: "topup",
                table: "TopUpTransaction");

            migrationBuilder.RenameColumn(
                name: "ProcessedAt",
                schema: "topup",
                table: "TopUpTransaction",
                newName: "CompletedAt");

            migrationBuilder.AlterColumn<string>(
                name: "TransactionStatusCode",
                schema: "topup",
                table: "TopUpTransaction",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(30)",
                oldUnicode: false,
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                schema: "topup",
                table: "TopUpTransaction",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "IdempotencyKey",
                schema: "topup",
                table: "TopUpTransaction",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120);

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                schema: "topup",
                table: "TopUpTransaction",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "topup",
                table: "TopUpTransaction",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<int>(
                name: "TotalSucceeded",
                schema: "topup",
                table: "TopUpRun",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "TotalSelected",
                schema: "topup",
                table: "TopUpRun",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "TotalProcessed",
                schema: "topup",
                table: "TopUpRun",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "TotalFailed",
                schema: "topup",
                table: "TopUpRun",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                schema: "topup",
                table: "TopUpRun",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,2)",
                oldPrecision: 19,
                oldScale: 2);

            migrationBuilder.AddColumn<int>(
                name: "TotalSkipped",
                schema: "topup",
                table: "TopUpRun",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_TopUpTransaction_Run_Account",
                schema: "topup",
                table: "TopUpTransaction",
                columns: new[] { "TopUpRunId", "EducationAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopUpRuns_Campaign_ScheduledFor",
                schema: "topup",
                table: "TopUpRun",
                columns: new[] { "TopUpCampaignId", "ScheduledFor" },
                unique: true,
                filter: "[ScheduledFor] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_TopUpTransaction_TopUpRun_TopUpRunId",
                schema: "topup",
                table: "TopUpTransaction",
                column: "TopUpRunId",
                principalSchema: "topup",
                principalTable: "TopUpRun",
                principalColumn: "TopUpRunId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TopUpTransaction_TopUpRun_TopUpRunId",
                schema: "topup",
                table: "TopUpTransaction");

            migrationBuilder.DropIndex(
                name: "IX_TopUpTransaction_Run_Account",
                schema: "topup",
                table: "TopUpTransaction");

            migrationBuilder.DropIndex(
                name: "IX_TopUpRuns_Campaign_ScheduledFor",
                schema: "topup",
                table: "TopUpRun");

            migrationBuilder.DropColumn(
                name: "Amount",
                schema: "topup",
                table: "TopUpTransaction");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "topup",
                table: "TopUpTransaction");

            migrationBuilder.DropColumn(
                name: "TotalSkipped",
                schema: "topup",
                table: "TopUpRun");

            migrationBuilder.RenameColumn(
                name: "CompletedAt",
                schema: "topup",
                table: "TopUpTransaction",
                newName: "ProcessedAt");

            migrationBuilder.AlterColumn<string>(
                name: "TransactionStatusCode",
                schema: "topup",
                table: "TopUpTransaction",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldUnicode: false,
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                schema: "topup",
                table: "TopUpTransaction",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IdempotencyKey",
                schema: "topup",
                table: "TopUpTransaction",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                schema: "topup",
                table: "TopUpTransaction",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProcessedByLoginAccountId",
                schema: "topup",
                table: "TopUpTransaction",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TopUpAmount",
                schema: "topup",
                table: "TopUpTransaction",
                type: "decimal(19,2)",
                precision: 19,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<int>(
                name: "TotalSucceeded",
                schema: "topup",
                table: "TopUpRun",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "TotalSelected",
                schema: "topup",
                table: "TopUpRun",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "TotalProcessed",
                schema: "topup",
                table: "TopUpRun",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "TotalFailed",
                schema: "topup",
                table: "TopUpRun",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                schema: "topup",
                table: "TopUpRun",
                type: "decimal(19,2)",
                precision: 19,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldDefaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_TopUpTransaction_TopUpRunId_EducationAccountId",
                schema: "topup",
                table: "TopUpTransaction",
                columns: new[] { "TopUpRunId", "EducationAccountId" });
        }
    }
}
