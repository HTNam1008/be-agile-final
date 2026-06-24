using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moe.StudentFinance.Persistence;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

[DbContext(typeof(MoeDbContext))]
[Migration("20260624033732_PersistEducationAccountOpeningReasonCode")]
public partial class PersistEducationAccountOpeningReasonCode : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "OpeningReasonCode",
            schema: "account",
            table: "EducationAccount",
            type: "varchar(50)",
            unicode: false,
            maxLength: 50,
            nullable: false,
            defaultValue: "MANUAL_LEGACY");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "OpeningReasonCode",
            schema: "account",
            table: "EducationAccount");
    }
}

