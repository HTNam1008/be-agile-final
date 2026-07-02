using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moe.StudentFinance.Persistence;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

/// <inheritdoc />
[DbContext(typeof(MoeDbContext))]
[Migration("20260702071000_SeedDemoFasScheme")]
public partial class SeedDemoFasScheme : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.InsertData(
            schema: "fas",
            table: "FASScheme",
            columns:
            [
                "FASSchemeId",
                "SchemeCode",
                "GrantCode",
                "Name",
                "Description",
                "StartDate",
                "EndDate",
                "StatusCode",
                "CreatedByLoginAccountId",
                "CreatedAt",
                "ActivatedByLoginAccountId",
                "ActivatedAt"
            ],
            values:
            [
                900001L,
                "DEMO-FAS-2026",
                "DEMO-GRANT-FAS-2026",
                "MOE Demo Financial Assistance Scheme",
                "Demo UAT scheme available to eligible student portal users for AI-assisted FAS application testing.",
                new DateOnly(2026, 1, 1),
                new DateOnly(2027, 12, 31),
                "ACTIVE",
                1001L,
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                1001L,
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            ]);

        migrationBuilder.InsertData(
            schema: "fas",
            table: "FASTier",
            columns:
            [
                "FASTierId",
                "FASSchemeId",
                "Label",
                "SubsidyType",
                "SubsidyValue",
                "DisplayOrder",
                "CreatedAt"
            ],
            values:
            [
                900001L,
                900001L,
                "Demo eligible tier",
                "PERCENTAGE",
                100m,
                1,
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            ]);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            schema: "fas",
            table: "FASTier",
            keyColumn: "FASTierId",
            keyValue: 900001L);

        migrationBuilder.DeleteData(
            schema: "fas",
            table: "FASScheme",
            keyColumn: "FASSchemeId",
            keyValue: 900001L);
    }
}
