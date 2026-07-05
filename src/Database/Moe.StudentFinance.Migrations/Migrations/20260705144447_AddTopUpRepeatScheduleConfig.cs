using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTopUpRepeatScheduleConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MonthlyDay",
                schema: "topup",
                table: "TopUpCampaign",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeeklyDayOfWeek",
                schema: "topup",
                table: "TopUpCampaign",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TopUpCampaign_MonthlyDay",
                schema: "topup",
                table: "TopUpCampaign",
                sql: "[MonthlyDay] IS NULL OR [MonthlyDay] BETWEEN 1 AND 31");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TopUpCampaign_WeeklyDayOfWeek",
                schema: "topup",
                table: "TopUpCampaign",
                sql: "[WeeklyDayOfWeek] IS NULL OR [WeeklyDayOfWeek] BETWEEN 0 AND 6");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TopUpCampaign_MonthlyDay",
                schema: "topup",
                table: "TopUpCampaign");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TopUpCampaign_WeeklyDayOfWeek",
                schema: "topup",
                table: "TopUpCampaign");

            migrationBuilder.DropColumn(
                name: "MonthlyDay",
                schema: "topup",
                table: "TopUpCampaign");

            migrationBuilder.DropColumn(
                name: "WeeklyDayOfWeek",
                schema: "topup",
                table: "TopUpCampaign");
        }
    }
}
