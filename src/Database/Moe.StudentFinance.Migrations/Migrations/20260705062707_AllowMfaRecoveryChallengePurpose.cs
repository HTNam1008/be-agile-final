using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AllowMfaRecoveryChallengePurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_LoginMfaChallenge_PurposeCode",
                schema: "iam",
                table: "LoginMfaChallenge");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LoginMfaChallenge_PurposeCode",
                schema: "iam",
                table: "LoginMfaChallenge",
                sql: "[PurposeCode] IN ('SETUP', 'VERIFY', 'LOGIN', 'RECOVERY')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_LoginMfaChallenge_PurposeCode",
                schema: "iam",
                table: "LoginMfaChallenge");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LoginMfaChallenge_PurposeCode",
                schema: "iam",
                table: "LoginMfaChallenge",
                sql: "[PurposeCode] IN ('SETUP', 'VERIFY', 'LOGIN')");
        }
    }
}
