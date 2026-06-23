using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseRefundPolicySnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AfterStartRefundPercentage",
                schema: "course",
                table: "CourseEnrollment",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 50m);

            migrationBuilder.AddColumn<decimal>(
                name: "BeforeStartRefundPercentage",
                schema: "course",
                table: "CourseEnrollment",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 100m);

            migrationBuilder.AddColumn<decimal>(
                name: "AfterStartRefundPercentage",
                schema: "course",
                table: "Course",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 50m);

            migrationBuilder.AddColumn<decimal>(
                name: "BeforeStartRefundPercentage",
                schema: "course",
                table: "Course",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 100m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AfterStartRefundPercentage",
                schema: "course",
                table: "CourseEnrollment");

            migrationBuilder.DropColumn(
                name: "BeforeStartRefundPercentage",
                schema: "course",
                table: "CourseEnrollment");

            migrationBuilder.DropColumn(
                name: "AfterStartRefundPercentage",
                schema: "course",
                table: "Course");

            migrationBuilder.DropColumn(
                name: "BeforeStartRefundPercentage",
                schema: "course",
                table: "Course");
        }
    }
}
