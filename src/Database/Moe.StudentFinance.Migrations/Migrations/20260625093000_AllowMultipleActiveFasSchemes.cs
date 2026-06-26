using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moe.StudentFinance.Persistence;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

/// <inheritdoc />
[DbContext(typeof(MoeDbContext))]
[Migration("20260625093000_AllowMultipleActiveFasSchemes")]
public partial class AllowMultipleActiveFasSchemes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_FASActiveScheme_StudentPersonId",
            schema: "fas",
            table: "FASActiveScheme");

        migrationBuilder.CreateIndex(
            name: "IX_FASActiveScheme_StudentPersonId",
            schema: "fas",
            table: "FASActiveScheme",
            column: "StudentPersonId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_FASActiveScheme_StudentPersonId",
            schema: "fas",
            table: "FASActiveScheme");

        migrationBuilder.CreateIndex(
            name: "IX_FASActiveScheme_StudentPersonId",
            schema: "fas",
            table: "FASActiveScheme",
            column: "StudentPersonId",
            unique: true,
            filter: "[StatusCode] = 'ACTIVE'");
    }
}
