using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

/// <inheritdoc />
public partial class SyncPendingModelChanges : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Course_OrganizationId_CourseCode_AcademicYear",
            schema: "course",
            table: "Course");

        migrationBuilder.DropColumn(
            name: "AcademicYear",
            schema: "course",
            table: "Course");

        migrationBuilder.DropColumn(
            name: "CreatedAt",
            schema: "course",
            table: "Course");

        migrationBuilder.DropColumn(
            name: "DisabledReason",
            schema: "course",
            table: "Course");

        migrationBuilder.AlterColumn<string>(
            name: "TransactionStatusCode",
            schema: "topup",
            table: "TopUpTransaction",
            type: "varchar(30)",
            unicode: false,
            maxLength: 30,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(20)",
            oldUnicode: false,
            oldMaxLength: 20);

        migrationBuilder.AlterColumn<string>(
            name: "Reason",
            schema: "topup",
            table: "TopUpTransaction",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(500)",
            oldMaxLength: 500,
            oldNullable: true);

        migrationBuilder.AlterColumn<long>(
            name: "EnrolledByLoginAccountId",
            schema: "course",
            table: "CourseEnrollment",
            type: "bigint",
            nullable: false,
            defaultValue: 0L,
            oldClrType: typeof(long),
            oldType: "bigint",
            oldNullable: true);

        migrationBuilder.AlterColumn<long>(
            name: "UpdatedByLoginAccountId",
            schema: "course",
            table: "Course",
            type: "bigint",
            nullable: false,
            defaultValue: 0L,
            oldClrType: typeof(long),
            oldType: "bigint",
            oldNullable: true);

        migrationBuilder.AlterColumn<DateTime>(
            name: "UpdatedAt",
            schema: "course",
            table: "Course",
            type: "datetime2",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
            oldClrType: typeof(DateTime),
            oldType: "datetime2",
            oldNullable: true);

        migrationBuilder.AlterColumn<DateTime>(
            name: "EnrollmentOpenAt",
            schema: "course",
            table: "Course",
            type: "datetime2",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
            oldClrType: typeof(DateTime),
            oldType: "datetime2",
            oldNullable: true);

        migrationBuilder.AlterColumn<DateTime>(
            name: "EnrollmentCloseAt",
            schema: "course",
            table: "Course",
            type: "datetime2",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
            oldClrType: typeof(DateTime),
            oldType: "datetime2",
            oldNullable: true);

        migrationBuilder.AlterColumn<DateOnly>(
            name: "EndDate",
            schema: "course",
            table: "Course",
            type: "date",
            nullable: false,
            defaultValue: new DateOnly(1, 1, 1),
            oldClrType: typeof(DateOnly),
            oldType: "date",
            oldNullable: true);

        migrationBuilder.CreateTable(
            name: "CourseMaterial",
            schema: "course",
            columns: table => new
            {
                CourseMaterialId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                CourseId = table.Column<long>(type: "bigint", nullable: false),
                MaterialTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                MaterialDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                MaterialTypeCode = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                FileExtension = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                StorageProviderCode = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                StoragePath = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                PublicUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                DisplayOrder = table.Column<int>(type: "int", nullable: false),
                IsRequired = table.Column<bool>(type: "bit", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CourseMaterial", x => x.CourseMaterialId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CourseMaterial_CourseId",
            schema: "course",
            table: "CourseMaterial",
            column: "CourseId");

        migrationBuilder.CreateIndex(
            name: "IX_CourseMaterial_CourseId_IsActive",
            schema: "course",
            table: "CourseMaterial",
            columns: new[] { "CourseId", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_CourseMaterial_CourseId_StoragePath",
            schema: "course",
            table: "CourseMaterial",
            columns: new[] { "CourseId", "StoragePath" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "CourseMaterial",
            schema: "course");

        migrationBuilder.AlterColumn<string>(
            name: "TransactionStatusCode",
            schema: "topup",
            table: "TopUpTransaction",
            type: "varchar(20)",
            unicode: false,
            maxLength: 20,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(30)",
            oldUnicode: false,
            oldMaxLength: 30);

        migrationBuilder.AlterColumn<string>(
            name: "Reason",
            schema: "topup",
            table: "TopUpTransaction",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(1000)",
            oldMaxLength: 1000,
            oldNullable: true);

        migrationBuilder.AlterColumn<long>(
            name: "EnrolledByLoginAccountId",
            schema: "course",
            table: "CourseEnrollment",
            type: "bigint",
            nullable: true,
            oldClrType: typeof(long),
            oldType: "bigint");

        migrationBuilder.AlterColumn<long>(
            name: "UpdatedByLoginAccountId",
            schema: "course",
            table: "Course",
            type: "bigint",
            nullable: true,
            oldClrType: typeof(long),
            oldType: "bigint");

        migrationBuilder.AlterColumn<DateTime>(
            name: "UpdatedAt",
            schema: "course",
            table: "Course",
            type: "datetime2",
            nullable: true,
            oldClrType: typeof(DateTime),
            oldType: "datetime2");

        migrationBuilder.AlterColumn<DateTime>(
            name: "EnrollmentOpenAt",
            schema: "course",
            table: "Course",
            type: "datetime2",
            nullable: true,
            oldClrType: typeof(DateTime),
            oldType: "datetime2");

        migrationBuilder.AlterColumn<DateTime>(
            name: "EnrollmentCloseAt",
            schema: "course",
            table: "Course",
            type: "datetime2",
            nullable: true,
            oldClrType: typeof(DateTime),
            oldType: "datetime2");

        migrationBuilder.AlterColumn<DateOnly>(
            name: "EndDate",
            schema: "course",
            table: "Course",
            type: "date",
            nullable: true,
            oldClrType: typeof(DateOnly),
            oldType: "date");

        migrationBuilder.AddColumn<string>(
            name: "AcademicYear",
            schema: "course",
            table: "Course",
            type: "varchar(20)",
            unicode: false,
            maxLength: 20,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            schema: "course",
            table: "Course",
            type: "datetime2",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

        migrationBuilder.AddColumn<string>(
            name: "DisabledReason",
            schema: "course",
            table: "Course",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Course_OrganizationId_CourseCode_AcademicYear",
            schema: "course",
            table: "Course",
            columns: new[] { "OrganizationId", "CourseCode", "AcademicYear" },
            unique: true);
    }
}
