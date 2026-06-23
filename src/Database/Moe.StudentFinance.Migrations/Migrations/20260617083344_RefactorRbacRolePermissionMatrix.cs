using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Moe.StudentFinance.Migrations.Migrations;

/// <inheritdoc />
public partial class RefactorRbacRolePermissionMatrix : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "ACCOUNTS_MANAGE");

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "ACCOUNTS_VIEW");

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "COURSES_MANAGE");

        migrationBuilder.InsertData(
            schema: "iam",
            table: "Permission",
            columns: new[] { "PermissionCode", "ActionCode", "ModuleCode", "PermissionName", "ResourceCode", "StatusCode" },
            values: new object[,]
            {
                { "ACCOUNT_LIFECYCLE_MANAGE", "MANAGE", "EDUCATION_ACCOUNT_TOPUP", "Suspend, reactivate and close education accounts", "ACCOUNT_LIFECYCLE", "ACTIVE" },
                { "ACCOUNT_MANUAL_CREATE", "CREATE", "EDUCATION_ACCOUNT_TOPUP", "Manually create education accounts", "ACCOUNTS", "ACTIVE" },
                { "ACCOUNT_SETTLEMENT_VIEW", "VIEW", "EDUCATION_ACCOUNT_TOPUP", "View settlement operations", "SETTLEMENTS", "ACTIVE" },
                { "ACCOUNT_VIEW_ALL", "VIEW", "EDUCATION_ACCOUNT_TOPUP", "View all education accounts", "ACCOUNTS_ALL", "ACTIVE" },
                { "ACCOUNT_VIEW_SCHOOL", "VIEW", "EDUCATION_ACCOUNT_TOPUP", "View own-school education account summaries", "ACCOUNTS_SCHOOL", "ACTIVE" },
                { "AUDIT_VIEW_ALL", "VIEW", "IDENTITY_PLATFORM", "View national audit", "AUDIT_ALL", "ACTIVE" },
                { "AUDIT_VIEW_SCHOOL", "VIEW", "IDENTITY_PLATFORM", "View own-school audit", "AUDIT_SCHOOL", "ACTIVE" },
                { "COURSE_ASSIGN_STUDENT", "ASSIGN", "COURSE_BILLING", "Assign own-school students to courses", "COURSE_STUDENTS", "ACTIVE" },
                { "COURSE_DISABLE_ANY", "DISABLE", "COURSE_BILLING", "Disable any course with reason", "COURSES_ANY", "ACTIVE" },
                { "COURSE_FEE_MANAGE_OWN_SCHOOL", "MANAGE", "COURSE_BILLING", "Manage own-school course fees", "COURSE_FEES_OWN_SCHOOL", "ACTIVE" },
                { "COURSE_MANAGE_ANY", "MANAGE", "COURSE_BILLING", "Manage courses across any school", "COURSES_ANY", "ACTIVE" },
                { "COURSE_MANAGE_OWN_SCHOOL", "MANAGE", "COURSE_BILLING", "Manage own-school courses", "COURSES_OWN_SCHOOL", "ACTIVE" },
                { "COURSE_VIEW_ALL", "VIEW", "COURSE_BILLING", "View all courses", "COURSES_ALL", "ACTIVE" },
                { "FAS_SCHEME_MANAGE", "MANAGE", "FAS_PAYMENT", "Manage national FAS schemes", "FAS_SCHEMES", "ACTIVE" },
                { "LOGIN_DISABLE", "DISABLE", "IDENTITY_PLATFORM", "Disable permitted login accounts", "LOGIN_ACCOUNTS", "ACTIVE" },
                { "ORG_VIEW_ALL", "VIEW", "IDENTITY_PLATFORM", "View all organizations", "ORGANIZATIONS", "ACTIVE" },
                { "REPORT_EXPORT_ALL", "EXPORT", "IDENTITY_PLATFORM", "Export national reports", "REPORTS_ALL", "ACTIVE" },
                { "REPORT_EXPORT_SCHOOL", "EXPORT", "IDENTITY_PLATFORM", "Export own-school reports", "REPORTS_SCHOOL", "ACTIVE" },
                { "SCHOOL_ADMIN_PROVISION", "PROVISION", "IDENTITY_PLATFORM", "Provision school admin accounts", "SCHOOL_ADMINS", "ACTIVE" },
                { "SCHOOL_STUDENT_VIEW", "VIEW", "IDENTITY_PLATFORM", "View assigned school students", "SCHOOL_STUDENTS", "ACTIVE" },
                { "STUDENT_ACCOUNT_VIEW_SELF", "VIEW", "EDUCATION_ACCOUNT_TOPUP", "View own education account", "ACCOUNT_SELF", "ACTIVE" },
                { "TOPUP_VIEW_ALL", "VIEW", "EDUCATION_ACCOUNT_TOPUP", "View national top-up activity", "TOPUPS_ALL", "ACTIVE" }
            });

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 1L,
            column: "PermissionCode",
            value: "ORG_VIEW_ALL");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 2L,
            column: "PermissionCode",
            value: "SCHOOL_STUDENT_VIEW");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 3L,
            column: "PermissionCode",
            value: "SCHOOL_ADMIN_PROVISION");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 4L,
            column: "PermissionCode",
            value: "LOGIN_DISABLE");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 5L,
            column: "PermissionCode",
            value: "EXTERNAL_ACCOUNTS_PROVISION");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 6L,
            column: "PermissionCode",
            value: "ACCESS_SCOPE_MANAGE");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 7L,
            column: "PermissionCode",
            value: "ACCOUNT_VIEW_ALL");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 8L,
            column: "PermissionCode",
            value: "ACCOUNT_MANUAL_CREATE");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 9L,
            columns: new[] { "PermissionCode", "RoleCode" },
            values: new object[] { "ACCOUNT_LIFECYCLE_MANAGE", "SYSTEM_ADMIN" });

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 10L,
            columns: new[] { "PermissionCode", "RoleCode" },
            values: new object[] { "ACCOUNT_SETTLEMENT_VIEW", "SYSTEM_ADMIN" });

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 11L,
            columns: new[] { "PermissionCode", "RoleCode" },
            values: new object[] { "TOPUP_VIEW_ALL", "SYSTEM_ADMIN" });

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 12L,
            columns: new[] { "PermissionCode", "RoleCode" },
            values: new object[] { "COURSE_VIEW_ALL", "SYSTEM_ADMIN" });

        migrationBuilder.InsertData(
            schema: "iam",
            table: "RolePermission",
            columns: new[] { "RolePermissionId", "EffectiveFromUtc", "EffectiveToUtc", "PermissionCode", "RoleCode", "StatusCode" },
            values: new object[,]
            {
                { 13L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "COURSE_DISABLE_ANY", "SYSTEM_ADMIN", "ACTIVE" },
                { 14L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "FAS_SCHEME_MANAGE", "SYSTEM_ADMIN", "ACTIVE" },
                { 15L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "FAS_REVIEW", "SYSTEM_ADMIN", "ACTIVE" },
                { 16L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PAYMENT_EXCEPTIONS_REVIEW", "SYSTEM_ADMIN", "ACTIVE" },
                { 17L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "AUDIT_VIEW_ALL", "SYSTEM_ADMIN", "ACTIVE" },
                { 18L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "REPORT_EXPORT_ALL", "SYSTEM_ADMIN", "ACTIVE" },
                { 19L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "SCHOOL_STUDENT_VIEW", "SCHOOL_ADMIN", "ACTIVE" },
                { 20L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ACCOUNT_VIEW_SCHOOL", "SCHOOL_ADMIN", "ACTIVE" },
                { 21L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "COURSE_MANAGE_OWN_SCHOOL", "SCHOOL_ADMIN", "ACTIVE" },
                { 22L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "COURSE_FEE_MANAGE_OWN_SCHOOL", "SCHOOL_ADMIN", "ACTIVE" },
                { 23L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "COURSE_ASSIGN_STUDENT", "SCHOOL_ADMIN", "ACTIVE" },
                { 24L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "AUDIT_VIEW_SCHOOL", "SCHOOL_ADMIN", "ACTIVE" },
                { 25L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "REPORT_EXPORT_SCHOOL", "SCHOOL_ADMIN", "ACTIVE" },
                { 26L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "STUDENT_ACCOUNT_VIEW_SELF", "STUDENT", "ACTIVE" }
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "ACCOUNT_LIFECYCLE_MANAGE");

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "ACCOUNT_MANUAL_CREATE");

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "ACCOUNT_SETTLEMENT_VIEW");

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "ACCOUNT_VIEW_ALL");

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "ACCOUNT_VIEW_SCHOOL");

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "AUDIT_VIEW_ALL");

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "AUDIT_VIEW_SCHOOL");

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "COURSE_ASSIGN_STUDENT");

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "COURSE_DISABLE_ANY");

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "COURSE_FEE_MANAGE_OWN_SCHOOL");

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "COURSE_MANAGE_ANY");

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "COURSE_MANAGE_OWN_SCHOOL");

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "COURSE_VIEW_ALL");

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "FAS_SCHEME_MANAGE");

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "LOGIN_DISABLE");

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "ORG_VIEW_ALL");

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "REPORT_EXPORT_ALL");

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "REPORT_EXPORT_SCHOOL");

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "SCHOOL_ADMIN_PROVISION");

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "SCHOOL_STUDENT_VIEW");

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "STUDENT_ACCOUNT_VIEW_SELF");

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "TOPUP_VIEW_ALL");

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 13L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 14L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 15L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 16L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 17L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 18L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 19L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 20L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 21L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 22L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 23L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 24L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 25L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 26L);

        migrationBuilder.InsertData(
            schema: "iam",
            table: "Permission",
            columns: new[] { "PermissionCode", "ActionCode", "ModuleCode", "PermissionName", "ResourceCode", "StatusCode" },
            values: new object[,]
            {
                { "ACCOUNTS_MANAGE", "MANAGE", "EDUCATION_ACCOUNT_TOPUP", "Manage accounts", "ACCOUNTS", "ACTIVE" },
                { "ACCOUNTS_VIEW", "VIEW", "EDUCATION_ACCOUNT_TOPUP", "View accounts", "ACCOUNTS", "ACTIVE" },
                { "COURSES_MANAGE", "MANAGE", "COURSE_BILLING", "Manage courses", "COURSES", "ACTIVE" }
            });

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 1L,
            column: "PermissionCode",
            value: "ACCESS_SCOPE_MANAGE");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 2L,
            column: "PermissionCode",
            value: "EXTERNAL_ACCOUNTS_PROVISION");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 3L,
            column: "PermissionCode",
            value: "ACCOUNTS_VIEW");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 4L,
            column: "PermissionCode",
            value: "ACCOUNTS_MANAGE");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 5L,
            column: "PermissionCode",
            value: "TOPUPS_MANAGE");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 6L,
            column: "PermissionCode",
            value: "COURSES_MANAGE");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 7L,
            column: "PermissionCode",
            value: "FAS_REVIEW");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 8L,
            column: "PermissionCode",
            value: "PAYMENT_EXCEPTIONS_REVIEW");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 9L,
            columns: new[] { "PermissionCode", "RoleCode" },
            values: new object[] { "ACCOUNTS_VIEW", "SCHOOL_ADMIN" });

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 10L,
            columns: new[] { "PermissionCode", "RoleCode" },
            values: new object[] { "COURSES_MANAGE", "SCHOOL_ADMIN" });

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 11L,
            columns: new[] { "PermissionCode", "RoleCode" },
            values: new object[] { "FAS_REVIEW", "SCHOOL_ADMIN" });

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "RolePermission",
            keyColumn: "RolePermissionId",
            keyValue: 12L,
            columns: new[] { "PermissionCode", "RoleCode" },
            values: new object[] { "ACCOUNTS_VIEW", "STUDENT" });
    }
}
