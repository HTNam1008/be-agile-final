using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moe.StudentFinance.Persistence;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

[Migration("20260626031955_MakePersonResidencyStatusNullable")]
[DbContextAttribute(typeof(MoeDbContext))]
public partial class MakePersonResidencyStatusNullable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "ResidencyStatusCode",
            schema: "person",
            table: "Person",
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
        migrationBuilder.AlterColumn<string>(
            name: "ResidencyStatusCode",
            schema: "person",
            table: "Person",
            type: "varchar(30)",
            unicode: false,
            maxLength: 30,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "varchar(30)",
            oldUnicode: false,
            oldMaxLength: 30,
            oldNullable: true);
    }
}
