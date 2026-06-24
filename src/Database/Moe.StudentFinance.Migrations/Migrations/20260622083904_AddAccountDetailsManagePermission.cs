using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Moe.StudentFinance.Migrations.Migrations;

/// <inheritdoc />
public partial class AddAccountDetailsManagePermission : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.InsertData(
            schema: "iam",
            table: "Permission",
            columns: new[] { "PermissionCode", "ActionCode", "ModuleCode", "PermissionName", "ResourceCode", "StatusCode" },
            values: new object[] { "ACCOUNT_DETAILS_MANAGE", "MANAGE", "EDUCATION_ACCOUNT_TOPUP", "Manage education account details", "ACCOUNT_DETAILS", "ACTIVE" });

        migrationBuilder.InsertData(
            schema: "iam",
            table: "RolePermission",
            columns: new[] { "RolePermissionId", "EffectiveFromUtc", "EffectiveToUtc", "PermissionCode", "RoleCode", "StatusCode" },
            values: new object[,]
            {
                { 27L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ACCOUNT_DETAILS_MANAGE", "HQ_ADMIN", "ACTIVE" },
                { 28L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ACCOUNT_DETAILS_MANAGE", "SCHOOL_ADMIN", "ACTIVE" }
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 27L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 28L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "ACCOUNT_DETAILS_MANAGE");
    }
}
