using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountTransactionReversalIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AccountTransaction_ReversalOfTransactionId",
                schema: "account",
                table: "AccountTransaction",
                column: "ReversalOfTransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccountTransaction_ReversalOfTransactionId",
                schema: "account",
                table: "AccountTransaction");
        }
    }
}
