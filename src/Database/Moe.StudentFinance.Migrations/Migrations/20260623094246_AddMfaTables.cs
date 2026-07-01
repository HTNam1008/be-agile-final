using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

/// <inheritdoc />
public partial class AddMfaTables : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LoginMfaChallenge",
            schema: "iam",
            columns: table => new
            {
                LoginMfaChallengeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LoginAccountId = table.Column<long>(type: "bigint", nullable: false),
                PortalAccessCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                PurposeCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                StatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                FailedAttemptCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LoginMfaChallenge", x => x.LoginMfaChallengeId);
                table.ForeignKey(
                    name: "FK_LoginMfaChallenge_LoginAccount",
                    column: x => x.LoginAccountId,
                    principalSchema: "iam",
                    principalTable: "LoginAccount",
                    principalColumn: "LoginAccountId");
            });

        migrationBuilder.CreateTable(
            name: "LoginMfaCredential",
            schema: "iam",
            columns: table => new
            {
                LoginMfaCredentialId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                LoginAccountId = table.Column<long>(type: "bigint", nullable: false),
                MfaTypeCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                SecretHash = table.Column<byte[]>(type: "varbinary(256)", maxLength: 256, nullable: false),
                SecretSalt = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                SecretHashAlgorithm = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                StatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                FailedAttemptCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                LockedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastVerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LoginMfaCredential", x => x.LoginMfaCredentialId);
                table.ForeignKey(
                    name: "FK_LoginMfaCredential_LoginAccount",
                    column: x => x.LoginAccountId,
                    principalSchema: "iam",
                    principalTable: "LoginAccount",
                    principalColumn: "LoginAccountId");
            });

        migrationBuilder.CreateTable(
            name: "LoginMfaAuditEvent",
            schema: "iam",
            columns: table => new
            {
                LoginMfaAuditEventId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                LoginAccountId = table.Column<long>(type: "bigint", nullable: false),
                LoginMfaChallengeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                EventCode = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                PortalAccessCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                PerformedByAccountId = table.Column<long>(type: "bigint", nullable: true),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LoginMfaAuditEvent", x => x.LoginMfaAuditEventId);
                table.ForeignKey(
                    name: "FK_LoginMfaAuditEvent_LoginAccount",
                    column: x => x.LoginAccountId,
                    principalSchema: "iam",
                    principalTable: "LoginAccount",
                    principalColumn: "LoginAccountId");
                table.ForeignKey(
                    name: "FK_LoginMfaAuditEvent_LoginMfaChallenge",
                    column: x => x.LoginMfaChallengeId,
                    principalSchema: "iam",
                    principalTable: "LoginMfaChallenge",
                    principalColumn: "LoginMfaChallengeId");
                table.ForeignKey(
                    name: "FK_LoginMfaAuditEvent_PerformedByAccount",
                    column: x => x.PerformedByAccountId,
                    principalSchema: "iam",
                    principalTable: "LoginAccount",
                    principalColumn: "LoginAccountId");
            });

        migrationBuilder.AddCheckConstraint(
            name: "CK_LoginMfaChallenge_FailedAttemptCount",
            schema: "iam",
            table: "LoginMfaChallenge",
            sql: "[FailedAttemptCount] >= 0");

        migrationBuilder.AddCheckConstraint(
            name: "CK_LoginMfaChallenge_PortalAccessCode",
            schema: "iam",
            table: "LoginMfaChallenge",
            sql: "[PortalAccessCode] IN ('ADMIN', 'ESERVICE')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_LoginMfaChallenge_PurposeCode",
            schema: "iam",
            table: "LoginMfaChallenge",
            sql: "[PurposeCode] IN ('SETUP', 'VERIFY', 'LOGIN')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_LoginMfaChallenge_StatusCode",
            schema: "iam",
            table: "LoginMfaChallenge",
            sql: "[StatusCode] IN ('PENDING', 'VERIFIED', 'EXPIRED', 'FAILED')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_LoginMfaCredential_FailedAttemptCount",
            schema: "iam",
            table: "LoginMfaCredential",
            sql: "[FailedAttemptCount] >= 0");

        migrationBuilder.AddCheckConstraint(
            name: "CK_LoginMfaCredential_MfaTypeCode",
            schema: "iam",
            table: "LoginMfaCredential",
            sql: "[MfaTypeCode] IN ('PIN')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_LoginMfaCredential_StatusCode",
            schema: "iam",
            table: "LoginMfaCredential",
            sql: "[StatusCode] IN ('ACTIVE', 'DISABLED', 'RESET_REQUIRED')");

        migrationBuilder.CreateIndex(
            name: "IX_LoginMfaAuditEvent_LoginAccountId_CreatedAtUtc",
            schema: "iam",
            table: "LoginMfaAuditEvent",
            columns: new[] { "LoginAccountId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_LoginMfaAuditEvent_LoginMfaChallengeId",
            schema: "iam",
            table: "LoginMfaAuditEvent",
            column: "LoginMfaChallengeId");

        migrationBuilder.CreateIndex(
            name: "IX_LoginMfaAuditEvent_PerformedByAccountId",
            schema: "iam",
            table: "LoginMfaAuditEvent",
            column: "PerformedByAccountId");

        migrationBuilder.CreateIndex(
            name: "IX_LoginMfaChallenge_LoginAccountId_StatusCode_ExpiresAtUtc",
            schema: "iam",
            table: "LoginMfaChallenge",
            columns: new[] { "LoginAccountId", "StatusCode", "ExpiresAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_LoginMfaCredential_LoginAccountId",
            schema: "iam",
            table: "LoginMfaCredential",
            column: "LoginAccountId");

        migrationBuilder.CreateIndex(
            name: "IX_LoginMfaCredential_LoginAccountId_MfaTypeCode",
            schema: "iam",
            table: "LoginMfaCredential",
            columns: new[] { "LoginAccountId", "MfaTypeCode" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "LoginMfaAuditEvent",
            schema: "iam");

        migrationBuilder.DropTable(
            name: "LoginMfaCredential",
            schema: "iam");

        migrationBuilder.DropTable(
            name: "LoginMfaChallenge",
            schema: "iam");
    }
}
