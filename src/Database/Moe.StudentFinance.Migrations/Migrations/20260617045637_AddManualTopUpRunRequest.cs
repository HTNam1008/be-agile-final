using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddManualTopUpRunRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TopUpCampaign_CampaignCode",
                schema: "topup",
                table: "TopUpCampaign");

            migrationBuilder.DropIndex(
                name: "IX_TopUpCampaign_OrganizationId",
                schema: "topup",
                table: "TopUpCampaign");

            migrationBuilder.AlterColumn<string>(
                name: "IdempotencyKey",
                schema: "topup",
                table: "TopUpRun",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                schema: "topup",
                table: "TopUpRun",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopUpCampaign_OrganizationId_CampaignCode",
                schema: "topup",
                table: "TopUpCampaign",
                columns: new[] { "OrganizationId", "CampaignCode" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TopUpCampaign_Amount",
                schema: "topup",
                table: "TopUpCampaign",
                sql: "[DefaultTopUpAmount] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TopUpCampaign_Schedule",
                schema: "topup",
                table: "TopUpCampaign",
                sql: "[ScheduleTypeCode] != 'RECURRING' OR ([FrequencyCode] IS NOT NULL AND [FrequencyInterval] IS NOT NULL AND [EndDate] IS NOT NULL AND [EndDate] >= [StartDate])");

            migrationBuilder.AddForeignKey(
                name: "FK_TopUpRun_TopUpCampaign_TopUpCampaignId",
                schema: "topup",
                table: "TopUpRun",
                column: "TopUpCampaignId",
                principalSchema: "topup",
                principalTable: "TopUpCampaign",
                principalColumn: "TopUpCampaignId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TopUpRun_TopUpCampaign_TopUpCampaignId",
                schema: "topup",
                table: "TopUpRun");

            migrationBuilder.DropIndex(
                name: "IX_TopUpCampaign_OrganizationId_CampaignCode",
                schema: "topup",
                table: "TopUpCampaign");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TopUpCampaign_Amount",
                schema: "topup",
                table: "TopUpCampaign");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TopUpCampaign_Schedule",
                schema: "topup",
                table: "TopUpCampaign");

            migrationBuilder.DropColumn(
                name: "Note",
                schema: "topup",
                table: "TopUpRun");

            migrationBuilder.AlterColumn<string>(
                name: "IdempotencyKey",
                schema: "topup",
                table: "TopUpRun",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "IX_TopUpCampaign_CampaignCode",
                schema: "topup",
                table: "TopUpCampaign",
                column: "CampaignCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopUpCampaign_OrganizationId",
                schema: "topup",
                table: "TopUpCampaign",
                column: "OrganizationId");
        }
    }
}
