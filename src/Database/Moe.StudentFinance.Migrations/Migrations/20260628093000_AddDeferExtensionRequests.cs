using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moe.StudentFinance.Persistence;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

[Migration("20260628093000_AddDeferExtensionRequests")]
[DbContext(typeof(MoeDbContext))]
public partial class AddDeferExtensionRequests : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DeferExtensionRequest",
            schema: "billing",
            columns: table => new
            {
                DeferExtensionRequestId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                BillId = table.Column<long>(type: "bigint", nullable: false),
                CourseEnrollmentId = table.Column<long>(type: "bigint", nullable: false),
                PersonId = table.Column<long>(type: "bigint", nullable: false),
                OrganizationId = table.Column<long>(type: "bigint", nullable: false),
                StatusCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                RequestedByLoginAccountId = table.Column<long>(type: "bigint", nullable: false),
                RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ReviewedByLoginAccountId = table.Column<long>(type: "bigint", nullable: true),
                ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeadlineAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DeferExtensionRequest", x => x.DeferExtensionRequestId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DeferExtensionRequest_BillId_StatusCode",
            schema: "billing",
            table: "DeferExtensionRequest",
            columns: new[] { "BillId", "StatusCode" },
            filter: "[StatusCode] = 'PENDING'");

        migrationBuilder.CreateIndex(
            name: "IX_DeferExtensionRequest_OrganizationId_StatusCode_RequestedAtUtc",
            schema: "billing",
            table: "DeferExtensionRequest",
            columns: new[] { "OrganizationId", "StatusCode", "RequestedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "DeferExtensionRequest",
            schema: "billing");
    }
}
