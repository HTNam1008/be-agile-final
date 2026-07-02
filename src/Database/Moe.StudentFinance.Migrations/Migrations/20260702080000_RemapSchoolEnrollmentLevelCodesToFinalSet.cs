using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class RemapSchoolEnrollmentLevelCodesToFinalSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE [person].[SchoolEnrollment]
                SET [LevelCode] = CASE
                    WHEN [LevelCode] IN ('PRI_1', 'PRI_2', 'PRI_3', 'PRI_4', 'PRI_5', 'PRI_6',
                                         'SEC_1', 'SEC_2', 'SEC_3', 'SEC_4', 'SEC_5') THEN 'POST_SEC'
                    WHEN [LevelCode] IN ('UNI_Y1', 'UNI_Y2') THEN 'BACHELOR'
                    WHEN [LevelCode] = 'UNI_Y3' THEN 'MASTER'
                    WHEN [LevelCode] = 'UNI_Y4' THEN 'PHD'
                    ELSE [LevelCode]
                END
                WHERE [LevelCode] IN ('PRI_1', 'PRI_2', 'PRI_3', 'PRI_4', 'PRI_5', 'PRI_6',
                                      'SEC_1', 'SEC_2', 'SEC_3', 'SEC_4', 'SEC_5',
                                      'UNI_Y1', 'UNI_Y2', 'UNI_Y3', 'UNI_Y4');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally irreversible: POST_SEC and BACHELOR collapse multiple legacy codes.
        }
    }
}
