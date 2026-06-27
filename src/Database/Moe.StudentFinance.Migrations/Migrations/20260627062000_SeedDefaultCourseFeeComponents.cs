using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moe.StudentFinance.Persistence;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

[Migration("20260627062000_SeedDefaultCourseFeeComponents")]
[DbContextAttribute(typeof(MoeDbContext))]
public partial class SeedDefaultCourseFeeComponents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            MERGE [course].[FeeComponent] AS target
            USING (VALUES
                (N'TUITION',  N'Tuition Fee',         'BASE',  'FIXED',      CAST(0 AS bit), CAST(1 AS bit), CAST(0.0000 AS decimal(19,4)), CAST(0 AS bit)),
                (N'MATERIAL', N'Learning Materials',  'ADDON', 'FIXED',      CAST(0 AS bit), CAST(1 AS bit), CAST(0.0000 AS decimal(19,4)), CAST(0 AS bit)),
                (N'LAB',      N'Lab / Workshop Fee',  'ADDON', 'FIXED',      CAST(0 AS bit), CAST(1 AS bit), CAST(0.0000 AS decimal(19,4)), CAST(0 AS bit)),
                (N'GST',      N'GST 9%',              'TAX',   'PERCENTAGE', CAST(1 AS bit), CAST(1 AS bit), CAST(9.0000 AS decimal(19,4)), CAST(1 AS bit))
            ) AS source (
                [ComponentCode],
                [ComponentName],
                [ComponentTypeCode],
                [CalculationTypeCode],
                [IsTaxComponent],
                [IsActive],
                [DefaultValue],
                [IsSystemManaged])
            ON target.[ComponentCode] = source.[ComponentCode]
            WHEN MATCHED THEN
                UPDATE SET
                    target.[ComponentName] = source.[ComponentName],
                    target.[ComponentTypeCode] = source.[ComponentTypeCode],
                    target.[CalculationTypeCode] = source.[CalculationTypeCode],
                    target.[IsTaxComponent] = source.[IsTaxComponent],
                    target.[IsActive] = source.[IsActive],
                    target.[DefaultValue] = source.[DefaultValue],
                    target.[IsSystemManaged] = source.[IsSystemManaged]
            WHEN NOT MATCHED THEN
                INSERT (
                    [ComponentCode],
                    [ComponentName],
                    [ComponentTypeCode],
                    [CalculationTypeCode],
                    [IsTaxComponent],
                    [IsActive],
                    [DefaultValue],
                    [IsSystemManaged])
                VALUES (
                    source.[ComponentCode],
                    source.[ComponentName],
                    source.[ComponentTypeCode],
                    source.[CalculationTypeCode],
                    source.[IsTaxComponent],
                    source.[IsActive],
                    source.[DefaultValue],
                    source.[IsSystemManaged]);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
