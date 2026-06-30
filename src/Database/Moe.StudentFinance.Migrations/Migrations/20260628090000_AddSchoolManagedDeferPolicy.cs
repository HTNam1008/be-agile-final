using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moe.StudentFinance.Persistence;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

[Migration("20260628090000_AddSchoolManagedDeferPolicy")]
[DbContext(typeof(MoeDbContext))]
public partial class AddSchoolManagedDeferPolicy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsDeferExtensionGranted",
            schema: "billing",
            table: "Bill",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.DropIndex(
            name: "IX_BillDeferral_BillId_FailedPaymentId",
            schema: "billing",
            table: "BillDeferral");

        migrationBuilder.RenameColumn(
            name: "FailedPaymentId",
            schema: "billing",
            table: "BillDeferral",
            newName: "SourcePaymentId");

        migrationBuilder.AlterColumn<long>(
            name: "SourcePaymentId",
            schema: "billing",
            table: "BillDeferral",
            type: "bigint",
            nullable: true,
            oldClrType: typeof(long),
            oldType: "bigint");

        migrationBuilder.CreateIndex(
            name: "IX_BillDeferral_BillId_SourcePaymentId",
            schema: "billing",
            table: "BillDeferral",
            columns: new[] { "BillId", "SourcePaymentId" });

        migrationBuilder.CreateTable(
            name: "OrganizationBillingConfiguration",
            schema: "billing",
            columns: table => new
            {
                OrganizationBillingConfigurationId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                OrganizationId = table.Column<long>(type: "bigint", nullable: false),
                MaxDeferralCount = table.Column<int>(type: "int", nullable: false),
                RejectionGracePeriodDays = table.Column<int>(type: "int", nullable: false),
                UpdatedByLoginAccountId = table.Column<long>(type: "bigint", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationBillingConfiguration", x => x.OrganizationBillingConfigurationId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationBillingConfiguration_OrganizationId",
            schema: "billing",
            table: "OrganizationBillingConfiguration",
            column: "OrganizationId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OrganizationBillingConfiguration",
            schema: "billing");

        migrationBuilder.DropIndex(
            name: "IX_BillDeferral_BillId_SourcePaymentId",
            schema: "billing",
            table: "BillDeferral");

        migrationBuilder.AlterColumn<long>(
            name: "SourcePaymentId",
            schema: "billing",
            table: "BillDeferral",
            type: "bigint",
            nullable: false,
            defaultValue: 0L,
            oldClrType: typeof(long),
            oldType: "bigint",
            oldNullable: true);

        migrationBuilder.RenameColumn(
            name: "SourcePaymentId",
            schema: "billing",
            table: "BillDeferral",
            newName: "FailedPaymentId");

        migrationBuilder.CreateIndex(
            name: "IX_BillDeferral_BillId_FailedPaymentId",
            schema: "billing",
            table: "BillDeferral",
            columns: new[] { "BillId", "FailedPaymentId" },
            unique: true);

        migrationBuilder.DropColumn(
            name: "IsDeferExtensionGranted",
            schema: "billing",
            table: "Bill");
    }
}
