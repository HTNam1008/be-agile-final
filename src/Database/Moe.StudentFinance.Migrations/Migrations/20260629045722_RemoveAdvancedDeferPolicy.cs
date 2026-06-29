using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAdvancedDeferPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeferExtensionRequest",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "OrganizationBillingConfiguration",
                schema: "billing");

            migrationBuilder.DropColumn(
                name: "IsDeferExtensionGranted",
                schema: "billing",
                table: "Bill");

            migrationBuilder.Sql("""
                IF COL_LENGTH('ai.Message', 'ResponseJson') IS NULL
                    ALTER TABLE [ai].[Message] ADD [ResponseJson] nvarchar(8000) NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('ai.Message', 'ResponseJson') IS NOT NULL
                    ALTER TABLE [ai].[Message] DROP COLUMN [ResponseJson];
                """);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeferExtensionGranted",
                schema: "billing",
                table: "Bill",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DeferExtensionRequest",
                schema: "billing",
                columns: table => new
                {
                    DeferExtensionRequestId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BillId = table.Column<long>(type: "bigint", nullable: false),
                    CourseEnrollmentId = table.Column<long>(type: "bigint", nullable: false),
                    DeadlineAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: false),
                    PersonId = table.Column<long>(type: "bigint", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedByLoginAccountId = table.Column<long>(type: "bigint", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByLoginAccountId = table.Column<long>(type: "bigint", nullable: true),
                    StatusCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeferExtensionRequest", x => x.DeferExtensionRequestId);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationBillingConfiguration",
                schema: "billing",
                columns: table => new
                {
                    OrganizationBillingConfigurationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MaxDeferralCount = table.Column<int>(type: "int", nullable: false),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: false),
                    RejectionGracePeriodDays = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByLoginAccountId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationBillingConfiguration", x => x.OrganizationBillingConfigurationId);
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

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationBillingConfiguration_OrganizationId",
                schema: "billing",
                table: "OrganizationBillingConfiguration",
                column: "OrganizationId",
                unique: true);
        }
    }
}
