using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

/// <inheritdoc />
public partial class Phase6_TopUp_Delivery_Contracts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            schema: "topup",
            table: "TopUpRun",
            type: "datetime2",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

        migrationBuilder.AddColumn<bool>(
            name: "IsContractDriven",
            schema: "topup",
            table: "TopUpRun",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "RunTypeCode",
            schema: "topup",
            table: "TopUpRun",
            type: "varchar(20)",
            unicode: false,
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "DeletedAt",
            schema: "topup",
            table: "TopUpCampaignRecipient",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "DeletedByLoginAccountId",
            schema: "topup",
            table: "TopUpCampaignRecipient",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DeliveryTypeCode",
            schema: "topup",
            table: "TopUpCampaign",
            type: "varchar(30)",
            unicode: false,
            maxLength: 30,
            nullable: false,
            defaultValue: "INSTANT");

        migrationBuilder.AddColumn<decimal>(
            name: "MaxTotalAmount",
            schema: "topup",
            table: "TopUpCampaign",
            type: "decimal(19,2)",
            precision: 19,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.Sql("""
            IF COL_LENGTH('ai.Message', 'ResponseJson') IS NULL
                ALTER TABLE [ai].[Message] ADD [ResponseJson] nvarchar(max) NULL;
            """);

        migrationBuilder.CreateTable(
            name: "DynamicTopUpContract",
            schema: "topup",
            columns: table => new
            {
                DynamicTopUpContractId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                TopUpCampaignId = table.Column<long>(type: "bigint", nullable: false),
                EducationAccountId = table.Column<long>(type: "bigint", nullable: false),
                DeliveryTypeCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                QualifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                AmountPerPayment = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                MaxTotalAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                FrequencyCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                FrequencyInterval = table.Column<int>(type: "int", nullable: false),
                TotalReceived = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                CyclesCompleted = table.Column<int>(type: "int", nullable: false),
                FirstPaymentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                NextPaymentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                ContractStatus = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DynamicTopUpContract", x => x.DynamicTopUpContractId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DynamicTopUpContract_TopUpCampaignId_EducationAccountId",
            schema: "topup",
            table: "DynamicTopUpContract",
            columns: new[] { "TopUpCampaignId", "EducationAccountId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "DynamicTopUpContract",
            schema: "topup");

        migrationBuilder.DropColumn(
            name: "CreatedAt",
            schema: "topup",
            table: "TopUpRun");

        migrationBuilder.DropColumn(
            name: "IsContractDriven",
            schema: "topup",
            table: "TopUpRun");

        migrationBuilder.DropColumn(
            name: "RunTypeCode",
            schema: "topup",
            table: "TopUpRun");

        migrationBuilder.DropColumn(
            name: "DeletedAt",
            schema: "topup",
            table: "TopUpCampaignRecipient");

        migrationBuilder.DropColumn(
            name: "DeletedByLoginAccountId",
            schema: "topup",
            table: "TopUpCampaignRecipient");

        migrationBuilder.DropColumn(
            name: "DeliveryTypeCode",
            schema: "topup",
            table: "TopUpCampaign");

        migrationBuilder.DropColumn(
            name: "MaxTotalAmount",
            schema: "topup",
            table: "TopUpCampaign");

        migrationBuilder.Sql("""
            IF COL_LENGTH('ai.Message', 'ResponseJson') IS NOT NULL
                ALTER TABLE [ai].[Message] DROP COLUMN [ResponseJson];
            """);
    }
}
