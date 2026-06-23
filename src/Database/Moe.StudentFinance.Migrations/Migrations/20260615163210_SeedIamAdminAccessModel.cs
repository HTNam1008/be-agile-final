using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Moe.StudentFinance.Migrations.Migrations;

/// <inheritdoc />
public partial class SeedIamAdminAccessModel : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.InsertData(
            schema: "iam",
            table: "OrganizationUnit",
            columns: new[] { "OrganizationUnitId", "EffectiveFromUtc", "EffectiveToUtc", "ParentOrganizationUnitId", "StatusCode", "UnitCode", "UnitName", "UnitTypeCode" },
            values: new object[] { 1L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, "ACTIVE", "MOE_HQ", "Ministry of Education Headquarters", "HQ" });

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "EXTERNAL_ACCOUNTS_PROVISION",
            column: "PermissionName",
            value: "Create admin users and prepare student Singpass access");

        migrationBuilder.InsertData(
            schema: "iam",
            table: "RolePermission",
            columns: new[] { "RolePermissionId", "EffectiveFromUtc", "EffectiveToUtc", "PermissionCode", "RoleCode", "StatusCode" },
            values: new object[,]
            {
                { 1L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ACCESS_SCOPE_MANAGE", "SUPER_ADMIN", "ACTIVE" },
                { 2L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "EXTERNAL_ACCOUNTS_PROVISION", "SUPER_ADMIN", "ACTIVE" },
                { 3L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ACCOUNTS_VIEW", "SUPER_ADMIN", "ACTIVE" },
                { 4L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ACCOUNTS_MANAGE", "SUPER_ADMIN", "ACTIVE" },
                { 5L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "TOPUPS_MANAGE", "SUPER_ADMIN", "ACTIVE" },
                { 6L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "COURSES_MANAGE", "SUPER_ADMIN", "ACTIVE" },
                { 7L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "FAS_REVIEW", "SUPER_ADMIN", "ACTIVE" },
                { 8L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PAYMENT_EXCEPTIONS_REVIEW", "SUPER_ADMIN", "ACTIVE" },
                { 9L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ACCESS_SCOPE_MANAGE", "IDENTITY_ADMIN", "ACTIVE" },
                { 10L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "EXTERNAL_ACCOUNTS_PROVISION", "IDENTITY_ADMIN", "ACTIVE" },
                { 11L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ACCOUNTS_VIEW", "FINANCE_ADMIN", "ACTIVE" },
                { 12L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ACCOUNTS_MANAGE", "FINANCE_ADMIN", "ACTIVE" },
                { 13L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "TOPUPS_MANAGE", "FINANCE_ADMIN", "ACTIVE" }
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            schema: "iam",
            table: "OrganizationUnit",
            keyColumn: "OrganizationUnitId",
            keyValue: 1L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 1L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 2L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 3L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 4L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 5L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 6L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 7L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 8L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 9L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 10L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 11L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 12L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 13L);

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "EXTERNAL_ACCOUNTS_PROVISION",
            column: "PermissionName",
            value: "Prepare student Singpass access");
    }
}
