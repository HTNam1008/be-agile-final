using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddToTalSkippedAndNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "person",
                table: "Person",
                columns: new[] { "PersonId", "ResidencyStatusCode", "CreatedAt", "DateOfBirth", "MockPassPersonId", "IdentityNumberMasked", "NationalityCode", "OfficialAddress", "OfficialEmail", "FullName", "OfficialMobile", "PersonStatusCode", "PreferredAddress", "PreferredEmail", "PreferredMobile", "SourceUpdatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 2002L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2009, 3, 18), "TOPUP-STUDENT-0002", "S234****B", "SG", "1 Demo Street, Singapore 000001", "aisha.official@example.test", "Aisha Rahman", "+6590000002", "ACTIVE", "1 Demo Street, Singapore 000001", "aisha@example.test", "+6590000002", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2003L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2010, 9, 4), "TOPUP-STUDENT-0003", "S345****C", "SG", "1 Demo Street, Singapore 000001", "brandon.official@example.test", "Brandon Lee", "+6590000003", "ACTIVE", "1 Demo Street, Singapore 000001", "brandon@example.test", "+6590000003", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2004L, "CITIZEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2008, 11, 28), "TOPUP-STUDENT-0004", "S456****D", "SG", "1 Demo Street, Singapore 000001", "weijie.official@example.test", "Chen Wei Jie", "+6590000004", "ACTIVE", "1 Demo Street, Singapore 000001", "weijie@example.test", "+6590000004", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                schema: "person",
                table: "SchoolEnrollment",
                columns: new[] { "SchoolEnrollmentId", "AcademicYear", "ClassCode", "CreatedAt", "EndDate", "LevelCode", "OrganizationId", "PersonId", "SchoolingStatusCode", "SourceCode", "StartDate", "StatusReasonCode", "StudentNumber", "UpdatedAt" },
                values: new object[,]
                {
                    { 3002L, "2026", "3B", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "SEC_3", 2L, 2002L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "DEMO-STU-0002", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3003L, "2026", "2C", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "SEC_2", 2L, 2003L, "ACTIVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "DEMO-STU-0003", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3004L, "2026", "4A", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "SEC_4", 2L, 2004L, "ON_LEAVE", "DEMO_SEED", new DateOnly(2026, 1, 2), null, "DEMO-STU-0004", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                schema: "iam",
                table: "UserAccessScope",
                columns: new[] { "UserAccessScopeId", "CreatedAtUtc", "CreatedByUserAccountId", "EffectiveFromUtc", "EffectiveToUtc", "OrganizationUnitId", "RoleCode", "StatusCode", "UserAccountId" },
                values: new object[] { 1004L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2L, "SYSTEM_ADMIN", "ACTIVE", 1001L });
        }
    }
}
