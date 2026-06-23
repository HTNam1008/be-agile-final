using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class HardenStudentFasWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "BlobKey",
                schema: "fas",
                table: "FASDocument",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "RejectionNotes",
                schema: "fas",
                table: "FASApplicationScheme",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeactivatedAtUtc",
                schema: "fas",
                table: "FASActiveScheme",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DeactivatedByLoginAccountId",
                schema: "fas",
                table: "FASActiveScheme",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeactivatedReason",
                schema: "fas",
                table: "FASActiveScheme",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FASDocument_ReplacedByDocumentId",
                schema: "fas",
                table: "FASDocument",
                column: "ReplacedByDocumentId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FASDocument_Size",
                schema: "fas",
                table: "FASDocument",
                sql: "[FileSizeBytes] > 0 AND [FileSizeBytes] <= 10485760");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FASDocument_Status",
                schema: "fas",
                table: "FASDocument",
                sql: "[UploadStatusCode] IN ('UPLOADED','REMOVED','SCAN_PENDING','SCAN_PASSED','SCAN_FAILED')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FASDocument_Type",
                schema: "fas",
                table: "FASDocument",
                sql: "[DocumentTypeCode] IN ('PAYSLIP','CPF_STATEMENT','NOA','WELFARE_LETTER','OTHER','INCOME_PROOF')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FASDeclaration_Type",
                schema: "fas",
                table: "FASDeclaration",
                sql: "[DeclarationTypeCode] IN ('TRUE_AND_ACCURATE','ACCEPT_TERMS')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FASApplicationScheme_RejectionNotes",
                schema: "fas",
                table: "FASApplicationScheme",
                sql: "[StatusCode] <> 'REJECTED' OR LEN(LTRIM(RTRIM([RejectionNotes]))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FASApplicationScheme_Validity",
                schema: "fas",
                table: "FASApplicationScheme",
                sql: "[ValidFrom] IS NULL OR [ValidTo] IS NULL OR [ValidTo] >= [ValidFrom]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FASApplication_HouseholdSize",
                schema: "fas",
                table: "FASApplication",
                sql: "[HouseholdSizeSnapshot] IS NULL OR [HouseholdSizeSnapshot] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FASApplication_Income",
                schema: "fas",
                table: "FASApplication",
                sql: "[HouseholdIncomeSnapshot] IS NULL OR [HouseholdIncomeSnapshot] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FASApplication_PCI",
                schema: "fas",
                table: "FASApplication",
                sql: "[PerCapitaIncomeSnapshot] IS NULL OR [PerCapitaIncomeSnapshot] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FASApplication_Status",
                schema: "fas",
                table: "FASApplication",
                sql: "[ApplicationStatusCode] IN ('DRAFT','SUBMITTED','WITHDRAWN','PENDING_REVIEW','APPROVED','REJECTED')");

            migrationBuilder.CreateIndex(
                name: "IX_FASActiveScheme_FasSchemeId",
                schema: "fas",
                table: "FASActiveScheme",
                column: "FasSchemeId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FASActiveScheme_Status",
                schema: "fas",
                table: "FASActiveScheme",
                sql: "[StatusCode] IN ('ACTIVE','EXPIRED','DEACTIVATED')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FASActiveScheme_Validity",
                schema: "fas",
                table: "FASActiveScheme",
                sql: "[ActiveTo] >= [ActiveFrom]");

            migrationBuilder.AddForeignKey(
                name: "FK_FASActiveScheme_FASApplicationScheme_FasApplicationSchemeId",
                schema: "fas",
                table: "FASActiveScheme",
                column: "FasApplicationSchemeId",
                principalSchema: "fas",
                principalTable: "FASApplicationScheme",
                principalColumn: "FASApplicationSchemeId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FASActiveScheme_FASScheme_FasSchemeId",
                schema: "fas",
                table: "FASActiveScheme",
                column: "FasSchemeId",
                principalSchema: "fas",
                principalTable: "FASScheme",
                principalColumn: "FASSchemeId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FASDocument_FASDocument_ReplacedByDocumentId",
                schema: "fas",
                table: "FASDocument",
                column: "ReplacedByDocumentId",
                principalSchema: "fas",
                principalTable: "FASDocument",
                principalColumn: "FASDocumentId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FASActiveScheme_FASApplicationScheme_FasApplicationSchemeId",
                schema: "fas",
                table: "FASActiveScheme");

            migrationBuilder.DropForeignKey(
                name: "FK_FASActiveScheme_FASScheme_FasSchemeId",
                schema: "fas",
                table: "FASActiveScheme");

            migrationBuilder.DropForeignKey(
                name: "FK_FASDocument_FASDocument_ReplacedByDocumentId",
                schema: "fas",
                table: "FASDocument");

            migrationBuilder.DropIndex(
                name: "IX_FASDocument_ReplacedByDocumentId",
                schema: "fas",
                table: "FASDocument");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FASDocument_Size",
                schema: "fas",
                table: "FASDocument");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FASDocument_Status",
                schema: "fas",
                table: "FASDocument");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FASDocument_Type",
                schema: "fas",
                table: "FASDocument");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FASDeclaration_Type",
                schema: "fas",
                table: "FASDeclaration");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FASApplicationScheme_RejectionNotes",
                schema: "fas",
                table: "FASApplicationScheme");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FASApplicationScheme_Validity",
                schema: "fas",
                table: "FASApplicationScheme");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FASApplication_HouseholdSize",
                schema: "fas",
                table: "FASApplication");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FASApplication_Income",
                schema: "fas",
                table: "FASApplication");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FASApplication_PCI",
                schema: "fas",
                table: "FASApplication");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FASApplication_Status",
                schema: "fas",
                table: "FASApplication");

            migrationBuilder.DropIndex(
                name: "IX_FASActiveScheme_FasSchemeId",
                schema: "fas",
                table: "FASActiveScheme");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FASActiveScheme_Status",
                schema: "fas",
                table: "FASActiveScheme");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FASActiveScheme_Validity",
                schema: "fas",
                table: "FASActiveScheme");

            migrationBuilder.DropColumn(
                name: "DeactivatedAtUtc",
                schema: "fas",
                table: "FASActiveScheme");

            migrationBuilder.DropColumn(
                name: "DeactivatedByLoginAccountId",
                schema: "fas",
                table: "FASActiveScheme");

            migrationBuilder.DropColumn(
                name: "DeactivatedReason",
                schema: "fas",
                table: "FASActiveScheme");

            migrationBuilder.AlterColumn<string>(
                name: "BlobKey",
                schema: "fas",
                table: "FASDocument",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "RejectionNotes",
                schema: "fas",
                table: "FASApplicationScheme",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);
        }
    }
}
