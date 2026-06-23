using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddParentNationalityAndAccountTypeCriteria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_FASTierCriteria_Range",
                schema: "fas",
                table: "FASTierCriteria");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FASTierCriteria_Type",
                schema: "fas",
                table: "FASTierCriteria");

            migrationBuilder.AddColumn<string>(
                name: "AccountTypeCode",
                schema: "fas",
                table: "FASApplication",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: false,
                defaultValue: "PERSONAL_ACCOUNT");

            migrationBuilder.AddColumn<string>(
                name: "ParentNationalitiesJson",
                schema: "fas",
                table: "FASApplication",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_FASTierCriteria_Range",
                schema: "fas",
                table: "FASTierCriteria",
                sql: "([CriteriaType] IN ('NATIONALITY','PARENT_NATIONALITY','ACCOUNT_TYPE') AND [NumberFrom] IS NULL AND [NumberTo] IS NULL) OR ([CriteriaType] NOT IN ('NATIONALITY','PARENT_NATIONALITY','ACCOUNT_TYPE') AND [NumberFrom] IS NOT NULL AND [NumberTo] IS NOT NULL AND [NumberFrom] <= [NumberTo])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FASTierCriteria_Type",
                schema: "fas",
                table: "FASTierCriteria",
                sql: "[CriteriaType] IN ('AGE','GDP','GHI','PCI','NATIONALITY','PARENT_NATIONALITY','ACCOUNT_TYPE')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_FASTierCriteria_Range",
                schema: "fas",
                table: "FASTierCriteria");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FASTierCriteria_Type",
                schema: "fas",
                table: "FASTierCriteria");

            migrationBuilder.DropColumn(
                name: "AccountTypeCode",
                schema: "fas",
                table: "FASApplication");

            migrationBuilder.DropColumn(
                name: "ParentNationalitiesJson",
                schema: "fas",
                table: "FASApplication");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FASTierCriteria_Range",
                schema: "fas",
                table: "FASTierCriteria",
                sql: "([CriteriaType] = 'NATIONALITY' AND [NumberFrom] IS NULL AND [NumberTo] IS NULL) OR ([CriteriaType] <> 'NATIONALITY' AND [NumberFrom] IS NOT NULL AND [NumberTo] IS NOT NULL AND [NumberFrom] <= [NumberTo])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FASTierCriteria_Type",
                schema: "fas",
                table: "FASTierCriteria",
                sql: "[CriteriaType] IN ('AGE','GDP','PCI','NATIONALITY')");
        }
    }
}
