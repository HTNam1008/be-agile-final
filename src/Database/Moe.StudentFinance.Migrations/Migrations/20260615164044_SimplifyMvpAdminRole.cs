using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyMvpAdminRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [iam].[UserAccessScope]
                SET [RoleCode] = 'ADMIN'
                WHERE [RoleCode] IN ('SUPER_ADMIN', 'IDENTITY_ADMIN', 'FINANCE_ADMIN')
                """);

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
                table: "RolePermission",
                keyColumn: "RolePermissionId",
                keyValue: 1L,
                column: "RoleCode",
                value: "ADMIN");

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "RolePermission",
                keyColumn: "RolePermissionId",
                keyValue: 2L,
                column: "RoleCode",
                value: "ADMIN");

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "RolePermission",
                keyColumn: "RolePermissionId",
                keyValue: 3L,
                column: "RoleCode",
                value: "ADMIN");

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "RolePermission",
                keyColumn: "RolePermissionId",
                keyValue: 4L,
                column: "RoleCode",
                value: "ADMIN");

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "RolePermission",
                keyColumn: "RolePermissionId",
                keyValue: 5L,
                column: "RoleCode",
                value: "ADMIN");

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "RolePermission",
                keyColumn: "RolePermissionId",
                keyValue: 6L,
                column: "RoleCode",
                value: "ADMIN");

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "RolePermission",
                keyColumn: "RolePermissionId",
                keyValue: 7L,
                column: "RoleCode",
                value: "ADMIN");

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "RolePermission",
                keyColumn: "RolePermissionId",
                keyValue: 8L,
                column: "RoleCode",
                value: "ADMIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [iam].[UserAccessScope]
                SET [RoleCode] = 'SUPER_ADMIN'
                WHERE [RoleCode] = 'ADMIN'
                """);

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "RolePermission",
                keyColumn: "RolePermissionId",
                keyValue: 1L,
                column: "RoleCode",
                value: "SUPER_ADMIN");

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "RolePermission",
                keyColumn: "RolePermissionId",
                keyValue: 2L,
                column: "RoleCode",
                value: "SUPER_ADMIN");

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "RolePermission",
                keyColumn: "RolePermissionId",
                keyValue: 3L,
                column: "RoleCode",
                value: "SUPER_ADMIN");

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "RolePermission",
                keyColumn: "RolePermissionId",
                keyValue: 4L,
                column: "RoleCode",
                value: "SUPER_ADMIN");

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "RolePermission",
                keyColumn: "RolePermissionId",
                keyValue: 5L,
                column: "RoleCode",
                value: "SUPER_ADMIN");

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "RolePermission",
                keyColumn: "RolePermissionId",
                keyValue: 6L,
                column: "RoleCode",
                value: "SUPER_ADMIN");

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "RolePermission",
                keyColumn: "RolePermissionId",
                keyValue: 7L,
                column: "RoleCode",
                value: "SUPER_ADMIN");

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "RolePermission",
                keyColumn: "RolePermissionId",
                keyValue: 8L,
                column: "RoleCode",
                value: "SUPER_ADMIN");

            migrationBuilder.InsertData(
                schema: "iam",
                table: "RolePermission",
                columns: new[] { "RolePermissionId", "EffectiveFromUtc", "EffectiveToUtc", "PermissionCode", "RoleCode", "StatusCode" },
                values: new object[,]
                {
                    { 9L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ACCESS_SCOPE_MANAGE", "IDENTITY_ADMIN", "ACTIVE" },
                    { 10L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "EXTERNAL_ACCOUNTS_PROVISION", "IDENTITY_ADMIN", "ACTIVE" },
                    { 11L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ACCOUNTS_VIEW", "FINANCE_ADMIN", "ACTIVE" },
                    { 12L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ACCOUNTS_MANAGE", "FINANCE_ADMIN", "ACTIVE" },
                    { 13L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "TOPUPS_MANAGE", "FINANCE_ADMIN", "ACTIVE" }
                });
        }
    }
}
