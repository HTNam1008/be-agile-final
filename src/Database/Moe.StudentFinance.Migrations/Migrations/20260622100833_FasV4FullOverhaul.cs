using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

/// <inheritdoc />
public partial class FasV4FullOverhaul : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "CourseFASScheme",
            schema: "fas");

        migrationBuilder.DropTable(
            name: "FASRule",
            schema: "fas");

        migrationBuilder.DropTable(
            name: "FASSubsidy",
            schema: "fas");

        migrationBuilder.DropTable(
            name: "FASTierBenefit",
            schema: "fas");

        migrationBuilder.DropIndex(
            name: "IX_FASTier_FASSchemeId_TierCode",
            schema: "fas",
            table: "FASTier");

        migrationBuilder.DropColumn(
            name: "TierCode",
            schema: "fas",
            table: "FASTier");

        migrationBuilder.DropColumn(
            name: "ApplicationOpenFrom",
            schema: "fas",
            table: "FASScheme");

        migrationBuilder.DropColumn(
            name: "ApplicationOpenTo",
            schema: "fas",
            table: "FASScheme");

        migrationBuilder.DropColumn(
            name: "ProviderName",
            schema: "fas",
            table: "FASScheme");

        migrationBuilder.RenameColumn(
            name: "StatusCode",
            schema: "fas",
            table: "FASTier",
            newName: "SubsidyType");

        migrationBuilder.RenameColumn(
            name: "PriorityNumber",
            schema: "fas",
            table: "FASTier",
            newName: "DisplayOrder");

        migrationBuilder.RenameColumn(
            name: "TierName",
            schema: "fas",
            table: "FASTier",
            newName: "Label");

        migrationBuilder.RenameColumn(
            name: "SchemeStatusCode",
            schema: "fas",
            table: "FASScheme",
            newName: "StatusCode");

        migrationBuilder.RenameColumn(
            name: "EffectiveFrom",
            schema: "fas",
            table: "FASScheme",
            newName: "StartDate");

        migrationBuilder.RenameColumn(
            name: "EffectiveTo",
            schema: "fas",
            table: "FASScheme",
            newName: "EndDate");

        migrationBuilder.RenameColumn(
            name: "SchemeName",
            schema: "fas",
            table: "FASScheme",
            newName: "Name");

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            schema: "fas",
            table: "FASTier",
            type: "datetime2",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

        migrationBuilder.AlterColumn<string>(
            name: "Label",
            schema: "fas",
            table: "FASTier",
            type: "nvarchar(255)",
            maxLength: 255,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(200)",
            oldMaxLength: 200);

        migrationBuilder.AddColumn<decimal>(
            name: "SubsidyValue",
            schema: "fas",
            table: "FASTier",
            type: "decimal(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAt",
            schema: "fas",
            table: "FASTier",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "SchemeCode",
            schema: "fas",
            table: "FASScheme",
            type: "varchar(50)",
            unicode: false,
            maxLength: 50,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(50)",
            oldMaxLength: 50);

        migrationBuilder.AlterColumn<string>(
            name: "Description",
            schema: "fas",
            table: "FASScheme",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(1000)",
            oldMaxLength: 1000,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            schema: "fas",
            table: "FASScheme",
            type: "nvarchar(255)",
            maxLength: 255,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(200)",
            oldMaxLength: 200);

        migrationBuilder.AddColumn<DateTime>(
            name: "ActivatedAt",
            schema: "fas",
            table: "FASScheme",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "ActivatedByLoginAccountId",
            schema: "fas",
            table: "FASScheme",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            schema: "fas",
            table: "FASScheme",
            type: "datetime2",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

        migrationBuilder.AddColumn<long>(
            name: "CreatedByLoginAccountId",
            schema: "fas",
            table: "FASScheme",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<string>(
            name: "GrantCode",
            schema: "fas",
            table: "FASScheme",
            type: "varchar(100)",
            unicode: false,
            maxLength: 100,
            nullable: false,
            defaultValue: "");

        migrationBuilder.Sql("UPDATE [fas].[FASScheme] SET [EndDate] = [StartDate] WHERE [EndDate] IS NULL;");
        migrationBuilder.AlterColumn<DateOnly>(
            name: "EndDate",
            schema: "fas",
            table: "FASScheme",
            type: "date",
            nullable: false,
            oldClrType: typeof(DateOnly),
            oldType: "date",
            oldNullable: true);

        migrationBuilder.Sql("UPDATE [fas].[FASScheme] SET [GrantCode] = [SchemeCode], [StatusCode] = CASE WHEN [StatusCode] = 'PUBLISHED' THEN 'ACTIVE' WHEN [StatusCode] = 'SUSPENDED' THEN 'RETIRED' WHEN [StatusCode] IN ('DRAFT','ACTIVE','RETIRED') THEN [StatusCode] ELSE 'DRAFT' END;");
        migrationBuilder.Sql("UPDATE [fas].[FASTier] SET [SubsidyType] = 'FIXED';");

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAt",
            schema: "fas",
            table: "FASScheme",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "UpdatedByLoginAccountId",
            schema: "fas",
            table: "FASScheme",
            type: "bigint",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "FASApplicationReviewDecision",
            schema: "fas",
            columns: table => new
            {
                FASApplicationReviewDecisionId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                FASApplicationId = table.Column<long>(type: "bigint", nullable: false),
                Decision = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                ReviewerLoginAccountId = table.Column<long>(type: "bigint", nullable: false),
                ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                RejectionReasonCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                Remarks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FASApplicationReviewDecision", x => x.FASApplicationReviewDecisionId);
                table.CheckConstraint("CK_FASReviewDecision_Decision", "[Decision] IN ('APPROVED','REJECTED')");
                table.CheckConstraint("CK_FASReviewDecision_RejectionReason", "[Decision] <> 'REJECTED' OR [RejectionReasonCode] IS NOT NULL");
                table.ForeignKey(
                    name: "FK_FASApplicationReviewDecision_FASApplication_FASApplicationId",
                    column: x => x.FASApplicationId,
                    principalSchema: "fas",
                    principalTable: "FASApplication",
                    principalColumn: "FASApplicationId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FASSchemeCourse",
            schema: "fas",
            columns: table => new
            {
                FASSchemeId = table.Column<long>(type: "bigint", nullable: false),
                CourseId = table.Column<long>(type: "bigint", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FASSchemeCourse", x => new { x.FASSchemeId, x.CourseId });
                table.ForeignKey(
                    name: "FK_FASSchemeCourse_Course_CourseId",
                    column: x => x.CourseId,
                    principalSchema: "course",
                    principalTable: "Course",
                    principalColumn: "CourseId",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FASSchemeCourse_FASScheme_FASSchemeId",
                    column: x => x.FASSchemeId,
                    principalSchema: "fas",
                    principalTable: "FASScheme",
                    principalColumn: "FASSchemeId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "FASTierCriteria",
            schema: "fas",
            columns: table => new
            {
                FASTierCriteriaId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                FASTierId = table.Column<long>(type: "bigint", nullable: false),
                CriteriaType = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                NumberFrom = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                NumberTo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                ConnectorToNext = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                DisplayOrder = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FASTierCriteria", x => x.FASTierCriteriaId);
                table.CheckConstraint("CK_FASTierCriteria_Connector", "[ConnectorToNext] IS NULL OR [ConnectorToNext] IN ('AND','OR')");
                table.CheckConstraint("CK_FASTierCriteria_Range", "([CriteriaType] = 'NATIONALITY' AND [NumberFrom] IS NULL AND [NumberTo] IS NULL) OR ([CriteriaType] <> 'NATIONALITY' AND [NumberFrom] IS NOT NULL AND [NumberTo] IS NOT NULL AND [NumberFrom] <= [NumberTo])");
                table.CheckConstraint("CK_FASTierCriteria_Type", "[CriteriaType] IN ('AGE','GDP','PCI','NATIONALITY')");
                table.ForeignKey(
                    name: "FK_FASTierCriteria_FASTier_FASTierId",
                    column: x => x.FASTierId,
                    principalSchema: "fas",
                    principalTable: "FASTier",
                    principalColumn: "FASTierId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "FASTierCriteriaNationality",
            schema: "fas",
            columns: table => new
            {
                FASTierCriteriaId = table.Column<long>(type: "bigint", nullable: false),
                Nationality = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FASTierCriteriaNationality", x => new { x.FASTierCriteriaId, x.Nationality });
                table.ForeignKey(
                    name: "FK_FASTierCriteriaNationality_FASTierCriteria_FASTierCriteriaId",
                    column: x => x.FASTierCriteriaId,
                    principalSchema: "fas",
                    principalTable: "FASTierCriteria",
                    principalColumn: "FASTierCriteriaId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_FASTier_FASSchemeId_DisplayOrder",
            schema: "fas",
            table: "FASTier",
            columns: new[] { "FASSchemeId", "DisplayOrder" },
            unique: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_FASTier_SubsidyType",
            schema: "fas",
            table: "FASTier",
            sql: "[SubsidyType] IN ('FIXED','PERCENTAGE')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_FASTier_SubsidyValue",
            schema: "fas",
            table: "FASTier",
            sql: "[SubsidyValue] >= 0 AND ([SubsidyType] <> 'PERCENTAGE' OR [SubsidyValue] <= 100)");

        migrationBuilder.CreateIndex(
            name: "IX_FASScheme_GrantCode",
            schema: "fas",
            table: "FASScheme",
            column: "GrantCode",
            unique: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_FASScheme_Dates",
            schema: "fas",
            table: "FASScheme",
            sql: "[EndDate] >= [StartDate]");

        migrationBuilder.AddCheckConstraint(
            name: "CK_FASScheme_Status",
            schema: "fas",
            table: "FASScheme",
            sql: "[StatusCode] IN ('DRAFT','ACTIVE','RETIRED')");

        migrationBuilder.CreateIndex(
            name: "IX_FASApplicationReviewDecision_FASApplicationId",
            schema: "fas",
            table: "FASApplicationReviewDecision",
            column: "FASApplicationId");

        migrationBuilder.CreateIndex(
            name: "IX_FASSchemeCourse_CourseId",
            schema: "fas",
            table: "FASSchemeCourse",
            column: "CourseId");

        migrationBuilder.CreateIndex(
            name: "IX_FASTierCriteria_FASTierId_DisplayOrder",
            schema: "fas",
            table: "FASTierCriteria",
            columns: new[] { "FASTierId", "DisplayOrder" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_FASTier_FASScheme_FASSchemeId",
            schema: "fas",
            table: "FASTier",
            column: "FASSchemeId",
            principalSchema: "fas",
            principalTable: "FASScheme",
            principalColumn: "FASSchemeId",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_FASTier_FASScheme_FASSchemeId",
            schema: "fas",
            table: "FASTier");

        migrationBuilder.DropTable(
            name: "FASApplicationReviewDecision",
            schema: "fas");

        migrationBuilder.DropTable(
            name: "FASSchemeCourse",
            schema: "fas");

        migrationBuilder.DropTable(
            name: "FASTierCriteriaNationality",
            schema: "fas");

        migrationBuilder.DropTable(
            name: "FASTierCriteria",
            schema: "fas");

        migrationBuilder.DropIndex(
            name: "IX_FASTier_FASSchemeId_DisplayOrder",
            schema: "fas",
            table: "FASTier");

        migrationBuilder.DropCheckConstraint(
            name: "CK_FASTier_SubsidyType",
            schema: "fas",
            table: "FASTier");

        migrationBuilder.DropCheckConstraint(
            name: "CK_FASTier_SubsidyValue",
            schema: "fas",
            table: "FASTier");

        migrationBuilder.DropIndex(
            name: "IX_FASScheme_GrantCode",
            schema: "fas",
            table: "FASScheme");

        migrationBuilder.DropCheckConstraint(
            name: "CK_FASScheme_Dates",
            schema: "fas",
            table: "FASScheme");

        migrationBuilder.DropCheckConstraint(
            name: "CK_FASScheme_Status",
            schema: "fas",
            table: "FASScheme");

        migrationBuilder.DropColumn(
            name: "CreatedAt",
            schema: "fas",
            table: "FASTier");

        migrationBuilder.DropColumn(
            name: "Label",
            schema: "fas",
            table: "FASTier");

        migrationBuilder.DropColumn(
            name: "SubsidyValue",
            schema: "fas",
            table: "FASTier");

        migrationBuilder.DropColumn(
            name: "UpdatedAt",
            schema: "fas",
            table: "FASTier");

        migrationBuilder.DropColumn(
            name: "ActivatedAt",
            schema: "fas",
            table: "FASScheme");

        migrationBuilder.DropColumn(
            name: "ActivatedByLoginAccountId",
            schema: "fas",
            table: "FASScheme");

        migrationBuilder.DropColumn(
            name: "CreatedAt",
            schema: "fas",
            table: "FASScheme");

        migrationBuilder.DropColumn(
            name: "CreatedByLoginAccountId",
            schema: "fas",
            table: "FASScheme");

        migrationBuilder.DropColumn(
            name: "EndDate",
            schema: "fas",
            table: "FASScheme");

        migrationBuilder.DropColumn(
            name: "GrantCode",
            schema: "fas",
            table: "FASScheme");

        migrationBuilder.DropColumn(
            name: "Name",
            schema: "fas",
            table: "FASScheme");

        migrationBuilder.DropColumn(
            name: "UpdatedAt",
            schema: "fas",
            table: "FASScheme");

        migrationBuilder.DropColumn(
            name: "UpdatedByLoginAccountId",
            schema: "fas",
            table: "FASScheme");

        migrationBuilder.RenameColumn(
            name: "SubsidyType",
            schema: "fas",
            table: "FASTier",
            newName: "StatusCode");

        migrationBuilder.RenameColumn(
            name: "DisplayOrder",
            schema: "fas",
            table: "FASTier",
            newName: "PriorityNumber");

        migrationBuilder.RenameColumn(
            name: "StatusCode",
            schema: "fas",
            table: "FASScheme",
            newName: "SchemeStatusCode");

        migrationBuilder.RenameColumn(
            name: "StartDate",
            schema: "fas",
            table: "FASScheme",
            newName: "EffectiveFrom");

        migrationBuilder.AddColumn<string>(
            name: "TierCode",
            schema: "fas",
            table: "FASTier",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "TierName",
            schema: "fas",
            table: "FASTier",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AlterColumn<string>(
            name: "SchemeCode",
            schema: "fas",
            table: "FASScheme",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(50)",
            oldUnicode: false,
            oldMaxLength: 50);

        migrationBuilder.AlterColumn<string>(
            name: "Description",
            schema: "fas",
            table: "FASScheme",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(2000)",
            oldMaxLength: 2000,
            oldNullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "ApplicationOpenFrom",
            schema: "fas",
            table: "FASScheme",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "ApplicationOpenTo",
            schema: "fas",
            table: "FASScheme",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "EffectiveTo",
            schema: "fas",
            table: "FASScheme",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProviderName",
            schema: "fas",
            table: "FASScheme",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "SchemeName",
            schema: "fas",
            table: "FASScheme",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateTable(
            name: "CourseFASScheme",
            schema: "fas",
            columns: table => new
            {
                CourseFASSchemeId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                CourseId = table.Column<long>(type: "bigint", nullable: false),
                FASSchemeId = table.Column<long>(type: "bigint", nullable: false),
                StatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CourseFASScheme", x => x.CourseFASSchemeId);
            });

        migrationBuilder.CreateTable(
            name: "FASRule",
            schema: "fas",
            columns: table => new
            {
                FASRuleId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                CriterionCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                FASTierId = table.Column<long>(type: "bigint", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                NumericValueFrom = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: true),
                NumericValueTo = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: true),
                OperatorCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                RuleGroupNumber = table.Column<int>(type: "int", nullable: false),
                SequenceNumber = table.Column<int>(type: "int", nullable: false),
                TextValue = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FASRule", x => x.FASRuleId);
            });

        migrationBuilder.CreateTable(
            name: "FASSubsidy",
            schema: "fas",
            columns: table => new
            {
                FASSubsidyId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                AppliedAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                BillLineId = table.Column<long>(type: "bigint", nullable: false),
                CalculatedAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                FASApplicationId = table.Column<long>(type: "bigint", nullable: false),
                FASTierBenefitId = table.Column<long>(type: "bigint", nullable: false),
                GrossAmountSnapshot = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                SubsidyStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FASSubsidy", x => x.FASSubsidyId);
            });

        migrationBuilder.CreateTable(
            name: "FASTierBenefit",
            schema: "fas",
            columns: table => new
            {
                FASTierBenefitId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                FASTierId = table.Column<long>(type: "bigint", nullable: false),
                FeeComponentId = table.Column<long>(type: "bigint", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                MaximumSubsidyAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: true),
                SubsidyTypeCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                SubsidyValue = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FASTierBenefit", x => x.FASTierBenefitId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_FASTier_FASSchemeId_TierCode",
            schema: "fas",
            table: "FASTier",
            columns: new[] { "FASSchemeId", "TierCode" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CourseFASScheme_CourseId_FASSchemeId",
            schema: "fas",
            table: "CourseFASScheme",
            columns: new[] { "CourseId", "FASSchemeId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_FASRule_FASTierId",
            schema: "fas",
            table: "FASRule",
            column: "FASTierId");

        migrationBuilder.CreateIndex(
            name: "IX_FASSubsidy_BillLineId",
            schema: "fas",
            table: "FASSubsidy",
            column: "BillLineId");

        migrationBuilder.CreateIndex(
            name: "IX_FASSubsidy_FASApplicationId",
            schema: "fas",
            table: "FASSubsidy",
            column: "FASApplicationId");

        migrationBuilder.CreateIndex(
            name: "IX_FASTierBenefit_FASTierId_FeeComponentId",
            schema: "fas",
            table: "FASTierBenefit",
            columns: new[] { "FASTierId", "FeeComponentId" });
    }
}
