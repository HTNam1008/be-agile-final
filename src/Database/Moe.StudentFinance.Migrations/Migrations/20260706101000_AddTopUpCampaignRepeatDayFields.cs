using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTopUpCampaignRepeatDayFields : Migration
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
