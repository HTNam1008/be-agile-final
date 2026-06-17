using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Moe.StudentFinance.Migrations.Migrations;

/// <inheritdoc />
public partial class SeedDemoRolesAdminsAndStudent : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.InsertData(
            schema: "account",
            table: "EducationAccount",
            columns: new[] { "EducationAccountId", "AccountNumber", "CurrentBalance", "ClosedAt", "ClosedByLoginAccountId", "ClosingReason", "ClosingTypeCode", "ClosureExceptionApprovedByLoginAccountId", "ClosureExceptionReason", "ClosureExceptionUntil", "OpenedAt", "OpenedByLoginAccountId", "OpeningTypeCode", "OpeningReason", "PendingClosureAt", "PersonId", "AccountStatusCode" },
            values: new object[] { 4001L, "EA-DEMO-0001", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for MockPass student", null, 2001L, "ACTIVE" });

        migrationBuilder.InsertData(
            schema: "iam",
            table: "LoginAccount",
            columns: new[] { "LoginAccountId", "LoginStatusCode", "AdminOrganizationId", "ContactEmail", "ContactMobile", "CreatedAt", "CreatedByLoginAccountId", "DisplayNameSnapshot", "ExternalIssuer", "ExternalObjectId", "ExternalSubjectId", "ExternalTenantId", "FirstLoginAtUtc", "IdentityProviderCode", "LastLoginAt", "LastSyncedAt", "LoginEmailNormalized", "PersonId", "PortalAccessCode", "ProviderDisplayName", "ProviderEmail", "ProviderLoginName", "ProviderMobile", "RoleCode", "UpdatedAt", "UserTypeCode" },
            values: new object[,]
            {
                { 1001L, "ACTIVE", 1L, "system.admin@moe.local", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "MOE System Admin", "https://sts.windows.net/ea71ddeb-596c-4034-84d4-d65f91edc14a/", "731f2a50-4fa7-4530-9294-1a5b912daf31", "731f2a50-4fa7-4530-9294-1a5b912daf31", "ea71ddeb-596c-4034-84d4-d65f91edc14a", null, "ENTRA_WORKFORCE", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM.ADMIN@MOE.LOCAL", null, "ADMIN", "MOE System Admin", "system.admin@moe.local", "system.admin@moe.local", null, "SYSTEM_ADMIN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "INTERNAL" },
                { 1002L, "ACTIVE", 2L, "school.admin@demo-school.local", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Demo School Admin", "https://sts.windows.net/ea71ddeb-596c-4034-84d4-d65f91edc14a/", "00000000-0000-0000-0000-000000000222", "00000000-0000-0000-0000-000000000222", "ea71ddeb-596c-4034-84d4-d65f91edc14a", null, "ENTRA_WORKFORCE", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SCHOOL.ADMIN@DEMO-SCHOOL.LOCAL", null, "ADMIN", "Demo School Admin", "school.admin@demo-school.local", "school.admin@demo-school.local", null, "SCHOOL_ADMIN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "INTERNAL" },
                { 1003L, "ACTIVE", null, "student@example.test", "+6590000001", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Tan Mei Ling", "http://localhost:5156/singpass/v3/fapi", null, "ef39a074-b64d-4990-a937-6f80772e2bb8", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2001L, "ESERVICE", "Tan Mei Ling", null, "S1234567A", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" }
            });

        migrationBuilder.InsertData(
            schema: "org",
            table: "Organization",
            columns: new[] { "OrganizationId", "CreatedAt", "EffectiveFromUtc", "EffectiveToUtc", "MockPassSchoolCode", "ParentOrganizationId", "OrganizationStatusCode", "OrganizationCode", "OrganizationName", "OrganizationTypeCode", "UpdatedAt" },
            values: new object[] { 2L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "MOEDEMO", 1L, "ACTIVE", "DEMO_SCHOOL", "Demo Secondary School", "SCHOOL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "ACCESS_SCOPE_MANAGE",
            column: "ModuleCode",
            value: "IDENTITY_PLATFORM");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "ACCOUNTS_MANAGE",
            column: "ModuleCode",
            value: "EDUCATION_ACCOUNT_TOPUP");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "ACCOUNTS_VIEW",
            column: "ModuleCode",
            value: "EDUCATION_ACCOUNT_TOPUP");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "COURSES_MANAGE",
            column: "ModuleCode",
            value: "COURSE_BILLING");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "EXTERNAL_ACCOUNTS_PROVISION",
            column: "ModuleCode",
            value: "IDENTITY_PLATFORM");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "FAS_REVIEW",
            column: "ModuleCode",
            value: "FAS_PAYMENT");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "PAYMENT_EXCEPTIONS_REVIEW",
            column: "ModuleCode",
            value: "FAS_PAYMENT");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "TOPUPS_MANAGE",
            column: "ModuleCode",
            value: "EDUCATION_ACCOUNT_TOPUP");

        migrationBuilder.InsertData(
            schema: "person",
            table: "Person",
            columns: new[] { "PersonId", "ResidencyStatusCode", "CreatedAt", "DateOfBirth", "MockPassPersonId", "IdentityNumberMasked", "NationalityCode", "OfficialAddress", "OfficialEmail", "FullName", "OfficialMobile", "PersonStatusCode", "PreferredAddress", "PreferredEmail", "PreferredMobile", "SourceUpdatedAt", "UpdatedAt" },
            values: new object[] { 2001L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2008, 5, 12), "ef39a074-b64d-4990-a937-6f80772e2bb8", "S1234567A", "SG", "1 Demo Street, Singapore 000001", "student.official@example.test", "Tan Mei Ling", "+6590000001", "ACTIVE", "1 Demo Street, Singapore 000001", "student@example.test", "+6590000001", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

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

        migrationBuilder.InsertData(
            schema: "iam",
            table: "RolePermission",
            columns: new[] { "RolePermissionId", "EffectiveFromUtc", "EffectiveToUtc", "PermissionCode", "RoleCode", "StatusCode" },
            values: new object[,]
            {
                { 9L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ACCOUNTS_VIEW", "SCHOOL_ADMIN", "ACTIVE" },
                { 10L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "COURSES_MANAGE", "SCHOOL_ADMIN", "ACTIVE" },
                { 11L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "FAS_REVIEW", "SCHOOL_ADMIN", "ACTIVE" },
                { 12L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ACCOUNTS_VIEW", "STUDENT", "ACTIVE" }
            });

        migrationBuilder.InsertData(
            schema: "person",
            table: "SchoolEnrollment",
            columns: new[] { "SchoolEnrollmentId", "AcademicYear", "ClassCode", "CreatedAt", "EndDate", "LevelCode", "OrganizationId", "PersonId", "SchoolingStatusCode", "SourceCode", "StartDate", "StatusReasonCode", "StudentNumber", "UpdatedAt" },
            values: new object[] { 3001L, "2026", "4A", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "SEC_4", 2L, 2001L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "DEMO-STU-0001", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

        migrationBuilder.InsertData(
            schema: "iam",
            table: "UserAccessScope",
            columns: new[] { "UserAccessScopeId", "CreatedAtUtc", "CreatedByUserAccountId", "EffectiveFromUtc", "EffectiveToUtc", "OrganizationUnitId", "RoleCode", "StatusCode", "UserAccountId" },
            values: new object[,]
            {
                { 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 1L, "SYSTEM_ADMIN", "ACTIVE", 1001L },
                { 1002L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2L, "SCHOOL_ADMIN", "ACTIVE", 1002L },
                { 1003L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2L, "STUDENT", "ACTIVE", 1003L }
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            schema: "account",
            table: "EducationAccount",
            keyColumn: "EducationAccountId",
            keyValue: 4001L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "LoginAccount",
            keyColumn: "LoginAccountId",
            keyValue: 1001L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "LoginAccount",
            keyColumn: "LoginAccountId",
            keyValue: 1002L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "LoginAccount",
            keyColumn: "LoginAccountId",
            keyValue: 1003L);

        migrationBuilder.DeleteData(
            schema: "org",
            table: "Organization",
            keyColumn: "OrganizationId",
            keyValue: 2L);

        migrationBuilder.DeleteData(
            schema: "person",
            table: "Person",
            keyColumn: "PersonId",
            keyValue: 2001L);

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
            schema: "person",
            table: "SchoolEnrollment",
            keyColumn: "SchoolEnrollmentId",
            keyValue: 3001L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "UserAccessScope",
            keyColumn: "UserAccessScopeId",
            keyValue: 1001L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "UserAccessScope",
            keyColumn: "UserAccessScopeId",
            keyValue: 1002L);

        migrationBuilder.DeleteData(
            schema: "iam",
            table: "UserAccessScope",
            keyColumn: "UserAccessScopeId",
            keyValue: 1003L);

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "ACCESS_SCOPE_MANAGE",
            column: "ModuleCode",
            value: "IDENTITY_STUDENT");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "ACCOUNTS_MANAGE",
            column: "ModuleCode",
            value: "ACCOUNT_FUNDING");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "ACCOUNTS_VIEW",
            column: "ModuleCode",
            value: "ACCOUNT_FUNDING");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "COURSES_MANAGE",
            column: "ModuleCode",
            value: "ACADEMIC_FINANCE");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "EXTERNAL_ACCOUNTS_PROVISION",
            column: "ModuleCode",
            value: "IDENTITY_STUDENT");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "FAS_REVIEW",
            column: "ModuleCode",
            value: "ACADEMIC_FINANCE");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "PAYMENT_EXCEPTIONS_REVIEW",
            column: "ModuleCode",
            value: "PAYMENTS_DIGITAL");

        migrationBuilder.UpdateData(
            schema: "iam",
            table: "Permission",
            keyColumn: "PermissionCode",
            keyValue: "TOPUPS_MANAGE",
            column: "ModuleCode",
            value: "ACCOUNT_FUNDING");

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
}
