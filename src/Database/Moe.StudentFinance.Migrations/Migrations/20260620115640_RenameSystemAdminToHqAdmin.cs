using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

/// <inheritdoc />
public partial class RenameSystemAdminToHqAdmin : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE [iam].[LoginAccount]
            SET [RoleCode] = 'HQ_ADMIN'
            WHERE [RoleCode] = 'SYSTEM_ADMIN';

            UPDATE [iam].[UserAccessScope]
            SET [RoleCode] = 'HQ_ADMIN'
            WHERE [RoleCode] = 'SYSTEM_ADMIN';

            UPDATE [iam].[RolePermission]
            SET [RoleCode] = 'HQ_ADMIN'
            WHERE [RoleCode] = 'SYSTEM_ADMIN';
            """);

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "LoginAccount",
            keyColumn: "LoginAccountId",
            keyValue: 1001L,
            columns: new[] { "DisplayNameSnapshot", "ProviderDisplayName", "RoleCode" },
            values: new object[] { "MOE HQ Admin", "MOE HQ Admin", "HQ_ADMIN" });

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 1L,
            column: "RoleCode",
            value: "HQ_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 2L,
            column: "RoleCode",
            value: "HQ_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 3L,
            column: "RoleCode",
            value: "HQ_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 4L,
            column: "RoleCode",
            value: "HQ_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 5L,
            column: "RoleCode",
            value: "HQ_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 6L,
            column: "RoleCode",
            value: "HQ_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 7L,
            column: "RoleCode",
            value: "HQ_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 8L,
            column: "RoleCode",
            value: "HQ_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 9L,
            column: "RoleCode",
            value: "HQ_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 10L,
            column: "RoleCode",
            value: "HQ_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 11L,
            column: "RoleCode",
            value: "HQ_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 12L,
            column: "RoleCode",
            value: "HQ_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 13L,
            column: "RoleCode",
            value: "HQ_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 14L,
            column: "RoleCode",
            value: "HQ_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 15L,
            column: "RoleCode",
            value: "HQ_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 16L,
            column: "RoleCode",
            value: "HQ_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 17L,
            column: "RoleCode",
            value: "HQ_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 18L,
            column: "RoleCode",
            value: "HQ_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "UserAccessScope",
            keyColumn: "UserAccessScopeId",
            keyValue: 1001L,
            column: "RoleCode",
            value: "HQ_ADMIN");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE [iam].[LoginAccount]
            SET [RoleCode] = 'SYSTEM_ADMIN'
            WHERE [RoleCode] = 'HQ_ADMIN';

            UPDATE [iam].[UserAccessScope]
            SET [RoleCode] = 'SYSTEM_ADMIN'
            WHERE [RoleCode] = 'HQ_ADMIN';

            UPDATE [iam].[RolePermission]
            SET [RoleCode] = 'SYSTEM_ADMIN'
            WHERE [RoleCode] = 'HQ_ADMIN';
            """);

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "LoginAccount",
            keyColumn: "LoginAccountId",
            keyValue: 1001L,
            columns: new[] { "DisplayNameSnapshot", "ProviderDisplayName", "RoleCode" },
            values: new object[] { "MOE System Admin", "MOE System Admin", "SYSTEM_ADMIN" });

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 1L,
            column: "RoleCode",
            value: "SYSTEM_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 2L,
            column: "RoleCode",
            value: "SYSTEM_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 3L,
            column: "RoleCode",
            value: "SYSTEM_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 4L,
            column: "RoleCode",
            value: "SYSTEM_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 5L,
            column: "RoleCode",
            value: "SYSTEM_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 6L,
            column: "RoleCode",
            value: "SYSTEM_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 7L,
            column: "RoleCode",
            value: "SYSTEM_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 8L,
            column: "RoleCode",
            value: "SYSTEM_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 9L,
            column: "RoleCode",
            value: "SYSTEM_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 10L,
            column: "RoleCode",
            value: "SYSTEM_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 11L,
            column: "RoleCode",
            value: "SYSTEM_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 12L,
            column: "RoleCode",
            value: "SYSTEM_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 13L,
            column: "RoleCode",
            value: "SYSTEM_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 14L,
            column: "RoleCode",
            value: "SYSTEM_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 15L,
            column: "RoleCode",
            value: "SYSTEM_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 16L,
            column: "RoleCode",
            value: "SYSTEM_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 17L,
            column: "RoleCode",
            value: "SYSTEM_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 18L,
            column: "RoleCode",
            value: "SYSTEM_ADMIN");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "UserAccessScope",
            keyColumn: "UserAccessScopeId",
            keyValue: 1001L,
            column: "RoleCode",
            value: "SYSTEM_ADMIN");
    }
}
