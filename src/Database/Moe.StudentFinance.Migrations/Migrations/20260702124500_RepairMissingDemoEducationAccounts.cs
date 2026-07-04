using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class RepairMissingDemoEducationAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    WHERE NOT EXISTS (SELECT 1 FROM [account].[EducationAccount] WHERE [EducationAccountId] = 4002 OR [PersonId] = 2002 OR [AccountNumber] = N'EA-NUS-002')
                       OR NOT EXISTS (SELECT 1 FROM [account].[EducationAccount] WHERE [EducationAccountId] = 4003 OR [PersonId] = 2003 OR [AccountNumber] = N'EA-NUS-003')
                       OR NOT EXISTS (SELECT 1 FROM [account].[EducationAccount] WHERE [EducationAccountId] = 4004 OR [PersonId] = 2004 OR [AccountNumber] = N'EA-NUS-004')
                )
                BEGIN
                    SET IDENTITY_INSERT [account].[EducationAccount] ON;

                    IF NOT EXISTS (SELECT 1 FROM [account].[EducationAccount] WHERE [EducationAccountId] = 4002 OR [PersonId] = 2002 OR [AccountNumber] = N'EA-NUS-002')
                    BEGIN
                        INSERT INTO [account].[EducationAccount] (
                            [EducationAccountId],
                            [PersonId],
                            [AccountNumber],
                            [AccountStatusCode],
                            [OpenedAt],
                            [OpeningTypeCode],
                            [OpeningReasonCode],
                            [OpeningReason],
                            [OpenedByLoginAccountId],
                            [CurrentBalance])
                        VALUES (
                            4002,
                            2002,
                            N'EA-NUS-002',
                            'ACTIVE',
                            '2026-01-01T00:00:00+00:00',
                            'MANUAL',
                            'MANUAL_LEGACY',
                            N'Demo seeded account for top-up search',
                            1001,
                            0.00);
                    END

                    IF NOT EXISTS (SELECT 1 FROM [account].[EducationAccount] WHERE [EducationAccountId] = 4003 OR [PersonId] = 2003 OR [AccountNumber] = N'EA-NUS-003')
                    BEGIN
                        INSERT INTO [account].[EducationAccount] (
                            [EducationAccountId],
                            [PersonId],
                            [AccountNumber],
                            [AccountStatusCode],
                            [OpenedAt],
                            [OpeningTypeCode],
                            [OpeningReasonCode],
                            [OpeningReason],
                            [OpenedByLoginAccountId],
                            [CurrentBalance])
                        VALUES (
                            4003,
                            2003,
                            N'EA-NUS-003',
                            'ACTIVE',
                            '2026-01-01T00:00:00+00:00',
                            'MANUAL',
                            'MANUAL_LEGACY',
                            N'Demo seeded account for top-up search',
                            1001,
                            0.00);
                    END

                    IF NOT EXISTS (SELECT 1 FROM [account].[EducationAccount] WHERE [EducationAccountId] = 4004 OR [PersonId] = 2004 OR [AccountNumber] = N'EA-NUS-004')
                    BEGIN
                        INSERT INTO [account].[EducationAccount] (
                            [EducationAccountId],
                            [PersonId],
                            [AccountNumber],
                            [AccountStatusCode],
                            [OpenedAt],
                            [OpeningTypeCode],
                            [OpeningReasonCode],
                            [OpeningReason],
                            [OpenedByLoginAccountId],
                            [CurrentBalance])
                        VALUES (
                            4004,
                            2004,
                            N'EA-NUS-004',
                            'ACTIVE',
                            '2026-01-01T00:00:00+00:00',
                            'MANUAL',
                            'MANUAL_LEGACY',
                            N'Demo seeded account for top-up search',
                            1001,
                            0.00);
                    END

                    SET IDENTITY_INSERT [account].[EducationAccount] OFF;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM [account].[EducationAccount]
                WHERE [EducationAccountId] IN (4002, 4003, 4004)
                  AND [PersonId] IN (2002, 2003, 2004)
                  AND [AccountNumber] IN (N'EA-NUS-002', N'EA-NUS-003', N'EA-NUS-004')
                  AND [OpeningReason] = N'Demo seeded account for top-up search';
                """);
        }
    }
}
