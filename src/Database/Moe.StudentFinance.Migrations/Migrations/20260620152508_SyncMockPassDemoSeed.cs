using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class SyncMockPassDemoSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1002L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1002L);

            migrationBuilder.UpdateData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4001L,
                column: "AccountNumber",
                value: "EA-NUS-001");

            migrationBuilder.UpdateData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4002L,
                column: "AccountNumber",
                value: "EA-NUS-002");

            migrationBuilder.UpdateData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4003L,
                column: "AccountNumber",
                value: "EA-NUS-003");

            migrationBuilder.UpdateData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4004L,
                column: "AccountNumber",
                value: "EA-NUS-004");

            migrationBuilder.InsertData(
                schema: "account",
                table: "EducationAccount",
                columns: new[] { "EducationAccountId", "AccountNumber", "CurrentBalance", "ClosedAt", "ClosedByLoginAccountId", "ClosingReason", "ClosingTypeCode", "ClosureExceptionApprovedByLoginAccountId", "ClosureExceptionReason", "ClosureExceptionUntil", "OpenedAt", "OpenedByLoginAccountId", "OpeningTypeCode", "OpeningReason", "PendingClosureAt", "PersonId", "AccountStatusCode" },
                values: new object[,]
                {
                    { 4005L, "EA-NUS-005", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2005L, "ACTIVE" },
                    { 4006L, "EA-NUS-006", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2006L, "ACTIVE" },
                    { 4007L, "EA-NUS-007", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2007L, "ACTIVE" },
                    { 4008L, "EA-NUS-008", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2008L, "ACTIVE" },
                    { 4009L, "EA-NUS-009", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2009L, "ACTIVE" },
                    { 4010L, "EA-NUS-010", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2010L, "ACTIVE" },
                    { 4011L, "EA-NTU-001", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2011L, "ACTIVE" },
                    { 4012L, "EA-NTU-002", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2012L, "ACTIVE" },
                    { 4013L, "EA-NTU-003", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2013L, "ACTIVE" },
                    { 4014L, "EA-NTU-004", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2014L, "ACTIVE" },
                    { 4015L, "EA-NTU-005", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2015L, "ACTIVE" },
                    { 4016L, "EA-NTU-006", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2016L, "ACTIVE" },
                    { 4017L, "EA-NTU-007", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2017L, "ACTIVE" },
                    { 4018L, "EA-NTU-008", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2018L, "ACTIVE" },
                    { 4019L, "EA-NTU-009", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2019L, "ACTIVE" },
                    { 4020L, "EA-NTU-010", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2020L, "ACTIVE" },
                    { 4021L, "EA-SMU-001", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2021L, "ACTIVE" },
                    { 4022L, "EA-SMU-002", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2022L, "ACTIVE" },
                    { 4023L, "EA-SMU-003", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2023L, "ACTIVE" },
                    { 4024L, "EA-SMU-004", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2024L, "ACTIVE" },
                    { 4025L, "EA-SMU-005", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2025L, "ACTIVE" },
                    { 4026L, "EA-SMU-006", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2026L, "ACTIVE" },
                    { 4027L, "EA-SMU-007", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2027L, "ACTIVE" },
                    { 4028L, "EA-SMU-008", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2028L, "ACTIVE" },
                    { 4029L, "EA-SMU-009", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2029L, "ACTIVE" },
                    { 4030L, "EA-SMU-010", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2030L, "ACTIVE" },
                    { 4031L, "EA-SUTD-001", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2031L, "ACTIVE" },
                    { 4032L, "EA-SUTD-002", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2032L, "ACTIVE" },
                    { 4033L, "EA-SUTD-003", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2033L, "ACTIVE" },
                    { 4034L, "EA-SUTD-004", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2034L, "ACTIVE" },
                    { 4035L, "EA-SUTD-005", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2035L, "ACTIVE" },
                    { 4036L, "EA-SUTD-006", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2036L, "ACTIVE" },
                    { 4037L, "EA-SUTD-007", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2037L, "ACTIVE" },
                    { 4038L, "EA-SUTD-008", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2038L, "ACTIVE" },
                    { 4039L, "EA-SUTD-009", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2039L, "ACTIVE" },
                    { 4040L, "EA-SUTD-010", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2040L, "ACTIVE" },
                    { 4041L, "EA-SIT-001", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2041L, "ACTIVE" },
                    { 4042L, "EA-SIT-002", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2042L, "ACTIVE" },
                    { 4043L, "EA-SIT-003", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2043L, "ACTIVE" },
                    { 4044L, "EA-SIT-004", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2044L, "ACTIVE" },
                    { 4045L, "EA-SIT-005", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2045L, "ACTIVE" },
                    { 4046L, "EA-SIT-006", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2046L, "ACTIVE" },
                    { 4047L, "EA-SIT-007", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2047L, "ACTIVE" },
                    { 4048L, "EA-SIT-008", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2048L, "ACTIVE" },
                    { 4049L, "EA-SIT-009", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2049L, "ACTIVE" },
                    { 4050L, "EA-SIT-010", 0m, null, null, null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1001L, "MANUAL", "Demo seeded account for top-up search", null, 2050L, "ACTIVE" }
                });

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1003L,
                columns: new[] { "ContactEmail", "ContactMobile", "DisplayNameSnapshot", "ExternalSubjectId", "ProviderDisplayName", "ProviderLoginName" },
                values: new object[] { "aarav.tan.nus001@student.example.test", "+6591000001", "Aarav Tan", "d4329977-8fa2-4f2c-9cca-2ec65b245757", "Aarav Tan", "S7000001A" });

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1004L,
                columns: new[] { "ContactEmail", "ContactMobile", "ExternalSubjectId", "ProviderLoginName" },
                values: new object[] { "aisha.tan.nus002@student.example.test", "+6591000002", "504615ea-1b59-4a2b-adae-96f00c2590cd", "S7000002B" });

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1005L,
                columns: new[] { "ContactEmail", "ContactMobile", "DisplayNameSnapshot", "ExternalSubjectId", "ProviderDisplayName", "ProviderLoginName" },
                values: new object[] { "benjamin.tan.nus003@student.example.test", "+6591000003", "Benjamin Tan", "7ec809bd-c7e8-4ddd-994e-e2e9f474adb2", "Benjamin Tan", "S7000003C" });

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1006L,
                columns: new[] { "ContactEmail", "ContactMobile", "DisplayNameSnapshot", "ExternalSubjectId", "ProviderDisplayName", "ProviderLoginName" },
                values: new object[] { "chloe.tan.nus004@student.example.test", "+6591000004", "Chloe Tan", "43b2b617-9e83-4129-a8b9-358446731674", "Chloe Tan", "S7000004D" });

            migrationBuilder.InsertData(
                schema: "iam",
                table: "LoginAccount",
                columns: new[] { "LoginAccountId", "LoginStatusCode", "AdminOrganizationId", "ContactEmail", "ContactMobile", "CreatedAt", "CreatedByLoginAccountId", "DisplayNameSnapshot", "ExternalIssuer", "ExternalObjectId", "ExternalSubjectId", "ExternalTenantId", "FirstLoginAtUtc", "IdentityProviderCode", "LastLoginAt", "LastSyncedAt", "LoginEmailNormalized", "PersonId", "PortalAccessCode", "ProviderDisplayName", "ProviderEmail", "ProviderLoginName", "ProviderMobile", "RoleCode", "UpdatedAt", "UserTypeCode" },
                values: new object[,]
                {
                    { 1007L, "ACTIVE", null, "daniel.tan.nus005@student.example.test", "+6591000005", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Daniel Tan", "http://localhost:5156/singpass/v3/fapi", null, "deafdd4e-69f4-451f-9c90-5e8deaff58dd", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2005L, "ESERVICE", "Daniel Tan", null, "S7000005E", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1008L, "ACTIVE", null, "emma.tan.nus006@student.example.test", "+6591000006", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Emma Tan", "http://localhost:5156/singpass/v3/fapi", null, "eabeb7c7-7254-4f02-8b1c-9b5283087c4b", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2006L, "ESERVICE", "Emma Tan", null, "S7000006F", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1009L, "ACTIVE", null, "farhan.tan.nus007@student.example.test", "+6591000007", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Farhan Tan", "http://localhost:5156/singpass/v3/fapi", null, "08baf5dd-07c7-4030-8163-be270f97346e", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2007L, "ESERVICE", "Farhan Tan", null, "S7000007G", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1010L, "ACTIVE", null, "grace.tan.nus008@student.example.test", "+6591000008", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Grace Tan", "http://localhost:5156/singpass/v3/fapi", null, "694fe5d5-21bb-4614-b1bb-7a85d1323cb6", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2008L, "ESERVICE", "Grace Tan", null, "S7000008H", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1011L, "ACTIVE", null, "hannah.tan.nus009@student.example.test", "+6591000009", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Hannah Tan", "http://localhost:5156/singpass/v3/fapi", null, "43a59bc6-c7e2-4402-82e8-155c50e3e3b1", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2009L, "ESERVICE", "Hannah Tan", null, "S7000009J", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1012L, "ACTIVE", null, "isaac.tan.nus010@student.example.test", "+6591000010", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Isaac Tan", "http://localhost:5156/singpass/v3/fapi", null, "fd4168bc-3a4b-4339-8e1b-4dfef896d0ec", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2010L, "ESERVICE", "Isaac Tan", null, "S7000010K", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1013L, "ACTIVE", null, "jia.min.tan.ntu001@student.example.test", "+6591000011", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Jia Min Tan", "http://localhost:5156/singpass/v3/fapi", null, "3ed57123-302e-4af5-9745-8b9a11cf84d2", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2011L, "ESERVICE", "Jia Min Tan", null, "S7000011L", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1014L, "ACTIVE", null, "kai.tan.ntu002@student.example.test", "+6591000012", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Kai Tan", "http://localhost:5156/singpass/v3/fapi", null, "3fbf4c48-1c06-47a1-a392-14e1ce88f2ef", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2012L, "ESERVICE", "Kai Tan", null, "S7000012M", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1015L, "ACTIVE", null, "liyana.tan.ntu003@student.example.test", "+6591000013", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Liyana Tan", "http://localhost:5156/singpass/v3/fapi", null, "737a1787-711f-4d4d-95cc-20fa351fb45f", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2013L, "ESERVICE", "Liyana Tan", null, "S7000013N", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1016L, "ACTIVE", null, "marcus.tan.ntu004@student.example.test", "+6591000014", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Marcus Tan", "http://localhost:5156/singpass/v3/fapi", null, "e63b844e-7c1e-4478-a8b8-b51bf35b0f5b", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2014L, "ESERVICE", "Marcus Tan", null, "S7000014P", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1017L, "ACTIVE", null, "nadia.tan.ntu005@student.example.test", "+6591000015", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Nadia Tan", "http://localhost:5156/singpass/v3/fapi", null, "ce28804a-407e-44e8-a228-a6d895744b85", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2015L, "ESERVICE", "Nadia Tan", null, "S7000015Q", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1018L, "ACTIVE", null, "oliver.tan.ntu006@student.example.test", "+6591000016", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Oliver Tan", "http://localhost:5156/singpass/v3/fapi", null, "fd308af1-279f-44ea-b859-6bd4c395e064", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2016L, "ESERVICE", "Oliver Tan", null, "S7000016R", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1019L, "ACTIVE", null, "priya.tan.ntu007@student.example.test", "+6591000017", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Priya Tan", "http://localhost:5156/singpass/v3/fapi", null, "a68d6c07-8452-4d21-9ebc-4b08cac341c9", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2017L, "ESERVICE", "Priya Tan", null, "S7000017T", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1020L, "ACTIVE", null, "ryan.tan.ntu008@student.example.test", "+6591000018", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Ryan Tan", "http://localhost:5156/singpass/v3/fapi", null, "ef92ca13-3e94-4997-9702-90bb42675404", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2018L, "ESERVICE", "Ryan Tan", null, "S7000018U", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1021L, "ACTIVE", null, "siti.tan.ntu009@student.example.test", "+6591000019", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Siti Tan", "http://localhost:5156/singpass/v3/fapi", null, "4d7e0a14-bc39-47aa-8487-4808f5b605ef", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2019L, "ESERVICE", "Siti Tan", null, "S7000019V", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1022L, "ACTIVE", null, "wei.jie.tan.ntu010@student.example.test", "+6591000020", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Wei Jie Tan", "http://localhost:5156/singpass/v3/fapi", null, "d7091bae-c4d7-41ba-ac9a-8e40201eb4da", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2020L, "ESERVICE", "Wei Jie Tan", null, "S7000020W", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1023L, "ACTIVE", null, "aarav.lim.smu001@student.example.test", "+6591000021", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Aarav Lim", "http://localhost:5156/singpass/v3/fapi", null, "015bc9cc-d1cb-4c98-8bf3-9d8a6f9524bc", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2021L, "ESERVICE", "Aarav Lim", null, "S7000021X", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1024L, "ACTIVE", null, "aisha.lim.smu002@student.example.test", "+6591000022", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Aisha Lim", "http://localhost:5156/singpass/v3/fapi", null, "88c38bbc-5202-405a-b155-511d191075dd", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2022L, "ESERVICE", "Aisha Lim", null, "S7000022Y", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1025L, "ACTIVE", null, "benjamin.lim.smu003@student.example.test", "+6591000023", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Benjamin Lim", "http://localhost:5156/singpass/v3/fapi", null, "9ad36576-33cf-45eb-a930-97751426c8ff", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2023L, "ESERVICE", "Benjamin Lim", null, "S7000023Z", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1026L, "ACTIVE", null, "chloe.lim.smu004@student.example.test", "+6591000024", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Chloe Lim", "http://localhost:5156/singpass/v3/fapi", null, "76a09bec-5dc9-4c4e-ad07-3ebf242a4e00", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2024L, "ESERVICE", "Chloe Lim", null, "S7000024A", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1027L, "ACTIVE", null, "daniel.lim.smu005@student.example.test", "+6591000025", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Daniel Lim", "http://localhost:5156/singpass/v3/fapi", null, "45d455ee-5b04-485e-9195-7c3a788d2375", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2025L, "ESERVICE", "Daniel Lim", null, "S7000025B", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1028L, "ACTIVE", null, "emma.lim.smu006@student.example.test", "+6591000026", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Emma Lim", "http://localhost:5156/singpass/v3/fapi", null, "9313e8fc-dcfc-4f73-b14c-eef53edce1f2", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2026L, "ESERVICE", "Emma Lim", null, "S7000026C", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1029L, "ACTIVE", null, "farhan.lim.smu007@student.example.test", "+6591000027", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Farhan Lim", "http://localhost:5156/singpass/v3/fapi", null, "464ebf11-4cfd-4165-a376-7fb6dfb21b69", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2027L, "ESERVICE", "Farhan Lim", null, "S7000027D", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1030L, "ACTIVE", null, "grace.lim.smu008@student.example.test", "+6591000028", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Grace Lim", "http://localhost:5156/singpass/v3/fapi", null, "5f3740d4-6ba5-4a05-b7c8-197636bb0de5", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2028L, "ESERVICE", "Grace Lim", null, "S7000028E", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1031L, "ACTIVE", null, "hannah.lim.smu009@student.example.test", "+6591000029", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Hannah Lim", "http://localhost:5156/singpass/v3/fapi", null, "9c9b5d05-1abd-4e11-817f-37c94f26be56", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2029L, "ESERVICE", "Hannah Lim", null, "S7000029F", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1032L, "ACTIVE", null, "isaac.lim.smu010@student.example.test", "+6591000030", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Isaac Lim", "http://localhost:5156/singpass/v3/fapi", null, "bd68ebe6-8847-46d1-8774-0160e0c773d7", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2030L, "ESERVICE", "Isaac Lim", null, "S7000030G", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1033L, "ACTIVE", null, "jia.min.lim.sutd001@student.example.test", "+6591000031", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Jia Min Lim", "http://localhost:5156/singpass/v3/fapi", null, "b964c08b-a798-458c-bce0-46c306da1ab6", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2031L, "ESERVICE", "Jia Min Lim", null, "S7000031H", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1034L, "ACTIVE", null, "kai.lim.sutd002@student.example.test", "+6591000032", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Kai Lim", "http://localhost:5156/singpass/v3/fapi", null, "28e41f28-5c7a-4c3f-9f38-dd45393a2bbc", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2032L, "ESERVICE", "Kai Lim", null, "S7000032J", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1035L, "ACTIVE", null, "liyana.lim.sutd003@student.example.test", "+6591000033", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Liyana Lim", "http://localhost:5156/singpass/v3/fapi", null, "717dac1c-c021-420b-b660-b94d041d7e67", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2033L, "ESERVICE", "Liyana Lim", null, "S7000033K", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1036L, "ACTIVE", null, "marcus.lim.sutd004@student.example.test", "+6591000034", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Marcus Lim", "http://localhost:5156/singpass/v3/fapi", null, "d5ce54ea-c2ab-4d29-bd4b-6905ffba67e3", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2034L, "ESERVICE", "Marcus Lim", null, "S7000034L", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1037L, "ACTIVE", null, "nadia.lim.sutd005@student.example.test", "+6591000035", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Nadia Lim", "http://localhost:5156/singpass/v3/fapi", null, "619e2335-7ff5-4884-abf7-929a2156f9b2", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2035L, "ESERVICE", "Nadia Lim", null, "S7000035M", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1038L, "ACTIVE", null, "oliver.lim.sutd006@student.example.test", "+6591000036", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Oliver Lim", "http://localhost:5156/singpass/v3/fapi", null, "76cd6639-f486-43b7-acf0-4ba34f72f961", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2036L, "ESERVICE", "Oliver Lim", null, "S7000036N", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1039L, "ACTIVE", null, "priya.lim.sutd007@student.example.test", "+6591000037", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Priya Lim", "http://localhost:5156/singpass/v3/fapi", null, "4c357bdb-2382-4105-8349-c30ef28a5c0d", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2037L, "ESERVICE", "Priya Lim", null, "S7000037P", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1040L, "ACTIVE", null, "ryan.lim.sutd008@student.example.test", "+6591000038", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Ryan Lim", "http://localhost:5156/singpass/v3/fapi", null, "c2974885-5c5f-4df5-8515-4ecb840e90db", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2038L, "ESERVICE", "Ryan Lim", null, "S7000038Q", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1041L, "ACTIVE", null, "siti.lim.sutd009@student.example.test", "+6591000039", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Siti Lim", "http://localhost:5156/singpass/v3/fapi", null, "75a84c54-f406-42e0-96c7-769fdfe64c70", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2039L, "ESERVICE", "Siti Lim", null, "S7000039R", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1042L, "ACTIVE", null, "wei.jie.lim.sutd010@student.example.test", "+6591000040", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Wei Jie Lim", "http://localhost:5156/singpass/v3/fapi", null, "8c751878-9c50-40ba-adba-e980605817a6", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2040L, "ESERVICE", "Wei Jie Lim", null, "S7000040T", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1043L, "ACTIVE", null, "aarav.lee.sit001@student.example.test", "+6591000041", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Aarav Lee", "http://localhost:5156/singpass/v3/fapi", null, "91ad0a28-92a0-47ac-a29c-e5cc07835697", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2041L, "ESERVICE", "Aarav Lee", null, "S7000041U", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1044L, "ACTIVE", null, "aisha.lee.sit002@student.example.test", "+6591000042", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Aisha Lee", "http://localhost:5156/singpass/v3/fapi", null, "a453341a-96e6-422f-889c-474ee17f5cec", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2042L, "ESERVICE", "Aisha Lee", null, "S7000042V", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1045L, "ACTIVE", null, "benjamin.lee.sit003@student.example.test", "+6591000043", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Benjamin Lee", "http://localhost:5156/singpass/v3/fapi", null, "2125c836-da1d-4a2b-b26b-e22dd1b99b8a", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2043L, "ESERVICE", "Benjamin Lee", null, "S7000043W", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1046L, "ACTIVE", null, "chloe.lee.sit004@student.example.test", "+6591000044", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Chloe Lee", "http://localhost:5156/singpass/v3/fapi", null, "d5b8c485-4ab1-497b-a0bb-9feefcebde71", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2044L, "ESERVICE", "Chloe Lee", null, "S7000044X", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1047L, "ACTIVE", null, "daniel.lee.sit005@student.example.test", "+6591000045", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Daniel Lee", "http://localhost:5156/singpass/v3/fapi", null, "8b49de77-611f-4897-bb44-e9ba90aa585d", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2045L, "ESERVICE", "Daniel Lee", null, "S7000045Y", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1048L, "ACTIVE", null, "emma.lee.sit006@student.example.test", "+6591000046", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Emma Lee", "http://localhost:5156/singpass/v3/fapi", null, "61275942-e60f-4aa3-969b-a313e4c7ccc9", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2046L, "ESERVICE", "Emma Lee", null, "S7000046Z", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1049L, "ACTIVE", null, "farhan.lee.sit007@student.example.test", "+6591000047", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Farhan Lee", "http://localhost:5156/singpass/v3/fapi", null, "796dca6f-69e2-481f-9b0c-bbeb158d9d92", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2047L, "ESERVICE", "Farhan Lee", null, "S7000047A", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1050L, "ACTIVE", null, "grace.lee.sit008@student.example.test", "+6591000048", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Grace Lee", "http://localhost:5156/singpass/v3/fapi", null, "6a3332d6-7959-41e2-8a16-633aeb039154", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2048L, "ESERVICE", "Grace Lee", null, "S7000048B", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1051L, "ACTIVE", null, "hannah.lee.sit009@student.example.test", "+6591000049", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Hannah Lee", "http://localhost:5156/singpass/v3/fapi", null, "50369a6f-b13e-43fd-9e3a-85c7a5d90cdd", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2049L, "ESERVICE", "Hannah Lee", null, "S7000049C", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1052L, "ACTIVE", null, "isaac.lee.sit010@student.example.test", "+6591000050", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Isaac Lee", "http://localhost:5156/singpass/v3/fapi", null, "89f4c2eb-e067-4b33-8fb2-7c8ddd4a7af2", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2050L, "ESERVICE", "Isaac Lee", null, "S7000050D", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" }
                });

            migrationBuilder.UpdateData(
                schema: "org",
                table: "Organization",
                keyColumn: "OrganizationId",
                keyValue: 2L,
                columns: new[] { "MockPassSchoolCode", "OrganizationCode", "OrganizationName" },
                values: new object[] { "NUS", "NUS", "National University of Singapore" });

            migrationBuilder.InsertData(
                schema: "org",
                table: "Organization",
                columns: new[] { "OrganizationId", "CreatedAt", "EffectiveFromUtc", "EffectiveToUtc", "MockPassSchoolCode", "ParentOrganizationId", "OrganizationStatusCode", "OrganizationCode", "OrganizationName", "OrganizationTypeCode", "UpdatedAt" },
                values: new object[,]
                {
                    { 3L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "NTU", 1L, "ACTIVE", "NTU", "Nanyang Technological University", "SCHOOL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "SMU", 1L, "ACTIVE", "SMU", "Singapore Management University", "SCHOOL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "SUTD", 1L, "ACTIVE", "SUTD", "Singapore University of Technology and Design", "SCHOOL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 6L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "SIT", 1L, "ACTIVE", "SIT", "Singapore Institute of Technology", "SCHOOL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2001L,
                columns: new[] { "DateOfBirth", "MockPassPersonId", "IdentityNumberMasked", "OfficialAddress", "OfficialEmail", "FullName", "OfficialMobile", "PreferredAddress", "PreferredEmail", "PreferredMobile" },
                values: new object[] { new DateOnly(2001, 4, 8), "d4329977-8fa2-4f2c-9cca-2ec65b245757", "S7000001A", "1 Education Avenue, Singapore 100001", "aarav.tan.nus001@student.example.test", "Aarav Tan", "+6591000001", "1 Education Avenue, Singapore 100001", "aarav.tan.nus001@student.example.test", "+6591000001" });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2002L,
                columns: new[] { "DateOfBirth", "MockPassPersonId", "IdentityNumberMasked", "OfficialAddress", "OfficialEmail", "OfficialMobile", "PreferredAddress", "PreferredEmail", "PreferredMobile" },
                values: new object[] { new DateOnly(2002, 7, 15), "504615ea-1b59-4a2b-adae-96f00c2590cd", "S7000002B", "2 Education Avenue, Singapore 100002", "aisha.tan.nus002@student.example.test", "+6591000002", "2 Education Avenue, Singapore 100002", "aisha.tan.nus002@student.example.test", "+6591000002" });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2003L,
                columns: new[] { "DateOfBirth", "MockPassPersonId", "IdentityNumberMasked", "OfficialAddress", "OfficialEmail", "FullName", "OfficialMobile", "PreferredAddress", "PreferredEmail", "PreferredMobile" },
                values: new object[] { new DateOnly(2003, 10, 22), "7ec809bd-c7e8-4ddd-994e-e2e9f474adb2", "S7000003C", "3 Education Avenue, Singapore 100003", "benjamin.tan.nus003@student.example.test", "Benjamin Tan", "+6591000003", "3 Education Avenue, Singapore 100003", "benjamin.tan.nus003@student.example.test", "+6591000003" });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2004L,
                columns: new[] { "ResidencyStatusCode", "DateOfBirth", "MockPassPersonId", "IdentityNumberMasked", "NationalityCode", "OfficialAddress", "OfficialEmail", "FullName", "OfficialMobile", "PreferredAddress", "PreferredEmail", "PreferredMobile" },
                values: new object[] { "CITIZEN", new DateOnly(2004, 1, 2), "43b2b617-9e83-4129-a8b9-358446731674", "S7000004D", "SG", "4 Education Avenue, Singapore 100004", "chloe.tan.nus004@student.example.test", "Chloe Tan", "+6591000004", "4 Education Avenue, Singapore 100004", "chloe.tan.nus004@student.example.test", "+6591000004" });

            migrationBuilder.InsertData(
                schema: "person",
                table: "Person",
                columns: new[] { "PersonId", "ResidencyStatusCode", "CreatedAt", "DateOfBirth", "MockPassPersonId", "IdentityNumberMasked", "NationalityCode", "OfficialAddress", "OfficialEmail", "FullName", "OfficialMobile", "PersonStatusCode", "PreferredAddress", "PreferredEmail", "PreferredMobile", "SourceUpdatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 2005L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2005, 4, 9), "deafdd4e-69f4-451f-9c90-5e8deaff58dd", "S7000005E", "SG", "5 Education Avenue, Singapore 100005", "daniel.tan.nus005@student.example.test", "Daniel Tan", "+6591000005", "ACTIVE", "5 Education Avenue, Singapore 100005", "daniel.tan.nus005@student.example.test", "+6591000005", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2006L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2001, 7, 16), "eabeb7c7-7254-4f02-8b1c-9b5283087c4b", "S7000006F", "SG", "6 Education Avenue, Singapore 100006", "emma.tan.nus006@student.example.test", "Emma Tan", "+6591000006", "ACTIVE", "6 Education Avenue, Singapore 100006", "emma.tan.nus006@student.example.test", "+6591000006", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2007L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2002, 10, 23), "08baf5dd-07c7-4030-8163-be270f97346e", "S7000007G", "SG", "7 Education Avenue, Singapore 100007", "farhan.tan.nus007@student.example.test", "Farhan Tan", "+6591000007", "ACTIVE", "7 Education Avenue, Singapore 100007", "farhan.tan.nus007@student.example.test", "+6591000007", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2008L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2003, 1, 3), "694fe5d5-21bb-4614-b1bb-7a85d1323cb6", "S7000008H", "SG", "8 Education Avenue, Singapore 100008", "grace.tan.nus008@student.example.test", "Grace Tan", "+6591000008", "ACTIVE", "8 Education Avenue, Singapore 100008", "grace.tan.nus008@student.example.test", "+6591000008", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2009L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2004, 4, 10), "43a59bc6-c7e2-4402-82e8-155c50e3e3b1", "S7000009J", "SG", "9 Education Avenue, Singapore 100009", "hannah.tan.nus009@student.example.test", "Hannah Tan", "+6591000009", "ACTIVE", "9 Education Avenue, Singapore 100009", "hannah.tan.nus009@student.example.test", "+6591000009", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2010L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2005, 7, 17), "fd4168bc-3a4b-4339-8e1b-4dfef896d0ec", "S7000010K", "SG", "10 Education Avenue, Singapore 100010", "isaac.tan.nus010@student.example.test", "Isaac Tan", "+6591000010", "ACTIVE", "10 Education Avenue, Singapore 100010", "isaac.tan.nus010@student.example.test", "+6591000010", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2011L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2001, 10, 24), "3ed57123-302e-4af5-9745-8b9a11cf84d2", "S7000011L", "SG", "11 Education Avenue, Singapore 100011", "jia.min.tan.ntu001@student.example.test", "Jia Min Tan", "+6591000011", "ACTIVE", "11 Education Avenue, Singapore 100011", "jia.min.tan.ntu001@student.example.test", "+6591000011", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2012L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2002, 1, 4), "3fbf4c48-1c06-47a1-a392-14e1ce88f2ef", "S7000012M", "SG", "12 Education Avenue, Singapore 100012", "kai.tan.ntu002@student.example.test", "Kai Tan", "+6591000012", "ACTIVE", "12 Education Avenue, Singapore 100012", "kai.tan.ntu002@student.example.test", "+6591000012", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2013L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2003, 4, 11), "737a1787-711f-4d4d-95cc-20fa351fb45f", "S7000013N", "SG", "13 Education Avenue, Singapore 100013", "liyana.tan.ntu003@student.example.test", "Liyana Tan", "+6591000013", "ACTIVE", "13 Education Avenue, Singapore 100013", "liyana.tan.ntu003@student.example.test", "+6591000013", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2014L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2004, 7, 18), "e63b844e-7c1e-4478-a8b8-b51bf35b0f5b", "S7000014P", "SG", "14 Education Avenue, Singapore 100014", "marcus.tan.ntu004@student.example.test", "Marcus Tan", "+6591000014", "ACTIVE", "14 Education Avenue, Singapore 100014", "marcus.tan.ntu004@student.example.test", "+6591000014", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2015L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2005, 10, 25), "ce28804a-407e-44e8-a228-a6d895744b85", "S7000015Q", "SG", "15 Education Avenue, Singapore 100015", "nadia.tan.ntu005@student.example.test", "Nadia Tan", "+6591000015", "ACTIVE", "15 Education Avenue, Singapore 100015", "nadia.tan.ntu005@student.example.test", "+6591000015", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2016L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2001, 1, 5), "fd308af1-279f-44ea-b859-6bd4c395e064", "S7000016R", "SG", "16 Education Avenue, Singapore 100016", "oliver.tan.ntu006@student.example.test", "Oliver Tan", "+6591000016", "ACTIVE", "16 Education Avenue, Singapore 100016", "oliver.tan.ntu006@student.example.test", "+6591000016", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2017L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2002, 4, 12), "a68d6c07-8452-4d21-9ebc-4b08cac341c9", "S7000017T", "SG", "17 Education Avenue, Singapore 100017", "priya.tan.ntu007@student.example.test", "Priya Tan", "+6591000017", "ACTIVE", "17 Education Avenue, Singapore 100017", "priya.tan.ntu007@student.example.test", "+6591000017", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2018L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2003, 7, 19), "ef92ca13-3e94-4997-9702-90bb42675404", "S7000018U", "SG", "18 Education Avenue, Singapore 100018", "ryan.tan.ntu008@student.example.test", "Ryan Tan", "+6591000018", "ACTIVE", "18 Education Avenue, Singapore 100018", "ryan.tan.ntu008@student.example.test", "+6591000018", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2019L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2004, 10, 26), "4d7e0a14-bc39-47aa-8487-4808f5b605ef", "S7000019V", "SG", "19 Education Avenue, Singapore 100019", "siti.tan.ntu009@student.example.test", "Siti Tan", "+6591000019", "ACTIVE", "19 Education Avenue, Singapore 100019", "siti.tan.ntu009@student.example.test", "+6591000019", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2020L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2005, 1, 6), "d7091bae-c4d7-41ba-ac9a-8e40201eb4da", "S7000020W", "SG", "20 Education Avenue, Singapore 100020", "wei.jie.tan.ntu010@student.example.test", "Wei Jie Tan", "+6591000020", "ACTIVE", "20 Education Avenue, Singapore 100020", "wei.jie.tan.ntu010@student.example.test", "+6591000020", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2021L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2001, 4, 13), "015bc9cc-d1cb-4c98-8bf3-9d8a6f9524bc", "S7000021X", "SG", "21 Education Avenue, Singapore 100021", "aarav.lim.smu001@student.example.test", "Aarav Lim", "+6591000021", "ACTIVE", "21 Education Avenue, Singapore 100021", "aarav.lim.smu001@student.example.test", "+6591000021", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2022L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2002, 7, 20), "88c38bbc-5202-405a-b155-511d191075dd", "S7000022Y", "SG", "22 Education Avenue, Singapore 100022", "aisha.lim.smu002@student.example.test", "Aisha Lim", "+6591000022", "ACTIVE", "22 Education Avenue, Singapore 100022", "aisha.lim.smu002@student.example.test", "+6591000022", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2023L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2003, 10, 27), "9ad36576-33cf-45eb-a930-97751426c8ff", "S7000023Z", "SG", "23 Education Avenue, Singapore 100023", "benjamin.lim.smu003@student.example.test", "Benjamin Lim", "+6591000023", "ACTIVE", "23 Education Avenue, Singapore 100023", "benjamin.lim.smu003@student.example.test", "+6591000023", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2024L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2004, 1, 7), "76a09bec-5dc9-4c4e-ad07-3ebf242a4e00", "S7000024A", "SG", "24 Education Avenue, Singapore 100024", "chloe.lim.smu004@student.example.test", "Chloe Lim", "+6591000024", "ACTIVE", "24 Education Avenue, Singapore 100024", "chloe.lim.smu004@student.example.test", "+6591000024", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2025L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2005, 4, 14), "45d455ee-5b04-485e-9195-7c3a788d2375", "S7000025B", "SG", "25 Education Avenue, Singapore 100025", "daniel.lim.smu005@student.example.test", "Daniel Lim", "+6591000025", "ACTIVE", "25 Education Avenue, Singapore 100025", "daniel.lim.smu005@student.example.test", "+6591000025", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2026L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2001, 7, 21), "9313e8fc-dcfc-4f73-b14c-eef53edce1f2", "S7000026C", "SG", "26 Education Avenue, Singapore 100026", "emma.lim.smu006@student.example.test", "Emma Lim", "+6591000026", "ACTIVE", "26 Education Avenue, Singapore 100026", "emma.lim.smu006@student.example.test", "+6591000026", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2027L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2002, 10, 1), "464ebf11-4cfd-4165-a376-7fb6dfb21b69", "S7000027D", "SG", "27 Education Avenue, Singapore 100027", "farhan.lim.smu007@student.example.test", "Farhan Lim", "+6591000027", "ACTIVE", "27 Education Avenue, Singapore 100027", "farhan.lim.smu007@student.example.test", "+6591000027", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2028L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2003, 1, 8), "5f3740d4-6ba5-4a05-b7c8-197636bb0de5", "S7000028E", "SG", "28 Education Avenue, Singapore 100028", "grace.lim.smu008@student.example.test", "Grace Lim", "+6591000028", "ACTIVE", "28 Education Avenue, Singapore 100028", "grace.lim.smu008@student.example.test", "+6591000028", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2029L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2004, 4, 15), "9c9b5d05-1abd-4e11-817f-37c94f26be56", "S7000029F", "SG", "29 Education Avenue, Singapore 100029", "hannah.lim.smu009@student.example.test", "Hannah Lim", "+6591000029", "ACTIVE", "29 Education Avenue, Singapore 100029", "hannah.lim.smu009@student.example.test", "+6591000029", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2030L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2005, 7, 22), "bd68ebe6-8847-46d1-8774-0160e0c773d7", "S7000030G", "SG", "30 Education Avenue, Singapore 100030", "isaac.lim.smu010@student.example.test", "Isaac Lim", "+6591000030", "ACTIVE", "30 Education Avenue, Singapore 100030", "isaac.lim.smu010@student.example.test", "+6591000030", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2031L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2001, 10, 2), "b964c08b-a798-458c-bce0-46c306da1ab6", "S7000031H", "SG", "31 Education Avenue, Singapore 100031", "jia.min.lim.sutd001@student.example.test", "Jia Min Lim", "+6591000031", "ACTIVE", "31 Education Avenue, Singapore 100031", "jia.min.lim.sutd001@student.example.test", "+6591000031", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2032L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2002, 1, 9), "28e41f28-5c7a-4c3f-9f38-dd45393a2bbc", "S7000032J", "SG", "32 Education Avenue, Singapore 100032", "kai.lim.sutd002@student.example.test", "Kai Lim", "+6591000032", "ACTIVE", "32 Education Avenue, Singapore 100032", "kai.lim.sutd002@student.example.test", "+6591000032", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2033L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2003, 4, 16), "717dac1c-c021-420b-b660-b94d041d7e67", "S7000033K", "SG", "33 Education Avenue, Singapore 100033", "liyana.lim.sutd003@student.example.test", "Liyana Lim", "+6591000033", "ACTIVE", "33 Education Avenue, Singapore 100033", "liyana.lim.sutd003@student.example.test", "+6591000033", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2034L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2004, 7, 23), "d5ce54ea-c2ab-4d29-bd4b-6905ffba67e3", "S7000034L", "SG", "34 Education Avenue, Singapore 100034", "marcus.lim.sutd004@student.example.test", "Marcus Lim", "+6591000034", "ACTIVE", "34 Education Avenue, Singapore 100034", "marcus.lim.sutd004@student.example.test", "+6591000034", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2035L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2005, 10, 3), "619e2335-7ff5-4884-abf7-929a2156f9b2", "S7000035M", "SG", "35 Education Avenue, Singapore 100035", "nadia.lim.sutd005@student.example.test", "Nadia Lim", "+6591000035", "ACTIVE", "35 Education Avenue, Singapore 100035", "nadia.lim.sutd005@student.example.test", "+6591000035", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2036L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2001, 1, 10), "76cd6639-f486-43b7-acf0-4ba34f72f961", "S7000036N", "SG", "36 Education Avenue, Singapore 100036", "oliver.lim.sutd006@student.example.test", "Oliver Lim", "+6591000036", "ACTIVE", "36 Education Avenue, Singapore 100036", "oliver.lim.sutd006@student.example.test", "+6591000036", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2037L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2002, 4, 17), "4c357bdb-2382-4105-8349-c30ef28a5c0d", "S7000037P", "SG", "37 Education Avenue, Singapore 100037", "priya.lim.sutd007@student.example.test", "Priya Lim", "+6591000037", "ACTIVE", "37 Education Avenue, Singapore 100037", "priya.lim.sutd007@student.example.test", "+6591000037", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2038L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2003, 7, 24), "c2974885-5c5f-4df5-8515-4ecb840e90db", "S7000038Q", "SG", "38 Education Avenue, Singapore 100038", "ryan.lim.sutd008@student.example.test", "Ryan Lim", "+6591000038", "ACTIVE", "38 Education Avenue, Singapore 100038", "ryan.lim.sutd008@student.example.test", "+6591000038", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2039L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2004, 10, 4), "75a84c54-f406-42e0-96c7-769fdfe64c70", "S7000039R", "SG", "39 Education Avenue, Singapore 100039", "siti.lim.sutd009@student.example.test", "Siti Lim", "+6591000039", "ACTIVE", "39 Education Avenue, Singapore 100039", "siti.lim.sutd009@student.example.test", "+6591000039", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2040L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2005, 1, 11), "8c751878-9c50-40ba-adba-e980605817a6", "S7000040T", "SG", "40 Education Avenue, Singapore 100040", "wei.jie.lim.sutd010@student.example.test", "Wei Jie Lim", "+6591000040", "ACTIVE", "40 Education Avenue, Singapore 100040", "wei.jie.lim.sutd010@student.example.test", "+6591000040", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2041L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2001, 4, 18), "91ad0a28-92a0-47ac-a29c-e5cc07835697", "S7000041U", "SG", "41 Education Avenue, Singapore 100041", "aarav.lee.sit001@student.example.test", "Aarav Lee", "+6591000041", "ACTIVE", "41 Education Avenue, Singapore 100041", "aarav.lee.sit001@student.example.test", "+6591000041", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2042L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2002, 7, 25), "a453341a-96e6-422f-889c-474ee17f5cec", "S7000042V", "SG", "42 Education Avenue, Singapore 100042", "aisha.lee.sit002@student.example.test", "Aisha Lee", "+6591000042", "ACTIVE", "42 Education Avenue, Singapore 100042", "aisha.lee.sit002@student.example.test", "+6591000042", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2043L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2003, 10, 5), "2125c836-da1d-4a2b-b26b-e22dd1b99b8a", "S7000043W", "SG", "43 Education Avenue, Singapore 100043", "benjamin.lee.sit003@student.example.test", "Benjamin Lee", "+6591000043", "ACTIVE", "43 Education Avenue, Singapore 100043", "benjamin.lee.sit003@student.example.test", "+6591000043", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2044L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2004, 1, 12), "d5b8c485-4ab1-497b-a0bb-9feefcebde71", "S7000044X", "SG", "44 Education Avenue, Singapore 100044", "chloe.lee.sit004@student.example.test", "Chloe Lee", "+6591000044", "ACTIVE", "44 Education Avenue, Singapore 100044", "chloe.lee.sit004@student.example.test", "+6591000044", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2045L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2005, 4, 19), "8b49de77-611f-4897-bb44-e9ba90aa585d", "S7000045Y", "SG", "45 Education Avenue, Singapore 100045", "daniel.lee.sit005@student.example.test", "Daniel Lee", "+6591000045", "ACTIVE", "45 Education Avenue, Singapore 100045", "daniel.lee.sit005@student.example.test", "+6591000045", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2046L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2001, 7, 26), "61275942-e60f-4aa3-969b-a313e4c7ccc9", "S7000046Z", "SG", "46 Education Avenue, Singapore 100046", "emma.lee.sit006@student.example.test", "Emma Lee", "+6591000046", "ACTIVE", "46 Education Avenue, Singapore 100046", "emma.lee.sit006@student.example.test", "+6591000046", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2047L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2002, 10, 6), "796dca6f-69e2-481f-9b0c-bbeb158d9d92", "S7000047A", "SG", "47 Education Avenue, Singapore 100047", "farhan.lee.sit007@student.example.test", "Farhan Lee", "+6591000047", "ACTIVE", "47 Education Avenue, Singapore 100047", "farhan.lee.sit007@student.example.test", "+6591000047", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2048L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2003, 1, 13), "6a3332d6-7959-41e2-8a16-633aeb039154", "S7000048B", "SG", "48 Education Avenue, Singapore 100048", "grace.lee.sit008@student.example.test", "Grace Lee", "+6591000048", "ACTIVE", "48 Education Avenue, Singapore 100048", "grace.lee.sit008@student.example.test", "+6591000048", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2049L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2004, 4, 20), "50369a6f-b13e-43fd-9e3a-85c7a5d90cdd", "S7000049C", "SG", "49 Education Avenue, Singapore 100049", "hannah.lee.sit009@student.example.test", "Hannah Lee", "+6591000049", "ACTIVE", "49 Education Avenue, Singapore 100049", "hannah.lee.sit009@student.example.test", "+6591000049", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2050L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2005, 7, 27), "89f4c2eb-e067-4b33-8fb2-7c8ddd4a7af2", "S7000050D", "SG", "50 Education Avenue, Singapore 100050", "isaac.lee.sit010@student.example.test", "Isaac Lee", "+6591000050", "ACTIVE", "50 Education Avenue, Singapore 100050", "isaac.lee.sit010@student.example.test", "+6591000050", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20011L,
                columns: new[] { "IdentifierMasked", "IdentifierValueHash" },
                values: new object[] { "d4329977-8fa2-4f2c-9cca-2ec65b245757", new byte[] { 247, 182, 49, 7, 89, 161, 92, 46, 151, 28, 15, 132, 138, 147, 183, 170, 4, 84, 164, 191, 85, 62, 186, 189, 95, 249, 139, 38, 72, 137, 123, 166 } });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20012L,
                columns: new[] { "IdentifierMasked", "IdentifierValueHash" },
                values: new object[] { "S7000001A", new byte[] { 255, 64, 176, 109, 148, 25, 213, 57, 230, 174, 192, 244, 96, 57, 107, 79, 227, 121, 30, 193, 18, 48, 238, 127, 133, 187, 173, 176, 238, 132, 127, 92 } });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20021L,
                columns: new[] { "IdentifierMasked", "IdentifierValueHash" },
                values: new object[] { "504615ea-1b59-4a2b-adae-96f00c2590cd", new byte[] { 253, 144, 171, 184, 227, 239, 161, 109, 177, 90, 247, 1, 37, 191, 90, 96, 147, 43, 222, 111, 162, 118, 243, 87, 40, 140, 87, 199, 217, 113, 148, 176 } });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20022L,
                columns: new[] { "IdentifierMasked", "IdentifierValueHash" },
                values: new object[] { "S7000002B", new byte[] { 41, 219, 200, 245, 75, 165, 204, 236, 199, 203, 93, 19, 148, 106, 120, 118, 109, 209, 98, 67, 62, 93, 48, 190, 109, 103, 133, 6, 187, 239, 200, 145 } });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20031L,
                columns: new[] { "IdentifierMasked", "IdentifierValueHash" },
                values: new object[] { "7ec809bd-c7e8-4ddd-994e-e2e9f474adb2", new byte[] { 9, 79, 143, 104, 244, 201, 217, 233, 159, 232, 179, 146, 203, 182, 209, 67, 32, 39, 69, 107, 207, 194, 216, 165, 11, 241, 63, 145, 54, 201, 135, 228 } });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20032L,
                columns: new[] { "IdentifierMasked", "IdentifierValueHash" },
                values: new object[] { "S7000003C", new byte[] { 212, 77, 100, 236, 131, 116, 15, 75, 52, 177, 171, 45, 2, 253, 73, 249, 127, 73, 149, 90, 61, 86, 55, 174, 192, 94, 229, 122, 119, 195, 183, 22 } });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20041L,
                columns: new[] { "IdentifierMasked", "IdentifierValueHash" },
                values: new object[] { "43b2b617-9e83-4129-a8b9-358446731674", new byte[] { 71, 146, 56, 165, 96, 236, 27, 252, 102, 255, 80, 9, 250, 88, 59, 21, 91, 185, 252, 138, 51, 245, 66, 128, 136, 225, 110, 122, 8, 158, 121, 186 } });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20042L,
                columns: new[] { "IdentifierMasked", "IdentifierValueHash" },
                values: new object[] { "S7000004D", new byte[] { 41, 200, 164, 8, 203, 32, 54, 50, 227, 128, 110, 52, 242, 131, 41, 150, 193, 163, 197, 59, 28, 248, 138, 223, 249, 234, 52, 107, 42, 205, 113, 204 } });

            migrationBuilder.InsertData(
                schema: "person",
                table: "PersonIdentifier",
                columns: new[] { "PersonIdentifierId", "CreatedAtUtc", "EffectiveFrom", "EffectiveTo", "IdentifierMasked", "IdentifierStatusCode", "IdentifierTypeCode", "IdentifierValueEncrypted", "IdentifierValueHash", "IsPrimary", "IssuedByAuthority", "IssuingCountryCode", "PersonId", "SourceSystemCode", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { 20051L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "deafdd4e-69f4-451f-9c90-5e8deaff58dd", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 145, 111, 79, 172, 177, 185, 135, 66, 113, 214, 55, 40, 73, 52, 206, 164, 246, 29, 7, 183, 18, 137, 28, 177, 42, 116, 214, 58, 31, 191, 40, 93 }, false, "MOCKPASS", "SG", 2005L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20052L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000005E", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 169, 120, 133, 255, 150, 89, 204, 220, 54, 133, 178, 250, 76, 114, 183, 220, 144, 220, 241, 102, 96, 143, 200, 42, 175, 244, 197, 179, 178, 164, 148, 47 }, true, "MOCKPASS", "SG", 2005L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20061L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "eabeb7c7-7254-4f02-8b1c-9b5283087c4b", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 64, 199, 173, 100, 31, 250, 65, 1, 244, 42, 3, 29, 162, 48, 9, 32, 185, 99, 66, 238, 86, 109, 9, 217, 236, 132, 222, 69, 75, 138, 249, 37 }, false, "MOCKPASS", "SG", 2006L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20062L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000006F", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 3, 242, 177, 85, 167, 16, 73, 54, 223, 36, 27, 132, 135, 53, 110, 225, 158, 1, 237, 131, 121, 121, 169, 10, 71, 250, 146, 11, 103, 48, 174, 236 }, true, "MOCKPASS", "SG", 2006L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20071L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "08baf5dd-07c7-4030-8163-be270f97346e", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 127, 116, 250, 194, 90, 215, 121, 5, 22, 144, 26, 220, 54, 14, 231, 75, 175, 222, 243, 191, 48, 11, 17, 46, 152, 109, 61, 184, 114, 132, 10, 19 }, false, "MOCKPASS", "SG", 2007L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20072L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000007G", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 253, 96, 251, 34, 94, 186, 81, 18, 85, 70, 22, 144, 205, 166, 149, 191, 196, 96, 81, 193, 194, 147, 40, 228, 55, 65, 123, 223, 195, 80, 216, 218 }, true, "MOCKPASS", "SG", 2007L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20081L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "694fe5d5-21bb-4614-b1bb-7a85d1323cb6", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 45, 110, 35, 102, 144, 131, 188, 24, 106, 92, 61, 172, 1, 162, 46, 248, 95, 183, 58, 204, 141, 172, 218, 186, 133, 157, 204, 140, 59, 109, 3, 217 }, false, "MOCKPASS", "SG", 2008L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20082L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000008H", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 134, 34, 57, 68, 4, 17, 153, 226, 171, 103, 226, 117, 116, 230, 66, 160, 133, 44, 237, 157, 201, 198, 42, 222, 64, 149, 198, 159, 147, 146, 211, 3 }, true, "MOCKPASS", "SG", 2008L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20091L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "43a59bc6-c7e2-4402-82e8-155c50e3e3b1", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 49, 226, 142, 120, 97, 245, 106, 75, 168, 179, 42, 145, 254, 169, 139, 227, 35, 162, 115, 136, 183, 4, 179, 110, 210, 222, 108, 70, 12, 205, 101, 166 }, false, "MOCKPASS", "SG", 2009L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20092L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000009J", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 115, 64, 236, 133, 148, 76, 38, 16, 96, 173, 55, 206, 35, 136, 242, 208, 186, 75, 127, 7, 238, 180, 15, 89, 139, 221, 230, 145, 135, 149, 189, 127 }, true, "MOCKPASS", "SG", 2009L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20101L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "fd4168bc-3a4b-4339-8e1b-4dfef896d0ec", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 128, 66, 177, 242, 80, 217, 22, 75, 178, 89, 67, 154, 214, 43, 188, 247, 78, 163, 102, 204, 105, 138, 150, 223, 59, 110, 156, 131, 110, 140, 3, 123 }, false, "MOCKPASS", "SG", 2010L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20102L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000010K", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 224, 90, 113, 32, 104, 21, 116, 211, 50, 171, 35, 251, 30, 169, 6, 149, 236, 113, 171, 207, 136, 35, 137, 150, 95, 227, 118, 16, 218, 210, 246, 119 }, true, "MOCKPASS", "SG", 2010L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20111L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "3ed57123-302e-4af5-9745-8b9a11cf84d2", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 171, 199, 231, 1, 156, 232, 72, 64, 233, 243, 136, 39, 110, 255, 121, 151, 58, 18, 95, 129, 205, 40, 169, 28, 153, 187, 117, 77, 223, 188, 176, 124 }, false, "MOCKPASS", "SG", 2011L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20112L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000011L", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 3, 49, 247, 8, 39, 203, 144, 162, 29, 33, 30, 234, 240, 142, 65, 64, 20, 112, 82, 176, 156, 85, 67, 75, 255, 215, 28, 123, 30, 229, 252, 160 }, true, "MOCKPASS", "SG", 2011L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20121L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "3fbf4c48-1c06-47a1-a392-14e1ce88f2ef", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 119, 148, 131, 145, 24, 1, 250, 74, 51, 226, 13, 212, 199, 136, 83, 161, 159, 6, 177, 21, 190, 192, 147, 221, 59, 215, 103, 15, 118, 78, 226, 105 }, false, "MOCKPASS", "SG", 2012L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20122L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000012M", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 22, 44, 227, 192, 31, 229, 107, 220, 34, 242, 136, 218, 197, 64, 180, 172, 126, 182, 157, 146, 239, 255, 105, 80, 193, 14, 34, 101, 236, 113, 35, 15 }, true, "MOCKPASS", "SG", 2012L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20131L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "737a1787-711f-4d4d-95cc-20fa351fb45f", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 188, 99, 32, 30, 252, 234, 3, 67, 136, 30, 134, 110, 151, 108, 50, 59, 86, 229, 156, 15, 87, 194, 129, 41, 5, 251, 90, 143, 65, 238, 138, 166 }, false, "MOCKPASS", "SG", 2013L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20132L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000013N", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 126, 16, 130, 167, 253, 247, 43, 66, 46, 129, 185, 163, 174, 140, 59, 173, 65, 110, 171, 236, 111, 17, 181, 215, 189, 176, 69, 3, 41, 233, 56, 157 }, true, "MOCKPASS", "SG", 2013L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20141L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "e63b844e-7c1e-4478-a8b8-b51bf35b0f5b", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 109, 172, 114, 230, 39, 212, 97, 170, 58, 73, 32, 7, 224, 141, 11, 39, 80, 250, 182, 19, 182, 71, 153, 2, 244, 84, 111, 15, 65, 193, 135, 58 }, false, "MOCKPASS", "SG", 2014L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20142L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000014P", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 27, 179, 26, 108, 240, 43, 21, 89, 158, 168, 128, 82, 219, 112, 98, 56, 175, 9, 3, 43, 247, 123, 139, 135, 99, 230, 158, 8, 188, 64, 106, 178 }, true, "MOCKPASS", "SG", 2014L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20151L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "ce28804a-407e-44e8-a228-a6d895744b85", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 130, 174, 27, 74, 23, 254, 117, 47, 213, 15, 147, 10, 120, 0, 73, 56, 31, 94, 198, 198, 20, 87, 185, 99, 26, 178, 61, 54, 66, 101, 247, 210 }, false, "MOCKPASS", "SG", 2015L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20152L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000015Q", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 130, 227, 177, 227, 11, 35, 87, 217, 198, 35, 56, 213, 0, 174, 131, 246, 110, 13, 59, 42, 173, 130, 42, 70, 168, 16, 228, 32, 176, 51, 103, 249 }, true, "MOCKPASS", "SG", 2015L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20161L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "fd308af1-279f-44ea-b859-6bd4c395e064", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 117, 38, 190, 187, 160, 98, 86, 193, 220, 246, 45, 22, 208, 164, 98, 8, 56, 69, 131, 13, 56, 213, 7, 164, 13, 157, 154, 18, 199, 248, 108, 207 }, false, "MOCKPASS", "SG", 2016L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20162L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000016R", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 239, 86, 194, 64, 226, 235, 61, 239, 10, 56, 108, 225, 236, 30, 253, 3, 218, 12, 181, 78, 119, 87, 230, 241, 237, 206, 169, 61, 247, 149, 111, 202 }, true, "MOCKPASS", "SG", 2016L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20171L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "a68d6c07-8452-4d21-9ebc-4b08cac341c9", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 137, 147, 176, 39, 234, 137, 61, 120, 88, 76, 77, 236, 55, 187, 57, 116, 216, 87, 68, 3, 53, 89, 113, 209, 62, 254, 122, 67, 100, 103, 127, 30 }, false, "MOCKPASS", "SG", 2017L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20172L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000017T", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 21, 38, 209, 115, 179, 73, 221, 84, 247, 222, 116, 65, 181, 71, 76, 136, 68, 184, 36, 49, 139, 158, 35, 57, 3, 61, 11, 79, 181, 53, 188, 9 }, true, "MOCKPASS", "SG", 2017L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20181L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "ef92ca13-3e94-4997-9702-90bb42675404", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 170, 76, 163, 209, 232, 8, 153, 62, 10, 173, 66, 204, 184, 205, 251, 107, 214, 250, 165, 185, 191, 62, 70, 70, 152, 30, 55, 111, 205, 51, 56, 105 }, false, "MOCKPASS", "SG", 2018L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20182L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000018U", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 228, 56, 82, 201, 131, 218, 171, 9, 129, 194, 34, 119, 240, 118, 217, 35, 17, 225, 119, 177, 10, 12, 174, 92, 248, 57, 184, 228, 25, 229, 135, 23 }, true, "MOCKPASS", "SG", 2018L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20191L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "4d7e0a14-bc39-47aa-8487-4808f5b605ef", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 4, 205, 227, 246, 9, 251, 81, 234, 86, 222, 166, 63, 250, 192, 128, 192, 52, 33, 149, 208, 12, 178, 164, 111, 145, 193, 136, 69, 112, 249, 180, 222 }, false, "MOCKPASS", "SG", 2019L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20192L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000019V", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 152, 117, 132, 181, 126, 59, 126, 73, 120, 210, 161, 36, 122, 54, 86, 39, 176, 5, 163, 164, 114, 135, 216, 5, 157, 243, 44, 92, 231, 33, 128, 223 }, true, "MOCKPASS", "SG", 2019L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20201L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "d7091bae-c4d7-41ba-ac9a-8e40201eb4da", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 18, 197, 7, 31, 170, 216, 110, 109, 115, 193, 199, 219, 122, 25, 120, 48, 148, 243, 185, 142, 188, 67, 181, 173, 25, 167, 221, 235, 88, 46, 110, 223 }, false, "MOCKPASS", "SG", 2020L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20202L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000020W", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 35, 14, 70, 67, 87, 104, 175, 202, 97, 188, 39, 210, 100, 195, 252, 137, 26, 104, 66, 0, 211, 104, 145, 229, 18, 84, 123, 7, 153, 184, 133, 56 }, true, "MOCKPASS", "SG", 2020L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20211L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "015bc9cc-d1cb-4c98-8bf3-9d8a6f9524bc", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 35, 165, 184, 15, 79, 178, 27, 94, 243, 19, 126, 151, 72, 140, 96, 33, 200, 208, 119, 117, 246, 248, 127, 166, 128, 232, 183, 0, 159, 145, 133, 183 }, false, "MOCKPASS", "SG", 2021L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20212L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000021X", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 10, 189, 255, 205, 183, 125, 171, 55, 90, 0, 56, 115, 32, 15, 189, 245, 173, 199, 114, 151, 23, 111, 235, 200, 182, 41, 194, 37, 209, 203, 186, 214 }, true, "MOCKPASS", "SG", 2021L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20221L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "88c38bbc-5202-405a-b155-511d191075dd", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 37, 70, 214, 147, 220, 118, 144, 110, 135, 132, 6, 245, 148, 96, 97, 42, 156, 143, 234, 131, 170, 118, 62, 128, 6, 17, 211, 30, 103, 111, 99, 88 }, false, "MOCKPASS", "SG", 2022L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20222L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000022Y", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 14, 232, 223, 9, 51, 101, 243, 252, 197, 113, 81, 135, 51, 214, 227, 172, 90, 42, 46, 211, 27, 245, 28, 123, 118, 130, 86, 48, 185, 48, 202, 160 }, true, "MOCKPASS", "SG", 2022L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20231L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "9ad36576-33cf-45eb-a930-97751426c8ff", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 251, 159, 42, 179, 196, 216, 172, 61, 176, 23, 184, 6, 125, 143, 49, 119, 209, 37, 74, 127, 138, 30, 153, 90, 9, 191, 148, 95, 6, 49, 178, 42 }, false, "MOCKPASS", "SG", 2023L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20232L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000023Z", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 194, 234, 113, 119, 11, 61, 83, 196, 249, 79, 120, 30, 15, 227, 134, 203, 91, 84, 206, 216, 211, 220, 129, 229, 44, 78, 247, 223, 255, 83, 72, 176 }, true, "MOCKPASS", "SG", 2023L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20241L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "76a09bec-5dc9-4c4e-ad07-3ebf242a4e00", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 103, 217, 105, 228, 229, 9, 174, 117, 78, 86, 95, 11, 223, 38, 10, 210, 232, 144, 70, 5, 93, 85, 216, 112, 61, 159, 228, 66, 123, 163, 186, 58 }, false, "MOCKPASS", "SG", 2024L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20242L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000024A", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 35, 71, 110, 59, 21, 232, 11, 178, 232, 70, 185, 252, 60, 65, 7, 95, 162, 196, 59, 3, 27, 1, 193, 152, 8, 23, 111, 67, 238, 130, 222, 9 }, true, "MOCKPASS", "SG", 2024L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20251L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "45d455ee-5b04-485e-9195-7c3a788d2375", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 23, 182, 136, 44, 233, 239, 117, 243, 175, 127, 248, 153, 116, 163, 54, 89, 140, 39, 52, 159, 198, 103, 97, 27, 60, 178, 44, 165, 135, 147, 104, 216 }, false, "MOCKPASS", "SG", 2025L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20252L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000025B", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 82, 22, 68, 8, 209, 176, 53, 173, 77, 18, 185, 62, 185, 197, 106, 138, 12, 231, 174, 149, 25, 182, 17, 246, 187, 137, 171, 14, 192, 101, 35, 118 }, true, "MOCKPASS", "SG", 2025L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20261L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "9313e8fc-dcfc-4f73-b14c-eef53edce1f2", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 213, 104, 248, 122, 106, 101, 170, 208, 180, 169, 193, 101, 186, 14, 83, 55, 156, 196, 17, 200, 129, 146, 209, 70, 226, 172, 187, 33, 90, 184, 163, 108 }, false, "MOCKPASS", "SG", 2026L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20262L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000026C", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 178, 242, 243, 159, 166, 14, 115, 128, 155, 170, 43, 73, 169, 173, 14, 23, 59, 93, 73, 147, 204, 91, 188, 219, 223, 192, 218, 210, 90, 138, 81, 180 }, true, "MOCKPASS", "SG", 2026L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20271L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "464ebf11-4cfd-4165-a376-7fb6dfb21b69", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 1, 53, 228, 108, 97, 2, 71, 21, 37, 107, 151, 100, 211, 244, 0, 62, 116, 93, 182, 180, 165, 19, 44, 145, 94, 77, 105, 166, 142, 87, 137, 80 }, false, "MOCKPASS", "SG", 2027L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20272L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000027D", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 17, 238, 82, 103, 211, 244, 231, 225, 151, 38, 138, 233, 219, 209, 114, 247, 171, 142, 98, 53, 253, 159, 84, 85, 181, 93, 122, 120, 41, 92, 207, 139 }, true, "MOCKPASS", "SG", 2027L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20281L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "5f3740d4-6ba5-4a05-b7c8-197636bb0de5", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 231, 46, 177, 138, 55, 55, 40, 156, 159, 31, 124, 192, 197, 66, 235, 32, 32, 103, 245, 183, 147, 217, 70, 83, 119, 59, 179, 156, 88, 212, 200, 14 }, false, "MOCKPASS", "SG", 2028L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20282L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000028E", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 77, 125, 218, 61, 211, 175, 254, 10, 227, 51, 31, 47, 173, 134, 117, 171, 122, 7, 145, 5, 152, 133, 222, 10, 91, 242, 184, 152, 21, 221, 123, 166 }, true, "MOCKPASS", "SG", 2028L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20291L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "9c9b5d05-1abd-4e11-817f-37c94f26be56", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 62, 191, 92, 164, 8, 37, 139, 199, 188, 211, 73, 21, 42, 144, 71, 163, 124, 226, 110, 139, 15, 168, 43, 238, 65, 175, 78, 213, 117, 181, 30, 223 }, false, "MOCKPASS", "SG", 2029L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20292L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000029F", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 15, 206, 58, 247, 148, 28, 66, 193, 25, 112, 108, 118, 158, 84, 2, 31, 165, 182, 53, 218, 63, 213, 87, 195, 147, 151, 85, 239, 65, 125, 241, 96 }, true, "MOCKPASS", "SG", 2029L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20301L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "bd68ebe6-8847-46d1-8774-0160e0c773d7", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 249, 187, 131, 245, 75, 125, 62, 174, 244, 64, 201, 124, 225, 123, 172, 66, 63, 144, 159, 17, 173, 221, 107, 63, 137, 89, 125, 36, 98, 179, 110, 250 }, false, "MOCKPASS", "SG", 2030L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20302L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000030G", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 46, 21, 255, 129, 199, 24, 12, 48, 110, 239, 212, 214, 189, 102, 63, 19, 85, 129, 37, 55, 237, 77, 19, 173, 43, 185, 164, 238, 165, 102, 150, 245 }, true, "MOCKPASS", "SG", 2030L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20311L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "b964c08b-a798-458c-bce0-46c306da1ab6", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 145, 64, 143, 95, 62, 140, 29, 203, 209, 216, 28, 193, 208, 109, 203, 9, 174, 118, 172, 126, 246, 198, 216, 149, 72, 143, 244, 232, 3, 108, 209, 26 }, false, "MOCKPASS", "SG", 2031L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20312L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000031H", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 4, 241, 66, 243, 173, 202, 146, 213, 89, 124, 26, 33, 73, 149, 18, 56, 183, 74, 228, 18, 121, 62, 100, 183, 89, 67, 175, 184, 38, 149, 32, 222 }, true, "MOCKPASS", "SG", 2031L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20321L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "28e41f28-5c7a-4c3f-9f38-dd45393a2bbc", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 56, 18, 49, 161, 47, 24, 240, 238, 207, 163, 42, 191, 184, 31, 211, 55, 30, 187, 234, 76, 215, 34, 47, 255, 68, 64, 102, 208, 204, 38, 120, 16 }, false, "MOCKPASS", "SG", 2032L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20322L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000032J", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 51, 120, 123, 125, 162, 119, 118, 187, 163, 166, 226, 49, 180, 111, 97, 251, 185, 46, 209, 23, 55, 99, 140, 186, 124, 19, 170, 243, 18, 238, 96, 113 }, true, "MOCKPASS", "SG", 2032L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20331L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "717dac1c-c021-420b-b660-b94d041d7e67", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 255, 241, 249, 79, 98, 119, 220, 143, 226, 234, 211, 6, 204, 162, 149, 40, 158, 41, 14, 13, 215, 236, 252, 139, 193, 51, 14, 238, 23, 65, 178, 28 }, false, "MOCKPASS", "SG", 2033L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20332L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000033K", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 88, 55, 142, 59, 228, 171, 215, 207, 52, 41, 2, 130, 1, 44, 221, 180, 205, 3, 213, 34, 243, 218, 207, 47, 147, 37, 79, 24, 227, 246, 12, 197 }, true, "MOCKPASS", "SG", 2033L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20341L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "d5ce54ea-c2ab-4d29-bd4b-6905ffba67e3", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 37, 58, 222, 174, 228, 121, 200, 104, 177, 243, 75, 36, 197, 32, 239, 119, 67, 30, 214, 165, 63, 193, 75, 49, 26, 155, 32, 166, 91, 245, 144, 103 }, false, "MOCKPASS", "SG", 2034L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20342L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000034L", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 219, 132, 84, 168, 134, 4, 250, 142, 34, 253, 210, 220, 191, 145, 203, 246, 218, 248, 235, 157, 62, 30, 214, 210, 47, 82, 178, 32, 234, 164, 229, 239 }, true, "MOCKPASS", "SG", 2034L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20351L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "619e2335-7ff5-4884-abf7-929a2156f9b2", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 235, 241, 214, 197, 181, 102, 190, 240, 11, 29, 85, 19, 89, 133, 254, 56, 52, 80, 150, 132, 204, 167, 119, 183, 188, 14, 60, 27, 177, 20, 69, 193 }, false, "MOCKPASS", "SG", 2035L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20352L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000035M", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 205, 49, 118, 232, 182, 20, 188, 178, 89, 202, 118, 28, 106, 208, 176, 162, 59, 87, 90, 164, 21, 130, 159, 205, 162, 144, 145, 120, 114, 131, 109, 147 }, true, "MOCKPASS", "SG", 2035L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20361L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "76cd6639-f486-43b7-acf0-4ba34f72f961", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 219, 228, 8, 215, 151, 212, 161, 52, 135, 225, 62, 244, 180, 123, 247, 190, 150, 74, 35, 2, 65, 104, 23, 40, 181, 40, 141, 46, 52, 47, 215, 213 }, false, "MOCKPASS", "SG", 2036L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20362L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000036N", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 62, 48, 245, 124, 160, 117, 219, 127, 62, 233, 48, 186, 64, 80, 247, 21, 158, 36, 101, 129, 72, 120, 71, 218, 218, 209, 217, 232, 55, 114, 226, 10 }, true, "MOCKPASS", "SG", 2036L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20371L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "4c357bdb-2382-4105-8349-c30ef28a5c0d", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 132, 156, 48, 124, 49, 130, 67, 5, 97, 124, 142, 94, 115, 21, 79, 87, 251, 149, 73, 30, 124, 93, 97, 249, 233, 118, 142, 94, 164, 219, 204, 191 }, false, "MOCKPASS", "SG", 2037L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20372L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000037P", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 107, 163, 212, 119, 220, 97, 28, 250, 192, 117, 243, 194, 20, 15, 71, 100, 163, 193, 186, 93, 252, 55, 56, 246, 68, 82, 202, 179, 70, 224, 91, 164 }, true, "MOCKPASS", "SG", 2037L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20381L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "c2974885-5c5f-4df5-8515-4ecb840e90db", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 105, 163, 103, 233, 50, 88, 55, 132, 147, 223, 218, 56, 146, 40, 98, 222, 213, 178, 174, 84, 97, 65, 103, 206, 149, 173, 44, 221, 42, 66, 37, 190 }, false, "MOCKPASS", "SG", 2038L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20382L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000038Q", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 39, 87, 21, 9, 164, 0, 50, 62, 71, 16, 153, 81, 141, 32, 100, 227, 79, 125, 246, 8, 240, 235, 95, 108, 155, 186, 108, 80, 188, 29, 63, 167 }, true, "MOCKPASS", "SG", 2038L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20391L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "75a84c54-f406-42e0-96c7-769fdfe64c70", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 83, 238, 233, 87, 94, 0, 49, 154, 149, 173, 204, 214, 228, 109, 137, 182, 44, 126, 225, 9, 176, 118, 127, 232, 138, 100, 230, 46, 104, 150, 48, 38 }, false, "MOCKPASS", "SG", 2039L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20392L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000039R", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 228, 187, 50, 106, 11, 47, 185, 178, 252, 145, 103, 16, 60, 96, 114, 219, 118, 117, 42, 72, 163, 61, 146, 154, 179, 223, 123, 137, 123, 253, 246, 198 }, true, "MOCKPASS", "SG", 2039L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20401L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "8c751878-9c50-40ba-adba-e980605817a6", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 196, 252, 241, 62, 86, 51, 111, 174, 68, 138, 251, 160, 162, 143, 66, 201, 93, 123, 25, 115, 132, 129, 80, 184, 40, 193, 149, 171, 207, 241, 253, 54 }, false, "MOCKPASS", "SG", 2040L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20402L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000040T", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 140, 222, 133, 99, 28, 236, 214, 119, 168, 222, 211, 143, 15, 226, 144, 133, 79, 174, 164, 18, 103, 39, 96, 92, 15, 248, 95, 122, 108, 154, 133, 145 }, true, "MOCKPASS", "SG", 2040L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20411L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "91ad0a28-92a0-47ac-a29c-e5cc07835697", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 135, 199, 129, 71, 227, 110, 233, 242, 32, 184, 33, 101, 15, 192, 59, 232, 187, 174, 14, 7, 117, 30, 72, 225, 245, 68, 204, 7, 130, 1, 230, 83 }, false, "MOCKPASS", "SG", 2041L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20412L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000041U", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 23, 10, 86, 98, 34, 132, 220, 92, 20, 150, 78, 50, 137, 244, 143, 45, 37, 111, 90, 223, 36, 160, 226, 22, 190, 56, 71, 160, 45, 229, 137, 74 }, true, "MOCKPASS", "SG", 2041L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20421L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "a453341a-96e6-422f-889c-474ee17f5cec", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 109, 212, 14, 154, 62, 122, 191, 176, 213, 229, 44, 31, 69, 27, 19, 236, 163, 133, 88, 193, 15, 91, 158, 175, 101, 19, 105, 44, 40, 14, 178, 153 }, false, "MOCKPASS", "SG", 2042L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20422L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000042V", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 247, 38, 88, 212, 240, 195, 178, 1, 86, 114, 89, 7, 103, 41, 221, 143, 136, 202, 206, 222, 95, 206, 112, 95, 159, 16, 239, 199, 207, 255, 70, 93 }, true, "MOCKPASS", "SG", 2042L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20431L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "2125c836-da1d-4a2b-b26b-e22dd1b99b8a", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 202, 182, 226, 112, 199, 226, 190, 138, 24, 215, 104, 89, 231, 196, 168, 63, 32, 245, 81, 203, 252, 197, 19, 25, 44, 115, 29, 106, 166, 44, 19, 158 }, false, "MOCKPASS", "SG", 2043L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20432L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000043W", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 247, 97, 35, 59, 17, 90, 147, 118, 219, 16, 85, 65, 223, 76, 248, 100, 45, 83, 65, 202, 184, 91, 220, 80, 127, 174, 11, 120, 254, 157, 83, 49 }, true, "MOCKPASS", "SG", 2043L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20441L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "d5b8c485-4ab1-497b-a0bb-9feefcebde71", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 165, 215, 32, 54, 138, 212, 53, 191, 26, 17, 168, 66, 131, 52, 81, 6, 121, 168, 47, 250, 143, 227, 24, 118, 249, 250, 208, 179, 185, 209, 202, 235 }, false, "MOCKPASS", "SG", 2044L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20442L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000044X", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 75, 117, 101, 45, 147, 92, 243, 202, 161, 46, 14, 236, 179, 92, 6, 146, 186, 7, 157, 105, 239, 250, 38, 71, 245, 199, 78, 135, 187, 48, 5, 148 }, true, "MOCKPASS", "SG", 2044L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20451L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "8b49de77-611f-4897-bb44-e9ba90aa585d", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 169, 215, 104, 35, 24, 180, 176, 76, 223, 12, 154, 148, 170, 46, 30, 35, 26, 249, 162, 116, 206, 234, 130, 122, 156, 34, 27, 161, 57, 154, 175, 85 }, false, "MOCKPASS", "SG", 2045L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20452L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000045Y", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 230, 197, 75, 207, 230, 57, 24, 232, 126, 186, 134, 48, 192, 117, 13, 149, 118, 253, 8, 213, 78, 17, 20, 22, 22, 100, 182, 108, 178, 19, 87, 120 }, true, "MOCKPASS", "SG", 2045L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20461L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "61275942-e60f-4aa3-969b-a313e4c7ccc9", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 9, 236, 156, 59, 53, 130, 191, 66, 166, 37, 163, 213, 157, 68, 11, 12, 70, 102, 73, 90, 87, 180, 255, 11, 25, 79, 232, 230, 25, 184, 156, 247 }, false, "MOCKPASS", "SG", 2046L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20462L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000046Z", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 213, 38, 171, 34, 81, 83, 110, 232, 10, 228, 100, 237, 178, 27, 192, 116, 220, 95, 90, 21, 6, 67, 98, 182, 202, 213, 169, 212, 160, 87, 159, 248 }, true, "MOCKPASS", "SG", 2046L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20471L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "796dca6f-69e2-481f-9b0c-bbeb158d9d92", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 135, 36, 221, 179, 235, 126, 72, 15, 170, 28, 5, 246, 164, 227, 86, 59, 131, 167, 227, 184, 151, 67, 29, 201, 150, 253, 46, 127, 231, 213, 205, 230 }, false, "MOCKPASS", "SG", 2047L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20472L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000047A", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 209, 227, 255, 20, 149, 27, 156, 82, 123, 10, 184, 121, 140, 78, 75, 11, 182, 104, 114, 3, 78, 207, 11, 46, 249, 140, 254, 213, 136, 82, 94, 128 }, true, "MOCKPASS", "SG", 2047L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20481L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "6a3332d6-7959-41e2-8a16-633aeb039154", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 147, 183, 123, 133, 78, 237, 195, 43, 1, 205, 223, 100, 212, 201, 74, 78, 185, 43, 113, 14, 184, 108, 209, 49, 13, 99, 69, 221, 121, 239, 246, 126 }, false, "MOCKPASS", "SG", 2048L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20482L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000048B", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 1, 32, 152, 98, 133, 241, 240, 183, 105, 204, 52, 134, 25, 135, 46, 91, 134, 134, 100, 210, 232, 253, 190, 4, 21, 225, 180, 45, 27, 191, 75, 171 }, true, "MOCKPASS", "SG", 2048L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20491L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "50369a6f-b13e-43fd-9e3a-85c7a5d90cdd", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 49, 112, 155, 16, 79, 88, 122, 206, 214, 92, 178, 180, 33, 149, 175, 203, 203, 196, 27, 100, 160, 138, 238, 250, 101, 218, 243, 202, 242, 1, 162, 10 }, false, "MOCKPASS", "SG", 2049L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20492L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000049C", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 163, 33, 105, 246, 251, 247, 200, 104, 183, 215, 121, 84, 78, 36, 66, 104, 57, 242, 51, 16, 223, 100, 158, 126, 139, 81, 134, 132, 84, 104, 201, 61 }, true, "MOCKPASS", "SG", 2049L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20501L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "89f4c2eb-e067-4b33-8fb2-7c8ddd4a7af2", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 240, 138, 180, 75, 118, 111, 80, 160, 76, 112, 206, 38, 134, 132, 97, 42, 146, 55, 139, 189, 2, 15, 135, 102, 119, 14, 209, 220, 40, 157, 95, 201 }, false, "MOCKPASS", "SG", 2050L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20502L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S7000050D", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 66, 92, 206, 183, 184, 45, 213, 130, 27, 210, 249, 252, 28, 199, 166, 11, 72, 221, 186, 255, 217, 209, 135, 166, 107, 43, 189, 125, 53, 89, 57, 238 }, true, "MOCKPASS", "SG", 2050L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3001L,
                columns: new[] { "ClassCode", "LevelCode", "StudentNumber" },
                values: new object[] { "NUS-Y1-01", "UNI_Y1", "NUS-2026-001" });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3002L,
                columns: new[] { "ClassCode", "LevelCode", "StudentNumber" },
                values: new object[] { "NUS-Y2-02", "UNI_Y2", "NUS-2026-002" });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3003L,
                columns: new[] { "ClassCode", "LevelCode", "StudentNumber" },
                values: new object[] { "NUS-Y3-03", "UNI_Y3", "NUS-2026-003" });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3004L,
                columns: new[] { "ClassCode", "LevelCode", "StudentNumber" },
                values: new object[] { "NUS-Y4-01", "UNI_Y4", "NUS-2026-004" });

            migrationBuilder.InsertData(
                schema: "person",
                table: "SchoolEnrollment",
                columns: new[] { "SchoolEnrollmentId", "AcademicYear", "ClassCode", "CreatedAt", "EndDate", "LevelCode", "OrganizationId", "PersonId", "SchoolingStatusCode", "SourceCode", "StartDate", "StatusReasonCode", "StudentNumber", "UpdatedAt" },
                values: new object[,]
                {
                    { 3005L, "2026", "NUS-Y1-02", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y1", 2L, 2005L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "NUS-2026-005", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3006L, "2026", "NUS-Y2-03", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y2", 2L, 2006L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "NUS-2026-006", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3007L, "2026", "NUS-Y3-01", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y3", 2L, 2007L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "NUS-2026-007", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3008L, "2026", "NUS-Y4-02", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y4", 2L, 2008L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "NUS-2026-008", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3009L, "2026", "NUS-Y1-03", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y1", 2L, 2009L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "NUS-2026-009", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3010L, "2026", "NUS-Y2-01", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y2", 2L, 2010L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "NUS-2026-010", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3011L, "2026", "NTU-Y1-01", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y1", 3L, 2011L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "NTU-2026-001", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3012L, "2026", "NTU-Y2-02", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y2", 3L, 2012L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "NTU-2026-002", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3013L, "2026", "NTU-Y3-03", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y3", 3L, 2013L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "NTU-2026-003", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3014L, "2026", "NTU-Y4-01", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y4", 3L, 2014L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "NTU-2026-004", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3015L, "2026", "NTU-Y1-02", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y1", 3L, 2015L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "NTU-2026-005", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3016L, "2026", "NTU-Y2-03", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y2", 3L, 2016L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "NTU-2026-006", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3017L, "2026", "NTU-Y3-01", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y3", 3L, 2017L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "NTU-2026-007", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3018L, "2026", "NTU-Y4-02", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y4", 3L, 2018L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "NTU-2026-008", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3019L, "2026", "NTU-Y1-03", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y1", 3L, 2019L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "NTU-2026-009", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3020L, "2026", "NTU-Y2-01", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y2", 3L, 2020L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "NTU-2026-010", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3021L, "2026", "SMU-Y1-01", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y1", 4L, 2021L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SMU-2026-001", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3022L, "2026", "SMU-Y2-02", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y2", 4L, 2022L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SMU-2026-002", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3023L, "2026", "SMU-Y3-03", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y3", 4L, 2023L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SMU-2026-003", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3024L, "2026", "SMU-Y4-01", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y4", 4L, 2024L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SMU-2026-004", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3025L, "2026", "SMU-Y1-02", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y1", 4L, 2025L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SMU-2026-005", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3026L, "2026", "SMU-Y2-03", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y2", 4L, 2026L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SMU-2026-006", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3027L, "2026", "SMU-Y3-01", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y3", 4L, 2027L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SMU-2026-007", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3028L, "2026", "SMU-Y4-02", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y4", 4L, 2028L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SMU-2026-008", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3029L, "2026", "SMU-Y1-03", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y1", 4L, 2029L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SMU-2026-009", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3030L, "2026", "SMU-Y2-01", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y2", 4L, 2030L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SMU-2026-010", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3031L, "2026", "SUTD-Y1-01", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y1", 5L, 2031L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SUTD-2026-001", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3032L, "2026", "SUTD-Y2-02", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y2", 5L, 2032L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SUTD-2026-002", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3033L, "2026", "SUTD-Y3-03", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y3", 5L, 2033L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SUTD-2026-003", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3034L, "2026", "SUTD-Y4-01", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y4", 5L, 2034L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SUTD-2026-004", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3035L, "2026", "SUTD-Y1-02", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y1", 5L, 2035L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SUTD-2026-005", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3036L, "2026", "SUTD-Y2-03", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y2", 5L, 2036L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SUTD-2026-006", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3037L, "2026", "SUTD-Y3-01", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y3", 5L, 2037L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SUTD-2026-007", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3038L, "2026", "SUTD-Y4-02", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y4", 5L, 2038L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SUTD-2026-008", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3039L, "2026", "SUTD-Y1-03", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y1", 5L, 2039L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SUTD-2026-009", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3040L, "2026", "SUTD-Y2-01", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y2", 5L, 2040L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SUTD-2026-010", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3041L, "2026", "SIT-Y1-01", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y1", 6L, 2041L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SIT-2026-001", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3042L, "2026", "SIT-Y2-02", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y2", 6L, 2042L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SIT-2026-002", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3043L, "2026", "SIT-Y3-03", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y3", 6L, 2043L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SIT-2026-003", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3044L, "2026", "SIT-Y4-01", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y4", 6L, 2044L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SIT-2026-004", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3045L, "2026", "SIT-Y1-02", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y1", 6L, 2045L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SIT-2026-005", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3046L, "2026", "SIT-Y2-03", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y2", 6L, 2046L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SIT-2026-006", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3047L, "2026", "SIT-Y3-01", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y3", 6L, 2047L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SIT-2026-007", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3048L, "2026", "SIT-Y4-02", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y4", 6L, 2048L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SIT-2026-008", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3049L, "2026", "SIT-Y1-03", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y1", 6L, 2049L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SIT-2026-009", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3050L, "2026", "SIT-Y2-01", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "UNI_Y2", 6L, 2050L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "SIT-2026-010", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                schema: "iam",
                table: "UserAccessScope",
                columns: new[] { "UserAccessScopeId", "CreatedAtUtc", "CreatedByUserAccountId", "EffectiveFromUtc", "EffectiveToUtc", "OrganizationUnitId", "RoleCode", "StatusCode", "UserAccountId" },
                values: new object[,]
                {
                    { 1007L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2L, "STUDENT", "ACTIVE", 1007L },
                    { 1008L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2L, "STUDENT", "ACTIVE", 1008L },
                    { 1009L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2L, "STUDENT", "ACTIVE", 1009L },
                    { 1010L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2L, "STUDENT", "ACTIVE", 1010L },
                    { 1011L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2L, "STUDENT", "ACTIVE", 1011L },
                    { 1012L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2L, "STUDENT", "ACTIVE", 1012L },
                    { 1013L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3L, "STUDENT", "ACTIVE", 1013L },
                    { 1014L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3L, "STUDENT", "ACTIVE", 1014L },
                    { 1015L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3L, "STUDENT", "ACTIVE", 1015L },
                    { 1016L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3L, "STUDENT", "ACTIVE", 1016L },
                    { 1017L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3L, "STUDENT", "ACTIVE", 1017L },
                    { 1018L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3L, "STUDENT", "ACTIVE", 1018L },
                    { 1019L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3L, "STUDENT", "ACTIVE", 1019L },
                    { 1020L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3L, "STUDENT", "ACTIVE", 1020L },
                    { 1021L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3L, "STUDENT", "ACTIVE", 1021L },
                    { 1022L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3L, "STUDENT", "ACTIVE", 1022L },
                    { 1023L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4L, "STUDENT", "ACTIVE", 1023L },
                    { 1024L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4L, "STUDENT", "ACTIVE", 1024L },
                    { 1025L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4L, "STUDENT", "ACTIVE", 1025L },
                    { 1026L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4L, "STUDENT", "ACTIVE", 1026L },
                    { 1027L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4L, "STUDENT", "ACTIVE", 1027L },
                    { 1028L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4L, "STUDENT", "ACTIVE", 1028L },
                    { 1029L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4L, "STUDENT", "ACTIVE", 1029L },
                    { 1030L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4L, "STUDENT", "ACTIVE", 1030L },
                    { 1031L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4L, "STUDENT", "ACTIVE", 1031L },
                    { 1032L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4L, "STUDENT", "ACTIVE", 1032L },
                    { 1033L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 5L, "STUDENT", "ACTIVE", 1033L },
                    { 1034L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 5L, "STUDENT", "ACTIVE", 1034L },
                    { 1035L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 5L, "STUDENT", "ACTIVE", 1035L },
                    { 1036L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 5L, "STUDENT", "ACTIVE", 1036L },
                    { 1037L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 5L, "STUDENT", "ACTIVE", 1037L },
                    { 1038L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 5L, "STUDENT", "ACTIVE", 1038L },
                    { 1039L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 5L, "STUDENT", "ACTIVE", 1039L },
                    { 1040L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 5L, "STUDENT", "ACTIVE", 1040L },
                    { 1041L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 5L, "STUDENT", "ACTIVE", 1041L },
                    { 1042L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 5L, "STUDENT", "ACTIVE", 1042L },
                    { 1043L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 6L, "STUDENT", "ACTIVE", 1043L },
                    { 1044L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 6L, "STUDENT", "ACTIVE", 1044L },
                    { 1045L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 6L, "STUDENT", "ACTIVE", 1045L },
                    { 1046L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 6L, "STUDENT", "ACTIVE", 1046L },
                    { 1047L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 6L, "STUDENT", "ACTIVE", 1047L },
                    { 1048L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 6L, "STUDENT", "ACTIVE", 1048L },
                    { 1049L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 6L, "STUDENT", "ACTIVE", 1049L },
                    { 1050L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 6L, "STUDENT", "ACTIVE", 1050L },
                    { 1051L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 6L, "STUDENT", "ACTIVE", 1051L },
                    { 1052L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 6L, "STUDENT", "ACTIVE", 1052L }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4005L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4006L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4007L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4008L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4009L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4010L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4011L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4012L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4013L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4014L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4015L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4016L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4017L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4018L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4019L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4020L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4021L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4022L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4023L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4024L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4025L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4026L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4027L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4028L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4029L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4030L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4031L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4032L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4033L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4034L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4035L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4036L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4037L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4038L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4039L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4040L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4041L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4042L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4043L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4044L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4045L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4046L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4047L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4048L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4049L);

            migrationBuilder.DeleteData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4050L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1007L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1008L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1009L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1010L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1011L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1012L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1013L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1014L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1015L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1016L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1017L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1018L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1019L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1020L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1021L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1022L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1023L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1024L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1025L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1026L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1027L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1028L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1029L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1030L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1031L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1032L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1033L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1034L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1035L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1036L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1037L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1038L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1039L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1040L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1041L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1042L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1043L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1044L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1045L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1046L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1047L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1048L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1049L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1050L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1051L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1052L);

            migrationBuilder.DeleteData(
                schema: "org",
                table: "Organization",
                keyColumn: "OrganizationId",
                keyValue: 3L);

            migrationBuilder.DeleteData(
                schema: "org",
                table: "Organization",
                keyColumn: "OrganizationId",
                keyValue: 4L);

            migrationBuilder.DeleteData(
                schema: "org",
                table: "Organization",
                keyColumn: "OrganizationId",
                keyValue: 5L);

            migrationBuilder.DeleteData(
                schema: "org",
                table: "Organization",
                keyColumn: "OrganizationId",
                keyValue: 6L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2005L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2006L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2007L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2008L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2009L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2010L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2011L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2012L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2013L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2014L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2015L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2016L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2017L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2018L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2019L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2020L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2021L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2022L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2023L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2024L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2025L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2026L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2027L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2028L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2029L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2030L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2031L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2032L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2033L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2034L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2035L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2036L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2037L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2038L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2039L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2040L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2041L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2042L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2043L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2044L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2045L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2046L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2047L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2048L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2049L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2050L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20051L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20052L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20061L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20062L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20071L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20072L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20081L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20082L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20091L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20092L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20101L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20102L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20111L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20112L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20121L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20122L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20131L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20132L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20141L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20142L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20151L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20152L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20161L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20162L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20171L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20172L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20181L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20182L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20191L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20192L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20201L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20202L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20211L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20212L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20221L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20222L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20231L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20232L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20241L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20242L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20251L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20252L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20261L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20262L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20271L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20272L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20281L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20282L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20291L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20292L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20301L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20302L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20311L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20312L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20321L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20322L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20331L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20332L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20341L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20342L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20351L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20352L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20361L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20362L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20371L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20372L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20381L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20382L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20391L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20392L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20401L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20402L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20411L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20412L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20421L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20422L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20431L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20432L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20441L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20442L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20451L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20452L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20461L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20462L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20471L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20472L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20481L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20482L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20491L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20492L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20501L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20502L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3005L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3006L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3007L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3008L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3009L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3010L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3011L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3012L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3013L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3014L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3015L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3016L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3017L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3018L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3019L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3020L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3021L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3022L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3023L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3024L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3025L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3026L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3027L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3028L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3029L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3030L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3031L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3032L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3033L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3034L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3035L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3036L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3037L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3038L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3039L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3040L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3041L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3042L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3043L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3044L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3045L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3046L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3047L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3048L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3049L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3050L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1007L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1008L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1009L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1010L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1011L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1012L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1013L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1014L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1015L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1016L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1017L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1018L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1019L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1020L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1021L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1022L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1023L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1024L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1025L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1026L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1027L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1028L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1029L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1030L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1031L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1032L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1033L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1034L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1035L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1036L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1037L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1038L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1039L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1040L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1041L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1042L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1043L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1044L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1045L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1046L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1047L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1048L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1049L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1050L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1051L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1052L);

            migrationBuilder.UpdateData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4001L,
                column: "AccountNumber",
                value: "EA-DEMO-0001");

            migrationBuilder.UpdateData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4002L,
                column: "AccountNumber",
                value: "EA-DEMO-0002");

            migrationBuilder.UpdateData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4003L,
                column: "AccountNumber",
                value: "EA-DEMO-0003");

            migrationBuilder.UpdateData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4004L,
                column: "AccountNumber",
                value: "EA-DEMO-0004");

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1003L,
                columns: new[] { "ContactEmail", "ContactMobile", "DisplayNameSnapshot", "ExternalSubjectId", "ProviderDisplayName", "ProviderLoginName" },
                values: new object[] { "tan.mei.ling@student.example.test", "+6590000001", "Tan Mei Ling", "ef39a074-b64d-4990-a937-6f80772e2bb8", "Tan Mei Ling", "S1234567A" });

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1004L,
                columns: new[] { "ContactEmail", "ContactMobile", "ExternalSubjectId", "ProviderLoginName" },
                values: new object[] { "aisha.tan@student.example.test", "+6590000002", "a9865837-7bd7-46ac-bef4-42a76a946424", "S8979373D" });

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1005L,
                columns: new[] { "ContactEmail", "ContactMobile", "DisplayNameSnapshot", "ExternalSubjectId", "ProviderDisplayName", "ProviderLoginName" },
                values: new object[] { "benjamin.lee@student.example.test", "+6590000003", "Benjamin Lee", "f4b70aea-d639-4b79-b8d9-8ace5875f6b1", "Benjamin Lee", "S8116474F" });

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1006L,
                columns: new[] { "ContactEmail", "ContactMobile", "DisplayNameSnapshot", "ExternalSubjectId", "ProviderDisplayName", "ProviderLoginName" },
                values: new object[] { "chloe.fernandez@student.example.test", "+6590000004", "Chloe Fernandez", "2135fe5c-d07b-49d3-b960-aabb0ff2e05a", "Chloe Fernandez", "F9477325W" });

            migrationBuilder.InsertData(
                schema: "iam",
                table: "LoginAccount",
                columns: new[] { "LoginAccountId", "LoginStatusCode", "AdminOrganizationId", "ContactEmail", "ContactMobile", "CreatedAt", "CreatedByLoginAccountId", "DisplayNameSnapshot", "ExternalIssuer", "ExternalObjectId", "ExternalSubjectId", "ExternalTenantId", "FirstLoginAtUtc", "IdentityProviderCode", "LastLoginAt", "LastSyncedAt", "LoginEmailNormalized", "PersonId", "PortalAccessCode", "ProviderDisplayName", "ProviderEmail", "ProviderLoginName", "ProviderMobile", "RoleCode", "UpdatedAt", "UserTypeCode" },
                values: new object[] { 1002L, "ACTIVE", 2L, "school.admin@demo-school.local", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Demo School Admin", "https://sts.windows.net/ea71ddeb-596c-4034-84d4-d65f91edc14a/", "00000000-0000-0000-0000-000000000222", "00000000-0000-0000-0000-000000000222", "ea71ddeb-596c-4034-84d4-d65f91edc14a", null, "ENTRA_WORKFORCE", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SCHOOL.ADMIN@DEMO-SCHOOL.LOCAL", null, "ADMIN", "Demo School Admin", "school.admin@demo-school.local", "school.admin@demo-school.local", null, "SCHOOL_ADMIN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "INTERNAL" });

            migrationBuilder.UpdateData(
                schema: "org",
                table: "Organization",
                keyColumn: "OrganizationId",
                keyValue: 2L,
                columns: new[] { "MockPassSchoolCode", "OrganizationCode", "OrganizationName" },
                values: new object[] { "MOEDEMO", "DEMO_SCHOOL", "Demo Secondary School" });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2001L,
                columns: new[] { "DateOfBirth", "MockPassPersonId", "IdentityNumberMasked", "OfficialAddress", "OfficialEmail", "FullName", "OfficialMobile", "PreferredAddress", "PreferredEmail", "PreferredMobile" },
                values: new object[] { new DateOnly(2008, 5, 12), "ef39a074-b64d-4990-a937-6f80772e2bb8", "S1234567A", "1 Demo Street, Singapore 000001", "tan.mei.ling@student.example.test", "Tan Mei Ling", "+6590000001", "1 Demo Street, Singapore 000001", "tan.mei.ling@student.example.test", "+6590000001" });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2002L,
                columns: new[] { "DateOfBirth", "MockPassPersonId", "IdentityNumberMasked", "OfficialAddress", "OfficialEmail", "OfficialMobile", "PreferredAddress", "PreferredEmail", "PreferredMobile" },
                values: new object[] { new DateOnly(2007, 3, 18), "a9865837-7bd7-46ac-bef4-42a76a946424", "S8979373D", "2 Demo Street, Singapore 000002", "aisha.tan@student.example.test", "+6590000002", "2 Demo Street, Singapore 000002", "aisha.tan@student.example.test", "+6590000002" });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2003L,
                columns: new[] { "DateOfBirth", "MockPassPersonId", "IdentityNumberMasked", "OfficialAddress", "OfficialEmail", "FullName", "OfficialMobile", "PreferredAddress", "PreferredEmail", "PreferredMobile" },
                values: new object[] { new DateOnly(2006, 9, 24), "f4b70aea-d639-4b79-b8d9-8ace5875f6b1", "S8116474F", "3 Demo Street, Singapore 000003", "benjamin.lee@student.example.test", "Benjamin Lee", "+6590000003", "3 Demo Street, Singapore 000003", "benjamin.lee@student.example.test", "+6590000003" });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2004L,
                columns: new[] { "ResidencyStatusCode", "DateOfBirth", "MockPassPersonId", "IdentityNumberMasked", "NationalityCode", "OfficialAddress", "OfficialEmail", "FullName", "OfficialMobile", "PreferredAddress", "PreferredEmail", "PreferredMobile" },
                values: new object[] { "VALID_PASS_HOLDER", new DateOnly(2005, 11, 2), "2135fe5c-d07b-49d3-b960-aabb0ff2e05a", "F9477325W", "FOREIGN", "4 Demo Street, Singapore 000004", "chloe.fernandez@student.example.test", "Chloe Fernandez", "+6590000004", "4 Demo Street, Singapore 000004", "chloe.fernandez@student.example.test", "+6590000004" });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20011L,
                columns: new[] { "IdentifierMasked", "IdentifierValueHash" },
                values: new object[] { "ef39a074-b64d-4990-a937-6f80772e2bb8", new byte[] { 143, 78, 16, 67, 202, 168, 233, 100, 254, 230, 253, 112, 0, 85, 30, 152, 119, 0, 18, 97, 181, 94, 50, 189, 7, 61, 62, 10, 243, 123, 21, 28 } });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20012L,
                columns: new[] { "IdentifierMasked", "IdentifierValueHash" },
                values: new object[] { "S1234567A", new byte[] { 112, 242, 185, 91, 219, 40, 139, 55, 222, 102, 239, 5, 72, 249, 127, 18, 78, 132, 214, 143, 112, 14, 253, 216, 147, 247, 170, 72, 70, 42, 214, 249 } });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20021L,
                columns: new[] { "IdentifierMasked", "IdentifierValueHash" },
                values: new object[] { "a9865837-7bd7-46ac-bef4-42a76a946424", new byte[] { 178, 115, 253, 212, 104, 58, 115, 121, 164, 32, 96, 165, 222, 151, 65, 131, 45, 237, 43, 141, 197, 227, 105, 61, 134, 240, 234, 253, 99, 124, 53, 190 } });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20022L,
                columns: new[] { "IdentifierMasked", "IdentifierValueHash" },
                values: new object[] { "S8979373D", new byte[] { 217, 24, 1, 94, 198, 81, 188, 158, 167, 90, 220, 111, 212, 54, 133, 57, 127, 62, 107, 132, 56, 116, 244, 64, 69, 97, 172, 228, 224, 100, 248, 35 } });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20031L,
                columns: new[] { "IdentifierMasked", "IdentifierValueHash" },
                values: new object[] { "f4b70aea-d639-4b79-b8d9-8ace5875f6b1", new byte[] { 219, 200, 191, 149, 29, 39, 62, 132, 65, 86, 70, 146, 171, 30, 126, 81, 124, 200, 87, 241, 196, 118, 119, 63, 4, 213, 242, 145, 190, 118, 41, 253 } });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20032L,
                columns: new[] { "IdentifierMasked", "IdentifierValueHash" },
                values: new object[] { "S8116474F", new byte[] { 180, 97, 139, 217, 161, 240, 208, 65, 157, 240, 232, 254, 49, 90, 9, 62, 131, 128, 254, 98, 49, 88, 90, 164, 46, 193, 252, 83, 32, 194, 217, 24 } });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20041L,
                columns: new[] { "IdentifierMasked", "IdentifierValueHash" },
                values: new object[] { "2135fe5c-d07b-49d3-b960-aabb0ff2e05a", new byte[] { 9, 16, 202, 164, 104, 3, 223, 70, 146, 34, 54, 119, 97, 133, 219, 53, 21, 100, 112, 249, 106, 131, 227, 213, 116, 146, 194, 39, 223, 24, 237, 131 } });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20042L,
                columns: new[] { "IdentifierMasked", "IdentifierValueHash" },
                values: new object[] { "F9477325W", new byte[] { 118, 252, 211, 152, 161, 67, 193, 130, 176, 160, 137, 248, 216, 85, 126, 95, 161, 98, 160, 1, 222, 220, 199, 130, 139, 221, 25, 118, 62, 151, 81, 252 } });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3001L,
                columns: new[] { "ClassCode", "LevelCode", "StudentNumber" },
                values: new object[] { "4A", "SEC_4", "DEMO-STU-0001" });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3002L,
                columns: new[] { "ClassCode", "LevelCode", "StudentNumber" },
                values: new object[] { "5B", "SEC_5", "DEMO-STU-0002" });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3003L,
                columns: new[] { "ClassCode", "LevelCode", "StudentNumber" },
                values: new object[] { "IT1A", "ITE_Y1", "DEMO-STU-0003" });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3004L,
                columns: new[] { "ClassCode", "LevelCode", "StudentNumber" },
                values: new object[] { "P2C", "POLY_Y2", "DEMO-STU-0004" });

            migrationBuilder.InsertData(
                schema: "iam",
                table: "UserAccessScope",
                columns: new[] { "UserAccessScopeId", "CreatedAtUtc", "CreatedByUserAccountId", "EffectiveFromUtc", "EffectiveToUtc", "OrganizationUnitId", "RoleCode", "StatusCode", "UserAccountId" },
                values: new object[] { 1002L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2L, "SCHOOL_ADMIN", "ACTIVE", 1002L });
        }
    }
}
