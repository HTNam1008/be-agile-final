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
        migrationBuilder.Sql("""
            IF EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = N'IX_Course_OrganizationId_CourseCode_AcademicYear'
                  AND object_id = OBJECT_ID(N'[course].[Course]')
            )
            BEGIN
                DROP INDEX [IX_Course_OrganizationId_CourseCode_AcademicYear] ON [course].[Course];
            END
            """);

        migrationBuilder.Sql("""
            IF COL_LENGTH(N'course.Course', N'AcademicYear') IS NOT NULL
            BEGIN
                DECLARE @constraintName nvarchar(max);
                SELECT @constraintName = QUOTENAME([d].[name])
                FROM [sys].[default_constraints] [d]
                INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
                WHERE ([d].[parent_object_id] = OBJECT_ID(N'[course].[Course]') AND [c].[name] = N'AcademicYear');
                IF @constraintName IS NOT NULL EXEC(N'ALTER TABLE [course].[Course] DROP CONSTRAINT ' + @constraintName + ';');
                ALTER TABLE [course].[Course] DROP COLUMN [AcademicYear];
            END
            """);

        migrationBuilder.Sql("""
            IF COL_LENGTH(N'course.Course', N'CreatedAt') IS NOT NULL
            BEGIN
                DECLARE @constraintName nvarchar(max);
                SELECT @constraintName = QUOTENAME([d].[name])
                FROM [sys].[default_constraints] [d]
                INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
                WHERE ([d].[parent_object_id] = OBJECT_ID(N'[course].[Course]') AND [c].[name] = N'CreatedAt');
                IF @constraintName IS NOT NULL EXEC(N'ALTER TABLE [course].[Course] DROP CONSTRAINT ' + @constraintName + ';');
                ALTER TABLE [course].[Course] DROP COLUMN [CreatedAt];
            END
            """);

        migrationBuilder.Sql("""
            IF COL_LENGTH(N'course.Course', N'DisabledReason') IS NOT NULL
            BEGIN
                DECLARE @constraintName nvarchar(max);
                SELECT @constraintName = QUOTENAME([d].[name])
                FROM [sys].[default_constraints] [d]
                INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
                WHERE ([d].[parent_object_id] = OBJECT_ID(N'[course].[Course]') AND [c].[name] = N'DisabledReason');
                IF @constraintName IS NOT NULL EXEC(N'ALTER TABLE [course].[Course] DROP CONSTRAINT ' + @constraintName + ';');
                ALTER TABLE [course].[Course] DROP COLUMN [DisabledReason];
            END
            """);

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

        migrationBuilder.Sql("""
            IF OBJECT_ID(N'[course].[CourseMaterial]', N'U') IS NULL
            BEGIN
                CREATE TABLE [course].[CourseMaterial] (
                    [CourseMaterialId] bigint NOT NULL IDENTITY,
                    [CourseId] bigint NOT NULL,
                    [MaterialTitle] nvarchar(200) NOT NULL,
                    [MaterialDescription] nvarchar(1000) NULL,
                    [MaterialTypeCode] varchar(40) NOT NULL,
                    [FileName] nvarchar(260) NOT NULL,
                    [OriginalFileName] nvarchar(260) NOT NULL,
                    [FileExtension] varchar(20) NOT NULL,
                    [ContentType] nvarchar(100) NOT NULL,
                    [FileSizeBytes] bigint NOT NULL,
                    [StorageProviderCode] varchar(40) NOT NULL,
                    [StoragePath] nvarchar(600) NOT NULL,
                    [PublicUrl] nvarchar(1000) NULL,
                    [DisplayOrder] int NOT NULL,
                    [IsRequired] bit NOT NULL,
                    [IsActive] bit NOT NULL,
                    [UploadedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NULL,
                    [DeletedAt] datetime2 NULL,
                    CONSTRAINT [PK_CourseMaterial] PRIMARY KEY ([CourseMaterialId])
                );
            END

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_CourseMaterial_CourseId'
                  AND object_id = OBJECT_ID(N'[course].[CourseMaterial]')
            )
            BEGIN
                CREATE INDEX [IX_CourseMaterial_CourseId] ON [course].[CourseMaterial] ([CourseId]);
            END

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_CourseMaterial_CourseId_IsActive'
                  AND object_id = OBJECT_ID(N'[course].[CourseMaterial]')
            )
            BEGIN
                CREATE INDEX [IX_CourseMaterial_CourseId_IsActive] ON [course].[CourseMaterial] ([CourseId], [IsActive]);
            END

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_CourseMaterial_CourseId_StoragePath'
                  AND object_id = OBJECT_ID(N'[course].[CourseMaterial]')
            )
            BEGIN
                CREATE UNIQUE INDEX [IX_CourseMaterial_CourseId_StoragePath] ON [course].[CourseMaterial] ([CourseId], [StoragePath]);
            END
            """);
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
