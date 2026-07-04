using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniqueEServiceAccountPerPerson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [iam].[LoginAccount]
                    WHERE [PersonId] IS NOT NULL
                    GROUP BY [PersonId], [PortalAccessCode]
                    HAVING COUNT(*) > 1
                )
                BEGIN
                    THROW 51000, 'Duplicate iam.LoginAccount rows found for the same PersonId and PortalAccessCode. Clean duplicate accounts before applying this migration.', 1;
                END
                """);

            migrationBuilder.CreateIndex(
                name: "IX_LoginAccount_PersonId_PortalAccessCode",
                schema: "iam",
                table: "LoginAccount",
                columns: new[] { "PersonId", "PortalAccessCode" },
                unique: true,
                filter: "[PersonId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoginAccount_PersonId_PortalAccessCode",
                schema: "iam",
                table: "LoginAccount");
        }
    }
}
