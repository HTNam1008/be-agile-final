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
            migrationBuilder.Sql("""
                ;WITH DuplicateScheduledRuns AS
                (
                    SELECT
                        [EducationAccountLifecycleRunId],
                        ROW_NUMBER() OVER
                        (
                            PARTITION BY [RunDateUtc], [TriggerTypeCode]
                            ORDER BY [StartedAtUtc] DESC, [EducationAccountLifecycleRunId] DESC
                        ) AS [RowNumber]
                    FROM [account].[EducationAccountLifecycleRun]
                    WHERE [TriggerTypeCode] = 'SCHEDULED'
                )
                DELETE run
                FROM [account].[EducationAccountLifecycleRun] run
                INNER JOIN DuplicateScheduledRuns duplicate
                    ON duplicate.[EducationAccountLifecycleRunId] = run.[EducationAccountLifecycleRunId]
                WHERE duplicate.[RowNumber] > 1;
                """);

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
