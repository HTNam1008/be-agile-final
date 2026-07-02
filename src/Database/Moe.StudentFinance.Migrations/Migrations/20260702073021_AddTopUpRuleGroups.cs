using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTopUpRuleGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TopUpRuleGroup",
                schema: "topup",
                columns: table => new
                {
                    TopUpRuleGroupId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TopUpCampaignId = table.Column<long>(type: "bigint", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopUpRuleGroup", x => x.TopUpRuleGroupId);
                    table.ForeignKey(
                        name: "FK_TopUpRuleGroup_TopUpCampaign_TopUpCampaignId",
                        column: x => x.TopUpCampaignId,
                        principalSchema: "topup",
                        principalTable: "TopUpCampaign",
                        principalColumn: "TopUpCampaignId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                schema: "topup",
                table: "TopUpCampaignRule",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "TopUpRuleGroupId",
                schema: "topup",
                table: "TopUpCampaignRule",
                type: "bigint",
                nullable: true);

            migrationBuilder.Sql("""
                INSERT INTO [topup].[TopUpRuleGroup] ([TopUpCampaignId], [DisplayOrder], [CreatedAt], [UpdatedAt])
                SELECT DISTINCT [TopUpCampaignId], 1, SYSUTCDATETIME(), NULL
                FROM [topup].[TopUpCampaignRule]
                WHERE [IsActive] = 1;
                """);

            migrationBuilder.Sql("""
                UPDATE r
                SET [TopUpRuleGroupId] = g.[TopUpRuleGroupId],
                    [DisplayOrder] = seq.[RowNum]
                FROM [topup].[TopUpCampaignRule] r
                INNER JOIN [topup].[TopUpRuleGroup] g
                    ON g.[TopUpCampaignId] = r.[TopUpCampaignId]
                   AND g.[DisplayOrder] = 1
                INNER JOIN (
                    SELECT [TopUpCampaignRuleId],
                           ROW_NUMBER() OVER (PARTITION BY [TopUpCampaignId] ORDER BY [TopUpCampaignRuleId]) AS [RowNum]
                    FROM [topup].[TopUpCampaignRule]
                    WHERE [IsActive] = 1
                ) seq ON seq.[TopUpCampaignRuleId] = r.[TopUpCampaignRuleId];
                """);

            migrationBuilder.Sql("""
                DELETE FROM [topup].[TopUpCampaignRule]
                WHERE [IsActive] = 0;
                """);

            migrationBuilder.AlterColumn<long>(
                name: "TopUpRuleGroupId",
                schema: "topup",
                table: "TopUpCampaignRule",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "topup",
                table: "TopUpCampaignRule");

            migrationBuilder.CreateIndex(
                name: "IX_TopUpCampaignRule_TopUpRuleGroupId_DisplayOrder",
                schema: "topup",
                table: "TopUpCampaignRule",
                columns: new[] { "TopUpRuleGroupId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_TopUpRuleGroup_TopUpCampaignId_DisplayOrder",
                schema: "topup",
                table: "TopUpRuleGroup",
                columns: new[] { "TopUpCampaignId", "DisplayOrder" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TopUpCampaignRule_TopUpRuleGroup_TopUpRuleGroupId",
                schema: "topup",
                table: "TopUpCampaignRule",
                column: "TopUpRuleGroupId",
                principalSchema: "topup",
                principalTable: "TopUpRuleGroup",
                principalColumn: "TopUpRuleGroupId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TopUpCampaignRule_TopUpRuleGroup_TopUpRuleGroupId",
                schema: "topup",
                table: "TopUpCampaignRule");

            migrationBuilder.DropTable(
                name: "TopUpRuleGroup",
                schema: "topup");

            migrationBuilder.DropIndex(
                name: "IX_TopUpCampaignRule_TopUpRuleGroupId_DisplayOrder",
                schema: "topup",
                table: "TopUpCampaignRule");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "topup",
                table: "TopUpCampaignRule",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                schema: "topup",
                table: "TopUpCampaignRule");

            migrationBuilder.DropColumn(
                name: "TopUpRuleGroupId",
                schema: "topup",
                table: "TopUpCampaignRule");
        }
    }
}
