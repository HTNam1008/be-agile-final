using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class FullRefactoredGovernmentTopUpSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserAccount",
                schema: "iam",
                table: "UserAccount");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OrganizationUnit",
                schema: "iam",
                table: "OrganizationUnit");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrganizationUnit_Parent_NotSelf",
                schema: "iam",
                table: "OrganizationUnit");

            migrationBuilder.DropColumn(
                name: "ClosingReasonCode",
                schema: "account",
                table: "EducationAccount");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                schema: "account",
                table: "EducationAccount");

            migrationBuilder.DropColumn(
                name: "OpeningReasonCode",
                schema: "account",
                table: "EducationAccount");

            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "billing");

            migrationBuilder.EnsureSchema(
                name: "course");

            migrationBuilder.EnsureSchema(
                name: "fas");

            migrationBuilder.EnsureSchema(
                name: "communication");

            migrationBuilder.EnsureSchema(
                name: "org");

            migrationBuilder.EnsureSchema(
                name: "payment");

            migrationBuilder.EnsureSchema(
                name: "topup");

            migrationBuilder.RenameTable(
                name: "UserAccount",
                schema: "iam",
                newName: "LoginAccount",
                newSchema: "iam");

            migrationBuilder.RenameTable(
                name: "OrganizationUnit",
                schema: "iam",
                newName: "Organization",
                newSchema: "org");

            migrationBuilder.RenameColumn(
                name: "OfficialFullName",
                schema: "person",
                table: "Person",
                newName: "FullName");

            migrationBuilder.RenameColumn(
                name: "ExternalPersonReference",
                schema: "person",
                table: "Person",
                newName: "MockPassPersonId");

            migrationBuilder.RenameColumn(
                name: "CitizenshipStatusCode",
                schema: "person",
                table: "Person",
                newName: "ResidencyStatusCode");

            migrationBuilder.RenameIndex(
                name: "IX_Person_ExternalPersonReference",
                schema: "person",
                table: "Person",
                newName: "IX_Person_MockPassPersonId");

            migrationBuilder.RenameColumn(
                name: "StatusCode",
                schema: "account",
                table: "EducationAccount",
                newName: "AccountStatusCode");

            migrationBuilder.RenameColumn(
                name: "OpeningRemarks",
                schema: "account",
                table: "EducationAccount",
                newName: "OpeningReason");

            migrationBuilder.RenameColumn(
                name: "OpeningModeCode",
                schema: "account",
                table: "EducationAccount",
                newName: "OpeningTypeCode");

            migrationBuilder.RenameColumn(
                name: "OpenedByUserId",
                schema: "account",
                table: "EducationAccount",
                newName: "OpenedByLoginAccountId");

            migrationBuilder.RenameColumn(
                name: "OpenedAtUtc",
                schema: "account",
                table: "EducationAccount",
                newName: "OpenedAt");

            migrationBuilder.RenameColumn(
                name: "ClosingRemarks",
                schema: "account",
                table: "EducationAccount",
                newName: "ClosingReason");

            migrationBuilder.RenameColumn(
                name: "ClosedAtUtc",
                schema: "account",
                table: "EducationAccount",
                newName: "ClosedAt");

            migrationBuilder.RenameColumn(
                name: "CachedBalance",
                schema: "account",
                table: "EducationAccount",
                newName: "CurrentBalance");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                schema: "iam",
                table: "LoginAccount",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "LastLoginAtUtc",
                schema: "iam",
                table: "LoginAccount",
                newName: "LastLoginAt");

            migrationBuilder.RenameColumn(
                name: "CreatedByUserAccountId",
                schema: "iam",
                table: "LoginAccount",
                newName: "CreatedByLoginAccountId");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                schema: "iam",
                table: "LoginAccount",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "AccountStatusCode",
                schema: "iam",
                table: "LoginAccount",
                newName: "LoginStatusCode");

            migrationBuilder.RenameColumn(
                name: "UserAccountId",
                schema: "iam",
                table: "LoginAccount",
                newName: "LoginAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_UserAccount_PersonId",
                schema: "iam",
                table: "LoginAccount",
                newName: "IX_LoginAccount_PersonId");

            migrationBuilder.RenameIndex(
                name: "IX_UserAccount_LoginEmailNormalized",
                schema: "iam",
                table: "LoginAccount",
                newName: "IX_LoginAccount_LoginEmailNormalized");

            migrationBuilder.RenameIndex(
                name: "IX_UserAccount_IdentityProviderCode_ExternalTenantId_ExternalObjectId",
                schema: "iam",
                table: "LoginAccount",
                newName: "IX_LoginAccount_IdentityProviderCode_ExternalTenantId_ExternalObjectId");

            migrationBuilder.RenameIndex(
                name: "IX_UserAccount_IdentityProviderCode_ExternalIssuer_ExternalSubjectId",
                schema: "iam",
                table: "LoginAccount",
                newName: "IX_LoginAccount_IdentityProviderCode_ExternalIssuer_ExternalSubjectId");

            migrationBuilder.RenameColumn(
                name: "UnitTypeCode",
                schema: "org",
                table: "Organization",
                newName: "OrganizationTypeCode");

            migrationBuilder.RenameColumn(
                name: "UnitName",
                schema: "org",
                table: "Organization",
                newName: "OrganizationName");

            migrationBuilder.RenameColumn(
                name: "UnitCode",
                schema: "org",
                table: "Organization",
                newName: "OrganizationCode");

            migrationBuilder.RenameColumn(
                name: "StatusCode",
                schema: "org",
                table: "Organization",
                newName: "OrganizationStatusCode");

            migrationBuilder.RenameColumn(
                name: "ParentOrganizationUnitId",
                schema: "org",
                table: "Organization",
                newName: "ParentOrganizationId");

            migrationBuilder.RenameColumn(
                name: "OrganizationUnitId",
                schema: "org",
                table: "Organization",
                newName: "OrganizationId");

            migrationBuilder.RenameIndex(
                name: "IX_OrganizationUnit_UnitCode",
                schema: "org",
                table: "Organization",
                newName: "IX_Organization_OrganizationCode");

            migrationBuilder.AlterColumn<string>(
                name: "PersonStatusCode",
                schema: "person",
                table: "Person",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "NationalityCode",
                schema: "person",
                table: "Person",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "ResidencyStatusCode",
                schema: "person",
                table: "Person",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "person",
                table: "Person",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "IdentityNumberMasked",
                schema: "person",
                table: "Person",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficialAddress",
                schema: "person",
                table: "Person",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficialEmail",
                schema: "person",
                table: "Person",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficialMobile",
                schema: "person",
                table: "Person",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredAddress",
                schema: "person",
                table: "Person",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredEmail",
                schema: "person",
                table: "Person",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredMobile",
                schema: "person",
                table: "Person",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceUpdatedAt",
                schema: "person",
                table: "Person",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "person",
                table: "Person",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "AccountStatusCode",
                schema: "account",
                table: "EducationAccount",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "OpeningTypeCode",
                schema: "account",
                table: "EducationAccount",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentBalance",
                schema: "account",
                table: "EducationAccount",
                type: "decimal(19,2)",
                precision: 19,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4);

            migrationBuilder.AddColumn<long>(
                name: "ClosedByLoginAccountId",
                schema: "account",
                table: "EducationAccount",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosingTypeCode",
                schema: "account",
                table: "EducationAccount",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ClosureExceptionApprovedByLoginAccountId",
                schema: "account",
                table: "EducationAccount",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosureExceptionReason",
                schema: "account",
                table: "EducationAccount",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ClosureExceptionUntil",
                schema: "account",
                table: "EducationAccount",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PendingClosureAt",
                schema: "account",
                table: "EducationAccount",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AdminOrganizationId",
                schema: "iam",
                table: "LoginAccount",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                schema: "iam",
                table: "LoginAccount",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactMobile",
                schema: "iam",
                table: "LoginAccount",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedAt",
                schema: "iam",
                table: "LoginAccount",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderDisplayName",
                schema: "iam",
                table: "LoginAccount",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderEmail",
                schema: "iam",
                table: "LoginAccount",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderLoginName",
                schema: "iam",
                table: "LoginAccount",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderMobile",
                schema: "iam",
                table: "LoginAccount",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoleCode",
                schema: "iam",
                table: "LoginAccount",
                type: "varchar(40)",
                unicode: false,
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "org",
                table: "Organization",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "MockPassSchoolCode",
                schema: "org",
                table: "Organization",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "org",
                table: "Organization",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddPrimaryKey(
                name: "PK_LoginAccount",
                schema: "iam",
                table: "LoginAccount",
                column: "LoginAccountId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Organization",
                schema: "org",
                table: "Organization",
                column: "OrganizationId");

            migrationBuilder.CreateTable(
                name: "AccountHold",
                schema: "account",
                columns: table => new
                {
                    AccountHoldId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EducationAccountId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentPartId = table.Column<long>(type: "bigint", nullable: true),
                    HoldAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    HoldStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConvertedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AccountTransactionId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountHold", x => x.AccountHoldId);
                });

            migrationBuilder.CreateTable(
                name: "AccountSettlement",
                schema: "account",
                columns: table => new
                {
                    AccountSettlementId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EducationAccountId = table.Column<long>(type: "bigint", nullable: false),
                    SettlementPreferenceId = table.Column<long>(type: "bigint", nullable: true),
                    AccountTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    SettlementAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    DestinationTypeCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    DestinationToken = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DestinationMasked = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SettlementStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    ProviderReference = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountSettlement", x => x.AccountSettlementId);
                });

            migrationBuilder.CreateTable(
                name: "AccountTransaction",
                schema: "account",
                columns: table => new
                {
                    AccountTransactionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EducationAccountId = table.Column<long>(type: "bigint", nullable: false),
                    TransactionTypeCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    TransactionAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReferenceTypeCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ReferenceId = table.Column<long>(type: "bigint", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ReversalOfTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    BalanceAfter = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByLoginAccountId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTransaction", x => x.AccountTransactionId);
                });

            migrationBuilder.CreateTable(
                name: "AuditLog",
                schema: "audit",
                columns: table => new
                {
                    AuditLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditScopeCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: true),
                    ActorTypeCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ActorLoginAccountId = table.Column<long>(type: "bigint", nullable: true),
                    ActorNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PersonId = table.Column<long>(type: "bigint", nullable: true),
                    ActionCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    EntityTypeCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    EntityId = table.Column<long>(type: "bigint", nullable: true),
                    OutcomeCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChangedFieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.AuditLogId);
                });

            migrationBuilder.CreateTable(
                name: "Bill",
                schema: "billing",
                columns: table => new
                {
                    BillId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BillNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CourseEnrollmentId = table.Column<long>(type: "bigint", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    SubsidyAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    NetPayableAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    OutstandingAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    BillStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bill", x => x.BillId);
                });

            migrationBuilder.CreateTable(
                name: "BillLine",
                schema: "billing",
                columns: table => new
                {
                    BillLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BillId = table.Column<long>(type: "bigint", nullable: false),
                    FeeComponentId = table.Column<long>(type: "bigint", nullable: false),
                    CourseFeeId = table.Column<long>(type: "bigint", nullable: true),
                    DescriptionSnapshot = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    UnitAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    SubsidyAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillLine", x => x.BillLineId);
                });

            migrationBuilder.CreateTable(
                name: "Course",
                schema: "course",
                columns: table => new
                {
                    CourseId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: false),
                    CourseCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CourseName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AcademicYear = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EnrollmentOpenAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EnrollmentCloseAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CourseStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    CreatedByLoginAccountId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByLoginAccountId = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisabledByLoginAccountId = table.Column<long>(type: "bigint", nullable: true),
                    DisabledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisabledReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Course", x => x.CourseId);
                });

            migrationBuilder.CreateTable(
                name: "CourseEnrollment",
                schema: "course",
                columns: table => new
                {
                    CourseEnrollmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonId = table.Column<long>(type: "bigint", nullable: false),
                    CourseId = table.Column<long>(type: "bigint", nullable: false),
                    EnrollmentSourceCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    EnrolledByLoginAccountId = table.Column<long>(type: "bigint", nullable: true),
                    EnrolledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EnrollmentStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    ExitAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExitReasonCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseEnrollment", x => x.CourseEnrollmentId);
                });

            migrationBuilder.CreateTable(
                name: "CourseFASScheme",
                schema: "fas",
                columns: table => new
                {
                    CourseFASSchemeId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourseId = table.Column<long>(type: "bigint", nullable: false),
                    FASSchemeId = table.Column<long>(type: "bigint", nullable: false),
                    StatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseFASScheme", x => x.CourseFASSchemeId);
                });

            migrationBuilder.CreateTable(
                name: "CourseFee",
                schema: "course",
                columns: table => new
                {
                    CourseFeeId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourseId = table.Column<long>(type: "bigint", nullable: false),
                    FeeComponentId = table.Column<long>(type: "bigint", nullable: false),
                    FeeValue = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseFee", x => x.CourseFeeId);
                });

            migrationBuilder.CreateTable(
                name: "CourseTarget",
                schema: "course",
                columns: table => new
                {
                    CourseTargetId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourseId = table.Column<long>(type: "bigint", nullable: false),
                    TargetTypeCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    LevelCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClassCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseTarget", x => x.CourseTargetId);
                });

            migrationBuilder.CreateTable(
                name: "FASApplication",
                schema: "fas",
                columns: table => new
                {
                    FASApplicationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FASSchemeId = table.Column<long>(type: "bigint", nullable: false),
                    PersonId = table.Column<long>(type: "bigint", nullable: false),
                    CourseId = table.Column<long>(type: "bigint", nullable: true),
                    ApplicationStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    NationalitySnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    HouseholdIncomeSnapshot = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: true),
                    HouseholdSizeSnapshot = table.Column<int>(type: "int", nullable: true),
                    PerCapitaIncomeSnapshot = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: true),
                    SelectedTierId = table.Column<long>(type: "bigint", nullable: true),
                    EvaluationResultCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    EvaluatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApplicantConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FASApplication", x => x.FASApplicationId);
                });

            migrationBuilder.CreateTable(
                name: "FASRule",
                schema: "fas",
                columns: table => new
                {
                    FASRuleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FASTierId = table.Column<long>(type: "bigint", nullable: false),
                    RuleGroupNumber = table.Column<int>(type: "int", nullable: false),
                    CriterionCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    OperatorCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    NumericValueFrom = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: true),
                    NumericValueTo = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: true),
                    TextValue = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FASRule", x => x.FASRuleId);
                });

            migrationBuilder.CreateTable(
                name: "FASScheme",
                schema: "fas",
                columns: table => new
                {
                    FASSchemeId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchemeCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SchemeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProviderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    ApplicationOpenFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ApplicationOpenTo = table.Column<DateOnly>(type: "date", nullable: true),
                    SchemeStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FASScheme", x => x.FASSchemeId);
                });

            migrationBuilder.CreateTable(
                name: "FASSubsidy",
                schema: "fas",
                columns: table => new
                {
                    FASSubsidyId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FASApplicationId = table.Column<long>(type: "bigint", nullable: false),
                    FASTierBenefitId = table.Column<long>(type: "bigint", nullable: false),
                    BillLineId = table.Column<long>(type: "bigint", nullable: false),
                    GrossAmountSnapshot = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    CalculatedAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    AppliedAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    SubsidyStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FASSubsidy", x => x.FASSubsidyId);
                });

            migrationBuilder.CreateTable(
                name: "FASTier",
                schema: "fas",
                columns: table => new
                {
                    FASTierId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FASSchemeId = table.Column<long>(type: "bigint", nullable: false),
                    TierCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PriorityNumber = table.Column<int>(type: "int", nullable: false),
                    StatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FASTier", x => x.FASTierId);
                });

            migrationBuilder.CreateTable(
                name: "FASTierBenefit",
                schema: "fas",
                columns: table => new
                {
                    FASTierBenefitId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FASTierId = table.Column<long>(type: "bigint", nullable: false),
                    FeeComponentId = table.Column<long>(type: "bigint", nullable: false),
                    SubsidyTypeCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    SubsidyValue = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    MaximumSubsidyAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FASTierBenefit", x => x.FASTierBenefitId);
                });

            migrationBuilder.CreateTable(
                name: "FeeComponent",
                schema: "course",
                columns: table => new
                {
                    FeeComponentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComponentCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ComponentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ComponentTypeCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    CalculationTypeCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    IsTaxComponent = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeComponent", x => x.FeeComponentId);
                });

            migrationBuilder.CreateTable(
                name: "Notification",
                schema: "communication",
                columns: table => new
                {
                    NotificationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipientPersonId = table.Column<long>(type: "bigint", nullable: false),
                    RecipientLoginAccountId = table.Column<long>(type: "bigint", nullable: true),
                    NotificationTypeCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ReferenceTypeCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ReferenceId = table.Column<long>(type: "bigint", nullable: true),
                    ChannelCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    TemplateCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    NotificationStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notification", x => x.NotificationId);
                });

            migrationBuilder.CreateTable(
                name: "Payment",
                schema: "payment",
                columns: table => new
                {
                    PaymentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BillId = table.Column<long>(type: "bigint", nullable: false),
                    PayerPersonId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    SuccessfulAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    PaymentStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    InitiatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payment", x => x.PaymentId);
                });

            migrationBuilder.CreateTable(
                name: "PaymentPart",
                schema: "payment",
                columns: table => new
                {
                    PaymentPartId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentId = table.Column<long>(type: "bigint", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    PaymentMethodCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    EducationAccountId = table.Column<long>(type: "bigint", nullable: true),
                    AccountTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    PartAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    ProviderCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ProviderReference = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    PartStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    AuthorizedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SettledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentPart", x => x.PaymentPartId);
                });

            migrationBuilder.CreateTable(
                name: "SchoolEnrollment",
                schema: "person",
                columns: table => new
                {
                    SchoolEnrollmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonId = table.Column<long>(type: "bigint", nullable: false),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: false),
                    StudentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AcademicYear = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    LevelCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    ClassCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    SchoolingStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    StatusReasonCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SourceCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolEnrollment", x => x.SchoolEnrollmentId);
                });

            migrationBuilder.CreateTable(
                name: "SettlementPreference",
                schema: "account",
                columns: table => new
                {
                    SettlementPreferenceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EducationAccountId = table.Column<long>(type: "bigint", nullable: false),
                    DestinationTypeCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    DestinationToken = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DestinationMasked = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementPreference", x => x.SettlementPreferenceId);
                });

            migrationBuilder.CreateTable(
                name: "TopUpCampaign",
                schema: "topup",
                columns: table => new
                {
                    TopUpCampaignId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: false),
                    CampaignCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CampaignName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RecipientModeCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    DefaultTopUpAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ScheduleTypeCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FrequencyCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    FrequencyInterval = table.Column<int>(type: "int", nullable: true),
                    NextRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CampaignStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    CampaignVersion = table.Column<int>(type: "int", nullable: false),
                    CreatedByLoginAccountId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByLoginAccountId = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopUpCampaign", x => x.TopUpCampaignId);
                });

            migrationBuilder.CreateTable(
                name: "TopUpCampaignRecipient",
                schema: "topup",
                columns: table => new
                {
                    TopUpCampaignRecipientId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TopUpCampaignId = table.Column<long>(type: "bigint", nullable: false),
                    EducationAccountId = table.Column<long>(type: "bigint", nullable: false),
                    AmountOverride = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AddedByLoginAccountId = table.Column<long>(type: "bigint", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopUpCampaignRecipient", x => x.TopUpCampaignRecipientId);
                });

            migrationBuilder.CreateTable(
                name: "TopUpCampaignRule",
                schema: "topup",
                columns: table => new
                {
                    TopUpCampaignRuleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TopUpCampaignId = table.Column<long>(type: "bigint", nullable: false),
                    CriterionCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    OperatorCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    NumericValueFrom = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: true),
                    NumericValueTo = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: true),
                    TextValue = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopUpCampaignRule", x => x.TopUpCampaignRuleId);
                });

            migrationBuilder.CreateTable(
                name: "TopUpRun",
                schema: "topup",
                columns: table => new
                {
                    TopUpRunId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TopUpCampaignId = table.Column<long>(type: "bigint", nullable: false),
                    CampaignVersion = table.Column<int>(type: "int", nullable: false),
                    ScheduledFor = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TriggerTypeCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    TriggeredByLoginAccountId = table.Column<long>(type: "bigint", nullable: true),
                    RunStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    RuleSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalSelected = table.Column<int>(type: "int", nullable: false),
                    TotalProcessed = table.Column<int>(type: "int", nullable: false),
                    TotalSucceeded = table.Column<int>(type: "int", nullable: false),
                    TotalFailed = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopUpRun", x => x.TopUpRunId);
                });

            migrationBuilder.CreateTable(
                name: "TopUpTransaction",
                schema: "topup",
                columns: table => new
                {
                    TopUpTransactionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TopUpRunId = table.Column<long>(type: "bigint", nullable: false),
                    EducationAccountId = table.Column<long>(type: "bigint", nullable: false),
                    TopUpAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    TransactionStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    ProcessedByLoginAccountId = table.Column<long>(type: "bigint", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AccountTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopUpTransaction", x => x.TopUpTransactionId);
                });

            migrationBuilder.UpdateData(
                schema: "org",
                table: "Organization",
                keyColumn: "OrganizationId",
                keyValue: 1L,
                columns: new[] { "CreatedAt", "MockPassSchoolCode", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.CreateIndex(
                name: "IX_LoginAccount_AdminOrganizationId",
                schema: "iam",
                table: "LoginAccount",
                column: "AdminOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Organization_MockPassSchoolCode",
                schema: "org",
                table: "Organization",
                column: "MockPassSchoolCode",
                filter: "[MockPassSchoolCode] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Organization_Parent_NotSelf",
                schema: "org",
                table: "Organization",
                sql: "[ParentOrganizationId] IS NULL OR [ParentOrganizationId] <> [OrganizationId]");

            migrationBuilder.CreateIndex(
                name: "IX_AccountHold_EducationAccountId",
                schema: "account",
                table: "AccountHold",
                column: "EducationAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountHold_PaymentPartId",
                schema: "account",
                table: "AccountHold",
                column: "PaymentPartId",
                filter: "[PaymentPartId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSettlement_EducationAccountId",
                schema: "account",
                table: "AccountSettlement",
                column: "EducationAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransaction_EducationAccountId_TransactionAt",
                schema: "account",
                table: "AccountTransaction",
                columns: new[] { "EducationAccountId", "TransactionAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransaction_IdempotencyKey",
                schema: "account",
                table: "AccountTransaction",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_AuditScopeCode_OccurredAt",
                schema: "audit",
                table: "AuditLog",
                columns: new[] { "AuditScopeCode", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_CorrelationId",
                schema: "audit",
                table: "AuditLog",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_Bill_BillNumber",
                schema: "billing",
                table: "Bill",
                column: "BillNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bill_CourseEnrollmentId",
                schema: "billing",
                table: "Bill",
                column: "CourseEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_BillLine_BillId",
                schema: "billing",
                table: "BillLine",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_Course_OrganizationId_CourseCode_AcademicYear",
                schema: "course",
                table: "Course",
                columns: new[] { "OrganizationId", "CourseCode", "AcademicYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseEnrollment_PersonId_CourseId",
                schema: "course",
                table: "CourseEnrollment",
                columns: new[] { "PersonId", "CourseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseFASScheme_CourseId_FASSchemeId",
                schema: "fas",
                table: "CourseFASScheme",
                columns: new[] { "CourseId", "FASSchemeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseFee_CourseId_FeeComponentId",
                schema: "course",
                table: "CourseFee",
                columns: new[] { "CourseId", "FeeComponentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseTarget_CourseId",
                schema: "course",
                table: "CourseTarget",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_FASApplication_ApplicationNumber",
                schema: "fas",
                table: "FASApplication",
                column: "ApplicationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FASApplication_PersonId_FASSchemeId_CourseId",
                schema: "fas",
                table: "FASApplication",
                columns: new[] { "PersonId", "FASSchemeId", "CourseId" });

            migrationBuilder.CreateIndex(
                name: "IX_FASRule_FASTierId",
                schema: "fas",
                table: "FASRule",
                column: "FASTierId");

            migrationBuilder.CreateIndex(
                name: "IX_FASScheme_SchemeCode",
                schema: "fas",
                table: "FASScheme",
                column: "SchemeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FASSubsidy_BillLineId",
                schema: "fas",
                table: "FASSubsidy",
                column: "BillLineId");

            migrationBuilder.CreateIndex(
                name: "IX_FASSubsidy_FASApplicationId",
                schema: "fas",
                table: "FASSubsidy",
                column: "FASApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_FASTier_FASSchemeId_TierCode",
                schema: "fas",
                table: "FASTier",
                columns: new[] { "FASSchemeId", "TierCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FASTierBenefit_FASTierId_FeeComponentId",
                schema: "fas",
                table: "FASTierBenefit",
                columns: new[] { "FASTierId", "FeeComponentId" });

            migrationBuilder.CreateIndex(
                name: "IX_FeeComponent_ComponentCode",
                schema: "course",
                table: "FeeComponent",
                column: "ComponentCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notification_RecipientLoginAccountId",
                schema: "communication",
                table: "Notification",
                column: "RecipientLoginAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_RecipientPersonId",
                schema: "communication",
                table: "Notification",
                column: "RecipientPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_BillId",
                schema: "payment",
                table: "Payment",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_IdempotencyKey",
                schema: "payment",
                table: "Payment",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payment_PaymentNumber",
                schema: "payment",
                table: "Payment",
                column: "PaymentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPart_PaymentId_SequenceNumber",
                schema: "payment",
                table: "PaymentPart",
                columns: new[] { "PaymentId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolEnrollment_PersonId_OrganizationId_AcademicYear",
                schema: "person",
                table: "SchoolEnrollment",
                columns: new[] { "PersonId", "OrganizationId", "AcademicYear" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolEnrollment_StudentNumber",
                schema: "person",
                table: "SchoolEnrollment",
                column: "StudentNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementPreference_EducationAccountId_IsActive",
                schema: "account",
                table: "SettlementPreference",
                columns: new[] { "EducationAccountId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_TopUpCampaign_CampaignCode",
                schema: "topup",
                table: "TopUpCampaign",
                column: "CampaignCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopUpCampaign_OrganizationId",
                schema: "topup",
                table: "TopUpCampaign",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_TopUpCampaignRecipient_TopUpCampaignId_EducationAccountId",
                schema: "topup",
                table: "TopUpCampaignRecipient",
                columns: new[] { "TopUpCampaignId", "EducationAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopUpCampaignRule_TopUpCampaignId",
                schema: "topup",
                table: "TopUpCampaignRule",
                column: "TopUpCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_TopUpRun_IdempotencyKey",
                schema: "topup",
                table: "TopUpRun",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopUpRun_TopUpCampaignId",
                schema: "topup",
                table: "TopUpRun",
                column: "TopUpCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_TopUpTransaction_IdempotencyKey",
                schema: "topup",
                table: "TopUpTransaction",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopUpTransaction_TopUpRunId_EducationAccountId",
                schema: "topup",
                table: "TopUpTransaction",
                columns: new[] { "TopUpRunId", "EducationAccountId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountHold",
                schema: "account");

            migrationBuilder.DropTable(
                name: "AccountSettlement",
                schema: "account");

            migrationBuilder.DropTable(
                name: "AccountTransaction",
                schema: "account");

            migrationBuilder.DropTable(
                name: "AuditLog",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "Bill",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "BillLine",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "Course",
                schema: "course");

            migrationBuilder.DropTable(
                name: "CourseEnrollment",
                schema: "course");

            migrationBuilder.DropTable(
                name: "CourseFASScheme",
                schema: "fas");

            migrationBuilder.DropTable(
                name: "CourseFee",
                schema: "course");

            migrationBuilder.DropTable(
                name: "CourseTarget",
                schema: "course");

            migrationBuilder.DropTable(
                name: "FASApplication",
                schema: "fas");

            migrationBuilder.DropTable(
                name: "FASRule",
                schema: "fas");

            migrationBuilder.DropTable(
                name: "FASScheme",
                schema: "fas");

            migrationBuilder.DropTable(
                name: "FASSubsidy",
                schema: "fas");

            migrationBuilder.DropTable(
                name: "FASTier",
                schema: "fas");

            migrationBuilder.DropTable(
                name: "FASTierBenefit",
                schema: "fas");

            migrationBuilder.DropTable(
                name: "FeeComponent",
                schema: "course");

            migrationBuilder.DropTable(
                name: "Notification",
                schema: "communication");

            migrationBuilder.DropTable(
                name: "Payment",
                schema: "payment");

            migrationBuilder.DropTable(
                name: "PaymentPart",
                schema: "payment");

            migrationBuilder.DropTable(
                name: "SchoolEnrollment",
                schema: "person");

            migrationBuilder.DropTable(
                name: "SettlementPreference",
                schema: "account");

            migrationBuilder.DropTable(
                name: "TopUpCampaign",
                schema: "topup");

            migrationBuilder.DropTable(
                name: "TopUpCampaignRecipient",
                schema: "topup");

            migrationBuilder.DropTable(
                name: "TopUpCampaignRule",
                schema: "topup");

            migrationBuilder.DropTable(
                name: "TopUpRun",
                schema: "topup");

            migrationBuilder.DropTable(
                name: "TopUpTransaction",
                schema: "topup");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Organization",
                schema: "org",
                table: "Organization");

            migrationBuilder.DropIndex(
                name: "IX_Organization_MockPassSchoolCode",
                schema: "org",
                table: "Organization");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Organization_Parent_NotSelf",
                schema: "org",
                table: "Organization");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LoginAccount",
                schema: "iam",
                table: "LoginAccount");

            migrationBuilder.DropIndex(
                name: "IX_LoginAccount_AdminOrganizationId",
                schema: "iam",
                table: "LoginAccount");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "person",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "IdentityNumberMasked",
                schema: "person",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "OfficialAddress",
                schema: "person",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "OfficialEmail",
                schema: "person",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "OfficialMobile",
                schema: "person",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "PreferredAddress",
                schema: "person",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "PreferredEmail",
                schema: "person",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "PreferredMobile",
                schema: "person",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "SourceUpdatedAt",
                schema: "person",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "person",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "ClosedByLoginAccountId",
                schema: "account",
                table: "EducationAccount");

            migrationBuilder.DropColumn(
                name: "ClosingTypeCode",
                schema: "account",
                table: "EducationAccount");

            migrationBuilder.DropColumn(
                name: "ClosureExceptionApprovedByLoginAccountId",
                schema: "account",
                table: "EducationAccount");

            migrationBuilder.DropColumn(
                name: "ClosureExceptionReason",
                schema: "account",
                table: "EducationAccount");

            migrationBuilder.DropColumn(
                name: "ClosureExceptionUntil",
                schema: "account",
                table: "EducationAccount");

            migrationBuilder.DropColumn(
                name: "PendingClosureAt",
                schema: "account",
                table: "EducationAccount");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "org",
                table: "Organization");

            migrationBuilder.DropColumn(
                name: "MockPassSchoolCode",
                schema: "org",
                table: "Organization");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "org",
                table: "Organization");

            migrationBuilder.DropColumn(
                name: "AdminOrganizationId",
                schema: "iam",
                table: "LoginAccount");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                schema: "iam",
                table: "LoginAccount");

            migrationBuilder.DropColumn(
                name: "ContactMobile",
                schema: "iam",
                table: "LoginAccount");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                schema: "iam",
                table: "LoginAccount");

            migrationBuilder.DropColumn(
                name: "ProviderDisplayName",
                schema: "iam",
                table: "LoginAccount");

            migrationBuilder.DropColumn(
                name: "ProviderEmail",
                schema: "iam",
                table: "LoginAccount");

            migrationBuilder.DropColumn(
                name: "ProviderLoginName",
                schema: "iam",
                table: "LoginAccount");

            migrationBuilder.DropColumn(
                name: "ProviderMobile",
                schema: "iam",
                table: "LoginAccount");

            migrationBuilder.DropColumn(
                name: "RoleCode",
                schema: "iam",
                table: "LoginAccount");

            migrationBuilder.RenameTable(
                name: "Organization",
                schema: "org",
                newName: "OrganizationUnit",
                newSchema: "iam");

            migrationBuilder.RenameTable(
                name: "LoginAccount",
                schema: "iam",
                newName: "UserAccount",
                newSchema: "iam");

            migrationBuilder.RenameColumn(
                name: "ResidencyStatusCode",
                schema: "person",
                table: "Person",
                newName: "CitizenshipStatusCode");

            migrationBuilder.RenameColumn(
                name: "MockPassPersonId",
                schema: "person",
                table: "Person",
                newName: "ExternalPersonReference");

            migrationBuilder.RenameColumn(
                name: "FullName",
                schema: "person",
                table: "Person",
                newName: "OfficialFullName");

            migrationBuilder.RenameIndex(
                name: "IX_Person_MockPassPersonId",
                schema: "person",
                table: "Person",
                newName: "IX_Person_ExternalPersonReference");

            migrationBuilder.RenameColumn(
                name: "OpeningTypeCode",
                schema: "account",
                table: "EducationAccount",
                newName: "OpeningModeCode");

            migrationBuilder.RenameColumn(
                name: "OpeningReason",
                schema: "account",
                table: "EducationAccount",
                newName: "OpeningRemarks");

            migrationBuilder.RenameColumn(
                name: "OpenedByLoginAccountId",
                schema: "account",
                table: "EducationAccount",
                newName: "OpenedByUserId");

            migrationBuilder.RenameColumn(
                name: "OpenedAt",
                schema: "account",
                table: "EducationAccount",
                newName: "OpenedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CurrentBalance",
                schema: "account",
                table: "EducationAccount",
                newName: "CachedBalance");

            migrationBuilder.RenameColumn(
                name: "ClosingReason",
                schema: "account",
                table: "EducationAccount",
                newName: "ClosingRemarks");

            migrationBuilder.RenameColumn(
                name: "ClosedAt",
                schema: "account",
                table: "EducationAccount",
                newName: "ClosedAtUtc");

            migrationBuilder.RenameColumn(
                name: "AccountStatusCode",
                schema: "account",
                table: "EducationAccount",
                newName: "StatusCode");

            migrationBuilder.RenameColumn(
                name: "ParentOrganizationId",
                schema: "iam",
                table: "OrganizationUnit",
                newName: "ParentOrganizationUnitId");

            migrationBuilder.RenameColumn(
                name: "OrganizationTypeCode",
                schema: "iam",
                table: "OrganizationUnit",
                newName: "UnitTypeCode");

            migrationBuilder.RenameColumn(
                name: "OrganizationStatusCode",
                schema: "iam",
                table: "OrganizationUnit",
                newName: "StatusCode");

            migrationBuilder.RenameColumn(
                name: "OrganizationName",
                schema: "iam",
                table: "OrganizationUnit",
                newName: "UnitName");

            migrationBuilder.RenameColumn(
                name: "OrganizationCode",
                schema: "iam",
                table: "OrganizationUnit",
                newName: "UnitCode");

            migrationBuilder.RenameColumn(
                name: "OrganizationId",
                schema: "iam",
                table: "OrganizationUnit",
                newName: "OrganizationUnitId");

            migrationBuilder.RenameIndex(
                name: "IX_Organization_OrganizationCode",
                schema: "iam",
                table: "OrganizationUnit",
                newName: "IX_OrganizationUnit_UnitCode");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                schema: "iam",
                table: "UserAccount",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "LoginStatusCode",
                schema: "iam",
                table: "UserAccount",
                newName: "AccountStatusCode");

            migrationBuilder.RenameColumn(
                name: "LastLoginAt",
                schema: "iam",
                table: "UserAccount",
                newName: "LastLoginAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedByLoginAccountId",
                schema: "iam",
                table: "UserAccount",
                newName: "CreatedByUserAccountId");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                schema: "iam",
                table: "UserAccount",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "LoginAccountId",
                schema: "iam",
                table: "UserAccount",
                newName: "UserAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_LoginAccount_PersonId",
                schema: "iam",
                table: "UserAccount",
                newName: "IX_UserAccount_PersonId");

            migrationBuilder.RenameIndex(
                name: "IX_LoginAccount_LoginEmailNormalized",
                schema: "iam",
                table: "UserAccount",
                newName: "IX_UserAccount_LoginEmailNormalized");

            migrationBuilder.RenameIndex(
                name: "IX_LoginAccount_IdentityProviderCode_ExternalTenantId_ExternalObjectId",
                schema: "iam",
                table: "UserAccount",
                newName: "IX_UserAccount_IdentityProviderCode_ExternalTenantId_ExternalObjectId");

            migrationBuilder.RenameIndex(
                name: "IX_LoginAccount_IdentityProviderCode_ExternalIssuer_ExternalSubjectId",
                schema: "iam",
                table: "UserAccount",
                newName: "IX_UserAccount_IdentityProviderCode_ExternalIssuer_ExternalSubjectId");

            migrationBuilder.AlterColumn<string>(
                name: "PersonStatusCode",
                schema: "person",
                table: "Person",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(30)",
                oldUnicode: false,
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "NationalityCode",
                schema: "person",
                table: "Person",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(30)",
                oldUnicode: false,
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "CitizenshipStatusCode",
                schema: "person",
                table: "Person",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(30)",
                oldUnicode: false,
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "OpeningModeCode",
                schema: "account",
                table: "EducationAccount",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(30)",
                oldUnicode: false,
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<decimal>(
                name: "CachedBalance",
                schema: "account",
                table: "EducationAccount",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,2)",
                oldPrecision: 19,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "StatusCode",
                schema: "account",
                table: "EducationAccount",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(30)",
                oldUnicode: false,
                oldMaxLength: 30);

            migrationBuilder.AddColumn<string>(
                name: "ClosingReasonCode",
                schema: "account",
                table: "EducationAccount",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                schema: "account",
                table: "EducationAccount",
                type: "char(3)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OpeningReasonCode",
                schema: "account",
                table: "EducationAccount",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OrganizationUnit",
                schema: "iam",
                table: "OrganizationUnit",
                column: "OrganizationUnitId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserAccount",
                schema: "iam",
                table: "UserAccount",
                column: "UserAccountId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrganizationUnit_Parent_NotSelf",
                schema: "iam",
                table: "OrganizationUnit",
                sql: "[ParentOrganizationUnitId] IS NULL OR [ParentOrganizationUnitId] <> [OrganizationUnitId]");
        }
    }
}
