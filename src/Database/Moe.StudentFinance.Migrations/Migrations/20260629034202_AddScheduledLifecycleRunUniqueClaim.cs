using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledLifecycleRunUniqueClaim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EducationAccountLifecycleRun_RunDateUtc",
                schema: "account",
                table: "EducationAccountLifecycleRun");

            migrationBuilder.CreateIndex(
                name: "IX_EducationAccountLifecycleRun_RunDateUtc_TriggerTypeCode",
                schema: "account",
                table: "EducationAccountLifecycleRun",
                columns: new[] { "RunDateUtc", "TriggerTypeCode" },
                unique: true,
                filter: "[TriggerTypeCode] = 'SCHEDULED'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EducationAccountLifecycleRun_RunDateUtc_TriggerTypeCode",
                schema: "account",
                table: "EducationAccountLifecycleRun");

            migrationBuilder.CreateIndex(
                name: "IX_EducationAccountLifecycleRun_RunDateUtc",
                schema: "account",
                table: "EducationAccountLifecycleRun",
                column: "RunDateUtc");
        }
    }
}
