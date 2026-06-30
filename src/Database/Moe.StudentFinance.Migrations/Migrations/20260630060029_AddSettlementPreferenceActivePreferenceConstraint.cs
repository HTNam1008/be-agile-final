using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddSettlementPreferenceActivePreferenceConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SettlementPreference_EducationAccountId_IsActive",
                schema: "account",
                table: "SettlementPreference");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementPreference_EducationAccountId",
                schema: "account",
                table: "SettlementPreference",
                column: "EducationAccountId",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_SettlementPreference_EducationAccount_EducationAccountId",
                schema: "account",
                table: "SettlementPreference",
                column: "EducationAccountId",
                principalSchema: "account",
                principalTable: "EducationAccount",
                principalColumn: "EducationAccountId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SettlementPreference_EducationAccount_EducationAccountId",
                schema: "account",
                table: "SettlementPreference");

            migrationBuilder.DropIndex(
                name: "IX_SettlementPreference_EducationAccountId",
                schema: "account",
                table: "SettlementPreference");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementPreference_EducationAccountId_IsActive",
                schema: "account",
                table: "SettlementPreference",
                columns: new[] { "EducationAccountId", "IsActive" });
        }
    }
}
