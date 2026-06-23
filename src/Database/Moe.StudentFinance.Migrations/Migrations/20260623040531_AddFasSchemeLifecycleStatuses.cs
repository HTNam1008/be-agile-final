using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moe.StudentFinance.Persistence;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

[DbContext(typeof(MoeDbContext))]
[Migration("20260623040531_AddFasSchemeLifecycleStatuses")]
public partial class AddFasSchemeLifecycleStatuses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_FASScheme_Status",
            schema: "fas",
            table: "FASScheme");

        migrationBuilder.AddCheckConstraint(
            name: "CK_FASScheme_Status",
            schema: "fas",
            table: "FASScheme",
            sql: "[StatusCode] IN ('DRAFT','ACTIVE','RETIRED','DISABLED','DELETED')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_FASScheme_Status",
            schema: "fas",
            table: "FASScheme");

        migrationBuilder.AddCheckConstraint(
            name: "CK_FASScheme_Status",
            schema: "fas",
            table: "FASScheme",
            sql: "[StatusCode] IN ('DRAFT','ACTIVE','RETIRED')");
    }
}
