using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moe.StudentFinance.Persistence;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MoeDbContext))]
    [Migration("20260624043000_AddMfaAuditResetMetadata")]
    public partial class AddMfaAuditResetMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PerformedByAccountId",
                schema: "iam",
                table: "LoginMfaAuditEvent",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                schema: "iam",
                table: "LoginMfaAuditEvent",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoginMfaAuditEvent_PerformedByAccountId",
                schema: "iam",
                table: "LoginMfaAuditEvent",
                column: "PerformedByAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_LoginMfaAuditEvent_PerformedByAccount",
                schema: "iam",
                table: "LoginMfaAuditEvent",
                column: "PerformedByAccountId",
                principalSchema: "iam",
                principalTable: "LoginAccount",
                principalColumn: "LoginAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LoginMfaAuditEvent_PerformedByAccount",
                schema: "iam",
                table: "LoginMfaAuditEvent");

            migrationBuilder.DropIndex(
                name: "IX_LoginMfaAuditEvent_PerformedByAccountId",
                schema: "iam",
                table: "LoginMfaAuditEvent");

            migrationBuilder.DropColumn(
                name: "PerformedByAccountId",
                schema: "iam",
                table: "LoginMfaAuditEvent");

            migrationBuilder.DropColumn(
                name: "Reason",
                schema: "iam",
                table: "LoginMfaAuditEvent");
        }
    }
}
