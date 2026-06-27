using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moe.StudentFinance.Persistence;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

[Migration("20260627125000_AddAuditLogSchoolScopeIndex")]
[DbContext(typeof(MoeDbContext))]
public partial class AddAuditLogSchoolScopeIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_AuditLog_OrganizationId_OccurredAt",
            schema: "audit",
            table: "AuditLog",
            columns: new[] { "OrganizationId", "OccurredAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AuditLog_OrganizationId_OccurredAt",
            schema: "audit",
            table: "AuditLog");
    }
}
