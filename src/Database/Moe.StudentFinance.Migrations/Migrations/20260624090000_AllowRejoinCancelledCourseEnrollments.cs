using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

[Migration("20260624090000_AllowRejoinCancelledCourseEnrollments")]
public partial class AllowRejoinCancelledCourseEnrollments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CourseEnrollment_PersonId_CourseId",
            schema: "course",
            table: "CourseEnrollment");

        migrationBuilder.CreateIndex(
            name: "IX_CourseEnrollment_PersonId_CourseId",
            schema: "course",
            table: "CourseEnrollment",
            columns: ["PersonId", "CourseId"],
            unique: true,
            filter: "[EnrollmentStatusCode] <> 'CANCELLED' AND [EnrollmentStatusCode] <> 'REFUNDED' AND [EnrollmentStatusCode] <> 'EXITED'");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CourseEnrollment_PersonId_CourseId",
            schema: "course",
            table: "CourseEnrollment");

        migrationBuilder.CreateIndex(
            name: "IX_CourseEnrollment_PersonId_CourseId",
            schema: "course",
            table: "CourseEnrollment",
            columns: ["PersonId", "CourseId"],
            unique: true);
    }
}
