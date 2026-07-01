using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

/// <inheritdoc />
public partial class RemovePortalAccessCodeFromMfa : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_LoginMfaChallenge_PortalAccessCode",
            schema: "iam",
            table: "LoginMfaChallenge");

        migrationBuilder.DropColumn(
            name: "PortalAccessCode",
            schema: "iam",
            table: "LoginMfaChallenge");

        migrationBuilder.DropColumn(
            name: "PortalAccessCode",
            schema: "iam",
            table: "LoginMfaAuditEvent");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PortalAccessCode",
            schema: "iam",
            table: "LoginMfaChallenge",
            type: "varchar(30)",
            unicode: false,
            maxLength: 30,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "PortalAccessCode",
            schema: "iam",
            table: "LoginMfaAuditEvent",
            type: "varchar(30)",
            unicode: false,
            maxLength: 30,
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_LoginMfaChallenge_PortalAccessCode",
            schema: "iam",
            table: "LoginMfaChallenge",
            sql: "[PortalAccessCode] IN ('ADMIN', 'ESERVICE')");
    }
}
