using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetReservedToTopUpCampaign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CancelRequestedAt",
                schema: "topup",
                table: "TopUpRun",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BudgetReserved",
                schema: "topup",
                table: "TopUpCampaign",
                type: "decimal(19,2)",
                precision: 19,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_TopUpRun_CancelRequested",
                schema: "topup",
                table: "TopUpRun",
                column: "CancelRequestedAt",
                filter: "[CancelRequestedAtUtc] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TopUpRun_CancelRequested",
                schema: "topup",
                table: "TopUpRun");

            migrationBuilder.DropColumn(
                name: "CancelRequestedAt",
                schema: "topup",
                table: "TopUpRun");

            migrationBuilder.DropColumn(
                name: "BudgetReserved",
                schema: "topup",
                table: "TopUpCampaign");
        }
    }
}
