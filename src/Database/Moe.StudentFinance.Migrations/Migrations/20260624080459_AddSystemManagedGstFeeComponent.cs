using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

/// <inheritdoc />
public partial class AddSystemManagedGstFeeComponent : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "DefaultValue",
            schema: "course",
            table: "FeeComponent",
            type: "decimal(19,4)",
            precision: 19,
            scale: 4,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<bool>(
            name: "IsSystemManaged",
            schema: "course",
            table: "FeeComponent",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql(
            """
            IF EXISTS (SELECT 1 FROM [course].[FeeComponent] WHERE [ComponentCode] = N'GST')
            BEGIN
                UPDATE [course].[FeeComponent]
                SET [ComponentName] = N'GST 9%',
                    [ComponentTypeCode] = 'TAX',
                    [CalculationTypeCode] = 'PERCENTAGE',
                    [IsTaxComponent] = 1,
                    [DefaultValue] = 9.0000,
                    [IsSystemManaged] = 1,
                    [IsActive] = 1
                WHERE [ComponentCode] = N'GST';
            END
            ELSE
            BEGIN
                INSERT INTO [course].[FeeComponent]
                    ([ComponentCode], [ComponentName], [ComponentTypeCode], [CalculationTypeCode], [IsTaxComponent], [IsActive], [DefaultValue], [IsSystemManaged])
                VALUES
                    (N'GST', N'GST 9%', 'TAX', 'PERCENTAGE', 1, 1, 9.0000, 1);
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DefaultValue",
            schema: "course",
            table: "FeeComponent");

        migrationBuilder.DropColumn(
            name: "IsSystemManaged",
            schema: "course",
            table: "FeeComponent");
    }
}
