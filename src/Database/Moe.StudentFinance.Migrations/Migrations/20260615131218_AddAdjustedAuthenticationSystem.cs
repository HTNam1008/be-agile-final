using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddAdjustedAuthenticationSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "account");

            migrationBuilder.EnsureSchema(
                name: "iam");

            migrationBuilder.EnsureSchema(
                name: "person");

            migrationBuilder.CreateTable(
                name: "EducationAccount",
                schema: "account",
                columns: table => new
                {
                    EducationAccountId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonId = table.Column<long>(type: "bigint", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CurrencyCode = table.Column<string>(type: "char(3)", nullable: false),
                    StatusCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OpenedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OpeningModeCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OpeningReasonCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OpeningRemarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OpenedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosingReasonCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ClosingRemarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CachedBalance = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EducationAccount", x => x.EducationAccountId);
                });

            migrationBuilder.CreateTable(
                name: "IdentityProvisioningRequest",
                schema: "iam",
                columns: table => new
                {
                    IdentityProvisioningRequestId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonId = table.Column<long>(type: "bigint", nullable: false),
                    IdentityProviderCode = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    RequestedEmailNormalized = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    DisplayNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProvisioningStatusCode = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExternalTenantId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExternalObjectId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExternalSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RequestedByUserAccountId = table.Column<long>(type: "bigint", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityProvisioningRequest", x => x.IdentityProvisioningRequestId);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationUnit",
                schema: "iam",
                columns: table => new
                {
                    OrganizationUnitId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentOrganizationUnitId = table.Column<long>(type: "bigint", nullable: true),
                    UnitCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UnitName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UnitTypeCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    StatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationUnit", x => x.OrganizationUnitId);
                    table.CheckConstraint("CK_OrganizationUnit_Parent_NotSelf", "[ParentOrganizationUnitId] IS NULL OR [ParentOrganizationUnitId] <> [OrganizationUnitId]");
                });

            migrationBuilder.CreateTable(
                name: "Permission",
                schema: "iam",
                columns: table => new
                {
                    PermissionCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    PermissionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ModuleCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ActionCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    ResourceCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    StatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permission", x => x.PermissionCode);
                });

            migrationBuilder.CreateTable(
                name: "Person",
                schema: "person",
                columns: table => new
                {
                    PersonId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExternalPersonReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OfficialFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    NationalityCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CitizenshipStatusCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PersonStatusCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Person", x => x.PersonId);
                });

            migrationBuilder.CreateTable(
                name: "PersonIdentifier",
                schema: "person",
                columns: table => new
                {
                    PersonIdentifierId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonId = table.Column<long>(type: "bigint", nullable: false),
                    IdentifierTypeCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    IdentifierValueEncrypted = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    IdentifierValueHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    IdentifierMasked = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IssuingCountryCode = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: true),
                    IssuedByAuthority = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IdentifierStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    SourceSystemCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonIdentifier", x => x.PersonIdentifierId);
                });

            migrationBuilder.CreateTable(
                name: "RolePermission",
                schema: "iam",
                columns: table => new
                {
                    RolePermissionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    PermissionCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    StatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermission", x => x.RolePermissionId);
                });

            migrationBuilder.CreateTable(
                name: "UserAccessScope",
                schema: "iam",
                columns: table => new
                {
                    UserAccessScopeId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserAccountId = table.Column<long>(type: "bigint", nullable: false),
                    OrganizationUnitId = table.Column<long>(type: "bigint", nullable: false),
                    RoleCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    StatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserAccountId = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccessScope", x => x.UserAccessScopeId);
                });

            migrationBuilder.CreateTable(
                name: "UserAccount",
                schema: "iam",
                columns: table => new
                {
                    UserAccountId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonId = table.Column<long>(type: "bigint", nullable: true),
                    IdentityProviderCode = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    ExternalTenantId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExternalIssuer = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ExternalSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExternalObjectId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LoginEmailNormalized = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    DisplayNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UserTypeCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    PortalAccessCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    AccountStatusCode = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    FirstLoginAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLoginAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserAccountId = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccount", x => x.UserAccountId);
                });

            migrationBuilder.InsertData(
                schema: "iam",
                table: "Permission",
                columns: new[] { "PermissionCode", "ActionCode", "ModuleCode", "PermissionName", "ResourceCode", "StatusCode" },
                values: new object[,]
                {
                    { "ACCESS_SCOPE_MANAGE", "MANAGE", "IDENTITY_STUDENT", "Manage access scopes", "ACCESS_SCOPE", "ACTIVE" },
                    { "ACCOUNTS_MANAGE", "MANAGE", "ACCOUNT_FUNDING", "Manage accounts", "ACCOUNTS", "ACTIVE" },
                    { "ACCOUNTS_VIEW", "VIEW", "ACCOUNT_FUNDING", "View accounts", "ACCOUNTS", "ACTIVE" },
                    { "COURSES_MANAGE", "MANAGE", "ACADEMIC_FINANCE", "Manage courses", "COURSES", "ACTIVE" },
                    { "EXTERNAL_ACCOUNTS_PROVISION", "PROVISION", "IDENTITY_STUDENT", "Prepare student Singpass access", "EXTERNAL_ACCOUNTS", "ACTIVE" },
                    { "FAS_REVIEW", "REVIEW", "ACADEMIC_FINANCE", "Review FAS applications", "FAS", "ACTIVE" },
                    { "PAYMENT_EXCEPTIONS_REVIEW", "REVIEW", "PAYMENTS_DIGITAL", "Review payment exceptions", "PAYMENT_EXCEPTIONS", "ACTIVE" },
                    { "TOPUPS_MANAGE", "MANAGE", "ACCOUNT_FUNDING", "Manage top-ups", "TOPUPS", "ACTIVE" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_EducationAccount_AccountNumber",
                schema: "account",
                table: "EducationAccount",
                column: "AccountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EducationAccount_PersonId",
                schema: "account",
                table: "EducationAccount",
                column: "PersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdentityProvisioningRequest_IdempotencyKey",
                schema: "iam",
                table: "IdentityProvisioningRequest",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdentityProvisioningRequest_PersonId_IdentityProviderCode",
                schema: "iam",
                table: "IdentityProvisioningRequest",
                columns: new[] { "PersonId", "IdentityProviderCode" },
                unique: true,
                filter: "[ProvisioningStatusCode] IN ('PENDING', 'COMPLETED')");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationUnit_UnitCode",
                schema: "iam",
                table: "OrganizationUnit",
                column: "UnitCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Person_ExternalPersonReference",
                schema: "person",
                table: "Person",
                column: "ExternalPersonReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonIdentifier_IdentifierTypeCode_IdentifierValueHash",
                schema: "person",
                table: "PersonIdentifier",
                columns: new[] { "IdentifierTypeCode", "IdentifierValueHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonIdentifier_PersonId_IdentifierTypeCode_IsPrimary",
                schema: "person",
                table: "PersonIdentifier",
                columns: new[] { "PersonId", "IdentifierTypeCode", "IsPrimary" },
                unique: true,
                filter: "[IdentifierStatusCode] = 'ACTIVE' AND [IsPrimary] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermission_RoleCode_PermissionCode_EffectiveFromUtc",
                schema: "iam",
                table: "RolePermission",
                columns: new[] { "RoleCode", "PermissionCode", "EffectiveFromUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAccessScope_UserAccountId_OrganizationUnitId_RoleCode_EffectiveFromUtc",
                schema: "iam",
                table: "UserAccessScope",
                columns: new[] { "UserAccountId", "OrganizationUnitId", "RoleCode", "EffectiveFromUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAccount_IdentityProviderCode_ExternalIssuer_ExternalSubjectId",
                schema: "iam",
                table: "UserAccount",
                columns: new[] { "IdentityProviderCode", "ExternalIssuer", "ExternalSubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAccount_IdentityProviderCode_ExternalTenantId_ExternalObjectId",
                schema: "iam",
                table: "UserAccount",
                columns: new[] { "IdentityProviderCode", "ExternalTenantId", "ExternalObjectId" },
                unique: true,
                filter: "[ExternalObjectId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccount_LoginEmailNormalized",
                schema: "iam",
                table: "UserAccount",
                column: "LoginEmailNormalized");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccount_PersonId",
                schema: "iam",
                table: "UserAccount",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EducationAccount",
                schema: "account");

            migrationBuilder.DropTable(
                name: "IdentityProvisioningRequest",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "OrganizationUnit",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "Permission",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "Person",
                schema: "person");

            migrationBuilder.DropTable(
                name: "PersonIdentifier",
                schema: "person");

            migrationBuilder.DropTable(
                name: "RolePermission",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "UserAccessScope",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "UserAccount",
                schema: "iam");
        }
    }
}
