using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moe.StudentFinance.Persistence;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

[Migration("20260625064000_MakeSchoolEnrollmentClassCodeOptional")]
[DbContextAttribute(typeof(MoeDbContext))]
public partial class MakeSchoolEnrollmentClassCodeOptional : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "ClassCode",
            schema: "person",
            table: "SchoolEnrollment",
            type: "varchar(30)",
            unicode: false,
            maxLength: 30,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(30)",
            oldUnicode: false,
            oldMaxLength: 30);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE [person].[SchoolEnrollment]
            SET [ClassCode] = ''
            WHERE [ClassCode] IS NULL
            """);

        migrationBuilder.AlterColumn<string>(
            name: "ClassCode",
            schema: "person",
            table: "SchoolEnrollment",
            type: "varchar(30)",
            unicode: false,
            maxLength: 30,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(30)",
            oldUnicode: false,
            oldMaxLength: 30,
            oldNullable: true);
    }
}
