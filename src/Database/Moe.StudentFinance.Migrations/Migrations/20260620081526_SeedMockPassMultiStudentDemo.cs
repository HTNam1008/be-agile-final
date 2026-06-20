using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class SeedMockPassMultiStudentDemo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4002L,
                column: "CurrentBalance",
                value: 0m);

            migrationBuilder.UpdateData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4003L,
                column: "CurrentBalance",
                value: 0m);

            migrationBuilder.UpdateData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4004L,
                columns: new[] { "CurrentBalance", "AccountStatusCode" },
                values: new object[] { 0m, "ACTIVE" });

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1003L,
                column: "ContactEmail",
                value: "tan.mei.ling@student.example.test");

            migrationBuilder.InsertData(
                schema: "iam",
                table: "LoginAccount",
                columns: new[] { "LoginAccountId", "LoginStatusCode", "AdminOrganizationId", "ContactEmail", "ContactMobile", "CreatedAt", "CreatedByLoginAccountId", "DisplayNameSnapshot", "ExternalIssuer", "ExternalObjectId", "ExternalSubjectId", "ExternalTenantId", "FirstLoginAtUtc", "IdentityProviderCode", "LastLoginAt", "LastSyncedAt", "LoginEmailNormalized", "PersonId", "PortalAccessCode", "ProviderDisplayName", "ProviderEmail", "ProviderLoginName", "ProviderMobile", "RoleCode", "UpdatedAt", "UserTypeCode" },
                values: new object[,]
                {
                    { 1004L, "ACTIVE", null, "aisha.tan@student.example.test", "+6590000002", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Aisha Tan", "http://localhost:5156/singpass/v3/fapi", null, "a9865837-7bd7-46ac-bef4-42a76a946424", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2002L, "ESERVICE", "Aisha Tan", null, "S8979373D", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1005L, "ACTIVE", null, "benjamin.lee@student.example.test", "+6590000003", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Benjamin Lee", "http://localhost:5156/singpass/v3/fapi", null, "f4b70aea-d639-4b79-b8d9-8ace5875f6b1", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2003L, "ESERVICE", "Benjamin Lee", null, "S8116474F", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" },
                    { 1006L, "ACTIVE", null, "chloe.fernandez@student.example.test", "+6590000004", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Chloe Fernandez", "http://localhost:5156/singpass/v3/fapi", null, "2135fe5c-d07b-49d3-b960-aabb0ff2e05a", null, null, "SINGPASS", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2004L, "ESERVICE", "Chloe Fernandez", null, "F9477325W", null, "STUDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESERVICE" }
                });

            migrationBuilder.UpdateData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2001L,
                columns: new[] { "OfficialEmail", "PreferredEmail" },
                values: new object[] { "tan.mei.ling@student.example.test", "tan.mei.ling@student.example.test" });

            migrationBuilder.InsertData(
                schema: "person",
                table: "Person",
                columns: new[] { "PersonId", "ResidencyStatusCode", "CreatedAt", "DateOfBirth", "MockPassPersonId", "IdentityNumberMasked", "NationalityCode", "OfficialAddress", "OfficialEmail", "FullName", "OfficialMobile", "PersonStatusCode", "PreferredAddress", "PreferredEmail", "PreferredMobile", "SourceUpdatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 2002L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2007, 3, 18), "a9865837-7bd7-46ac-bef4-42a76a946424", "S8979373D", "SG", "2 Demo Street, Singapore 000002", "aisha.tan@student.example.test", "Aisha Tan", "+6590000002", "ACTIVE", "2 Demo Street, Singapore 000002", "aisha.tan@student.example.test", "+6590000002", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2003L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2006, 9, 24), "f4b70aea-d639-4b79-b8d9-8ace5875f6b1", "S8116474F", "SG", "3 Demo Street, Singapore 000003", "benjamin.lee@student.example.test", "Benjamin Lee", "+6590000003", "ACTIVE", "3 Demo Street, Singapore 000003", "benjamin.lee@student.example.test", "+6590000003", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2004L, "VALID_PASS_HOLDER", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2005, 11, 2), "2135fe5c-d07b-49d3-b960-aabb0ff2e05a", "F9477325W", "FOREIGN", "4 Demo Street, Singapore 000004", "chloe.fernandez@student.example.test", "Chloe Fernandez", "+6590000004", "ACTIVE", "4 Demo Street, Singapore 000004", "chloe.fernandez@student.example.test", "+6590000004", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                schema: "person",
                table: "PersonIdentifier",
                columns: new[] { "PersonIdentifierId", "CreatedAtUtc", "EffectiveFrom", "EffectiveTo", "IdentifierMasked", "IdentifierStatusCode", "IdentifierTypeCode", "IdentifierValueEncrypted", "IdentifierValueHash", "IsPrimary", "IssuedByAuthority", "IssuingCountryCode", "PersonId", "SourceSystemCode", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { 20011L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "ef39a074-b64d-4990-a937-6f80772e2bb8", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 143, 78, 16, 67, 202, 168, 233, 100, 254, 230, 253, 112, 0, 85, 30, 152, 119, 0, 18, 97, 181, 94, 50, 189, 7, 61, 62, 10, 243, 123, 21, 28 }, false, "MOCKPASS", "SG", 2001L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20012L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S1234567A", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 112, 242, 185, 91, 219, 40, 139, 55, 222, 102, 239, 5, 72, 249, 127, 18, 78, 132, 214, 143, 112, 14, 253, 216, 147, 247, 170, 72, 70, 42, 214, 249 }, true, "MOCKPASS", "SG", 2001L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20021L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "a9865837-7bd7-46ac-bef4-42a76a946424", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 178, 115, 253, 212, 104, 58, 115, 121, 164, 32, 96, 165, 222, 151, 65, 131, 45, 237, 43, 141, 197, 227, 105, 61, 134, 240, 234, 253, 99, 124, 53, 190 }, false, "MOCKPASS", "SG", 2002L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20022L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S8979373D", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 217, 24, 1, 94, 198, 81, 188, 158, 167, 90, 220, 111, 212, 54, 133, 57, 127, 62, 107, 132, 56, 116, 244, 64, 69, 97, 172, 228, 224, 100, 248, 35 }, true, "MOCKPASS", "SG", 2002L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20031L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "f4b70aea-d639-4b79-b8d9-8ace5875f6b1", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 219, 200, 191, 149, 29, 39, 62, 132, 65, 86, 70, 146, 171, 30, 126, 81, 124, 200, 87, 241, 196, 118, 119, 63, 4, 213, 242, 145, 190, 118, 41, 253 }, false, "MOCKPASS", "SG", 2003L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20032L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "S8116474F", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 180, 97, 139, 217, 161, 240, 208, 65, 157, 240, 232, 254, 49, 90, 9, 62, 131, 128, 254, 98, 49, 88, 90, 164, 46, 193, 252, 83, 32, 194, 217, 24 }, true, "MOCKPASS", "SG", 2003L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20041L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "2135fe5c-d07b-49d3-b960-aabb0ff2e05a", "ACTIVE", "SINGPASS_SUBJECT", null, new byte[] { 9, 16, 202, 164, 104, 3, 223, 70, 146, 34, 54, 119, 97, 133, 219, 53, 21, 100, 112, 249, 106, 131, 227, 213, 116, 146, 194, 39, 223, 24, 237, 131 }, false, "MOCKPASS", "SG", 2004L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20042L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), null, "F9477325W", "ACTIVE", "IDENTITY_NUMBER", null, new byte[] { 118, 252, 211, 152, 161, 67, 193, 130, 176, 160, 137, 248, 216, 85, 126, 95, 161, 98, 160, 1, 222, 220, 199, 130, 139, 221, 25, 118, 62, 151, 81, 252 }, true, "MOCKPASS", "SG", 2004L, "MOCKPASS_DEMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                schema: "person",
                table: "SchoolEnrollment",
                columns: new[] { "SchoolEnrollmentId", "AcademicYear", "ClassCode", "CreatedAt", "EndDate", "LevelCode", "OrganizationId", "PersonId", "SchoolingStatusCode", "SourceCode", "StartDate", "StatusReasonCode", "StudentNumber", "UpdatedAt" },
                values: new object[,]
                {
                    { 3002L, "2026", "5B", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "SEC_5", 2L, 2002L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "DEMO-STU-0002", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3003L, "2026", "IT1A", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "ITE_Y1", 2L, 2003L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "DEMO-STU-0003", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3004L, "2026", "P2C", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "POLY_Y2", 2L, 2004L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "DEMO-STU-0004", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                schema: "iam",
                table: "UserAccessScope",
                columns: new[] { "UserAccessScopeId", "CreatedAtUtc", "CreatedByUserAccountId", "EffectiveFromUtc", "EffectiveToUtc", "OrganizationUnitId", "RoleCode", "StatusCode", "UserAccountId" },
                values: new object[,]
                {
                    { 1004L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2L, "STUDENT", "ACTIVE", 1004L },
                    { 1005L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2L, "STUDENT", "ACTIVE", 1005L },
                    { 1006L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2L, "STUDENT", "ACTIVE", 1006L }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1004L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1005L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1006L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2002L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2003L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2004L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20011L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20012L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20021L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20022L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20031L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20032L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20041L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "PersonIdentifier",
                keyColumn: "PersonIdentifierId",
                keyValue: 20042L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3002L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3003L);

            migrationBuilder.DeleteData(
                schema: "person",
                table: "SchoolEnrollment",
                keyColumn: "SchoolEnrollmentId",
                keyValue: 3004L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1004L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1005L);

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserAccessScope",
                keyColumn: "UserAccessScopeId",
                keyValue: 1006L);

            migrationBuilder.UpdateData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4002L,
                column: "CurrentBalance",
                value: 125.50m);

            migrationBuilder.UpdateData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4003L,
                column: "CurrentBalance",
                value: 480m);

            migrationBuilder.UpdateData(
                schema: "account",
                table: "EducationAccount",
                keyColumn: "EducationAccountId",
                keyValue: 4004L,
                columns: new[] { "CurrentBalance", "AccountStatusCode" },
                values: new object[] { 35.75m, "CLOSING" });

            migrationBuilder.UpdateData(
                schema: "iam",
                table: "LoginAccount",
                keyColumn: "LoginAccountId",
                keyValue: 1003L,
                column: "ContactEmail",
                value: "student@example.test");

            migrationBuilder.UpdateData(
                schema: "person",
                table: "Person",
                keyColumn: "PersonId",
                keyValue: 2001L,
                columns: new[] { "OfficialEmail", "PreferredEmail" },
                values: new object[] { "student.official@example.test", "student@example.test" });
        }
    }
}
