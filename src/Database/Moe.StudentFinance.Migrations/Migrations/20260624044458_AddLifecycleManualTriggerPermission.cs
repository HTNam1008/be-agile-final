using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

/// <inheritdoc />
public partial class AddLifecycleManualTriggerPermission : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.InsertData(
            schema: "iam",
            table: "Permission",
            columns: new[] { "PermissionCode", "ActionCode", "ModuleCode", "PermissionName", "ResourceCode", "StatusCode" },
            values: new object[] { "LIFECYCLE_MANUAL_TRIGGER", "TRIGGER", "EDUCATION_ACCOUNT_TOPUP", "Trigger education account lifecycle manually", "ACCOUNT_LIFECYCLE", "ACTIVE" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "LIFECYCLE_MANUAL_TRIGGER");
    }
}
