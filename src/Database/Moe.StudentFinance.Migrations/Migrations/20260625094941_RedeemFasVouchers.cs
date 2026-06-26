using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moe.StudentFinance.Persistence;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

/// <inheritdoc />
[DbContext(typeof(MoeDbContext))]
[Migration("20260625094941_RedeemFasVouchers")]
public partial class RedeemFasVouchers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE [fas].[FASApplicationScheme] DROP CONSTRAINT [CK_FASApplicationScheme_Status];
            ALTER TABLE [fas].[FASApplicationScheme]
            ADD CONSTRAINT [CK_FASApplicationScheme_Status]
            CHECK ([StatusCode] IN ('DRAFT','PENDING','APPROVED','REJECTED','CANCELLED','EXPIRED','REDEEMED'));
            """);

        migrationBuilder.AddColumn<DateTime>(
            name: "RedeemedAt",
            schema: "fas",
            table: "FASApplicationScheme",
            type: "datetime2",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "FASVoucherRedemption",
            schema: "fas",
            columns: table => new
            {
                FASVoucherRedemptionId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                StudentPersonId = table.Column<long>(type: "bigint", nullable: false),
                FasApplicationSchemeId = table.Column<long>(type: "bigint", nullable: false),
                CourseId = table.Column<long>(type: "bigint", nullable: false),
                CourseEnrollmentId = table.Column<long>(type: "bigint", nullable: false),
                BillId = table.Column<long>(type: "bigint", nullable: false),
                AppliedAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                StatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                RedeemedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FASVoucherRedemption", x => x.FASVoucherRedemptionId);
                table.CheckConstraint("CK_FASVoucherRedemption_Status", "[StatusCode] IN ('PENDING','REDEEMED','CANCELLED')");
                table.ForeignKey(
                    name: "FK_FASVoucherRedemption_FASApplicationScheme_FasApplicationSchemeId",
                    column: x => x.FasApplicationSchemeId,
                    principalSchema: "fas",
                    principalTable: "FASApplicationScheme",
                    principalColumn: "FASApplicationSchemeId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_FASVoucherRedemption_BillId",
            schema: "fas",
            table: "FASVoucherRedemption",
            column: "BillId");

        migrationBuilder.CreateIndex(
            name: "IX_FASVoucherRedemption_CourseEnrollmentId",
            schema: "fas",
            table: "FASVoucherRedemption",
            column: "CourseEnrollmentId");

        migrationBuilder.CreateIndex(
            name: "IX_FASVoucherRedemption_FasApplicationSchemeId",
            schema: "fas",
            table: "FASVoucherRedemption",
            column: "FasApplicationSchemeId",
            unique: true,
            filter: "[StatusCode] <> 'CANCELLED'");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "FASVoucherRedemption",
            schema: "fas");

        migrationBuilder.DropColumn(
            name: "RedeemedAt",
            schema: "fas",
            table: "FASApplicationScheme");

        migrationBuilder.Sql("""
            ALTER TABLE [fas].[FASApplicationScheme] DROP CONSTRAINT [CK_FASApplicationScheme_Status];
            ALTER TABLE [fas].[FASApplicationScheme]
            ADD CONSTRAINT [CK_FASApplicationScheme_Status]
            CHECK ([StatusCode] IN ('DRAFT','PENDING','APPROVED','REJECTED','CANCELLED','EXPIRED'));
            """);
    }
}
