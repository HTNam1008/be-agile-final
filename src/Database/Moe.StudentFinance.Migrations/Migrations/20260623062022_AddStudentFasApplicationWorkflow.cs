using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentFasApplicationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Legacy FAS columns are intentionally retained until data-copy validation succeeds.

            migrationBuilder.AddColumn<long>(
                name: "AccountHolderPersonId",
                schema: "fas",
                table: "FASApplication",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(name: "SchoolOrganizationId", schema: "fas", table: "FASApplication", type: "bigint", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "LockedAtUtc", schema: "fas", table: "FASApplication", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "UpdatedAt", schema: "fas", table: "FASApplication", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<long>(name: "UpdatedByLoginAccountId", schema: "fas", table: "FASApplication", type: "bigint", nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                schema: "fas",
                table: "FASApplication",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "fas",
                table: "FASApplication",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<long>(
                name: "CreatedByLoginAccountId",
                schema: "fas",
                table: "FASApplication",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                schema: "fas",
                table: "FASApplication",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                schema: "fas",
                table: "FASApplication",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmploymentStatusCode",
                schema: "fas",
                table: "FASApplication",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsWelfareHomeResident",
                schema: "fas",
                table: "FASApplication",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mobile",
                schema: "fas",
                table: "FASApplication",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NricFinMasked",
                schema: "fas",
                table: "FASApplication",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OtherMonthlyIncome",
                schema: "fas",
                table: "FASApplication",
                type: "decimal(19,2)",
                precision: 19,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchoolName",
                schema: "fas",
                table: "FASApplication",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentNameSnapshot",
                schema: "fas",
                table: "FASApplication",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StudentNumber",
                schema: "fas",
                table: "FASApplication",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentNumberSnapshot",
                schema: "fas",
                table: "FASApplication",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "SubmittedDateSnapshot",
                schema: "fas",
                table: "FASApplication",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.CreateTable(
                name: "FASActiveScheme",
                schema: "fas",
                columns: table => new
                {
                    FASActiveSchemeId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentPersonId = table.Column<long>(type: "bigint", nullable: false),
                    FasApplicationSchemeId = table.Column<long>(type: "bigint", nullable: false),
                    FasSchemeId = table.Column<long>(type: "bigint", nullable: false),
                    ActiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ActiveTo = table.Column<DateOnly>(type: "date", nullable: false),
                    StatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActivatedByLoginAccountId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FASActiveScheme", x => x.FASActiveSchemeId);
                });

            migrationBuilder.CreateTable(
                name: "FASApplicationScheme",
                schema: "fas",
                columns: table => new
                {
                    FASApplicationSchemeId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FasApplicationId = table.Column<long>(type: "bigint", nullable: false),
                    FasSchemeId = table.Column<long>(type: "bigint", nullable: false),
                    StatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    RejectionNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ApprovedComponentsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByLoginAccountId = table.Column<long>(type: "bigint", nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedByLoginAccountId = table.Column<long>(type: "bigint", nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByLoginAccountId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FASApplicationScheme", x => x.FASApplicationSchemeId);
                    table.CheckConstraint("CK_FASApplicationScheme_Status", "[StatusCode] IN ('DRAFT','PENDING','APPROVED','REJECTED','CANCELLED','EXPIRED')");
                    table.ForeignKey(
                        name: "FK_FASApplicationScheme_FASApplication_FasApplicationId",
                        column: x => x.FasApplicationId,
                        principalSchema: "fas",
                        principalTable: "FASApplication",
                        principalColumn: "FASApplicationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FASApplicationScheme_FASScheme_FasSchemeId",
                        column: x => x.FasSchemeId,
                        principalSchema: "fas",
                        principalTable: "FASScheme",
                        principalColumn: "FASSchemeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FASDeclaration",
                schema: "fas",
                columns: table => new
                {
                    FASDeclarationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FasApplicationId = table.Column<long>(type: "bigint", nullable: false),
                    DeclarationTypeCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    IsAccepted = table.Column<bool>(type: "bit", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedByLoginAccountId = table.Column<long>(type: "bigint", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeclarationTextSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FASDeclaration", x => x.FASDeclarationId);
                    table.ForeignKey(
                        name: "FK_FASDeclaration_FASApplication_FasApplicationId",
                        column: x => x.FasApplicationId,
                        principalSchema: "fas",
                        principalTable: "FASApplication",
                        principalColumn: "FASApplicationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FASDocument",
                schema: "fas",
                columns: table => new
                {
                    FASDocumentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FasApplicationId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentTypeCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ChecklistItemCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    BlobKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MimeType = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    UploadedByLoginAccountId = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RemovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RemovedByLoginAccountId = table.Column<long>(type: "bigint", nullable: true),
                    ReplacedByDocumentId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FASDocument", x => x.FASDocumentId);
                    table.ForeignKey(
                        name: "FK_FASDocument_FASApplication_FasApplicationId",
                        column: x => x.FasApplicationId,
                        principalSchema: "fas",
                        principalTable: "FASApplication",
                        principalColumn: "FASApplicationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FASStatusHistory",
                schema: "fas",
                columns: table => new
                {
                    FASStatusHistoryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FasApplicationId = table.Column<long>(type: "bigint", nullable: true),
                    FasApplicationSchemeId = table.Column<long>(type: "bigint", nullable: true),
                    OldStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    NewStatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangedByLoginAccountId = table.Column<long>(type: "bigint", nullable: false),
                    ChangedByRole = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FASStatusHistory", x => x.FASStatusHistoryId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FASActiveScheme_FasApplicationSchemeId",
                schema: "fas",
                table: "FASActiveScheme",
                column: "FasApplicationSchemeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FASActiveScheme_StudentPersonId",
                schema: "fas",
                table: "FASActiveScheme",
                column: "StudentPersonId",
                unique: true,
                filter: "[StatusCode] = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "IX_FASApplicationScheme_FasApplicationId_FasSchemeId",
                schema: "fas",
                table: "FASApplicationScheme",
                columns: new[] { "FasApplicationId", "FasSchemeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FASApplicationScheme_FasSchemeId",
                schema: "fas",
                table: "FASApplicationScheme",
                column: "FasSchemeId");

            migrationBuilder.CreateIndex(
                name: "IX_FASDeclaration_FasApplicationId_DeclarationTypeCode",
                schema: "fas",
                table: "FASDeclaration",
                columns: new[] { "FasApplicationId", "DeclarationTypeCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FASDocument_FasApplicationId",
                schema: "fas",
                table: "FASDocument",
                column: "FasApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_FASStatusHistory_FasApplicationId",
                schema: "fas",
                table: "FASStatusHistory",
                column: "FasApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_FASStatusHistory_FasApplicationSchemeId",
                schema: "fas",
                table: "FASStatusHistory",
                column: "FasApplicationSchemeId");

            migrationBuilder.Sql("""
                UPDATE a SET
                    AccountHolderPersonId = a.PersonId,
                    StudentNameSnapshot = p.FullName,
                    StudentNumberSnapshot = COALESCE(e.StudentNumber, CONVERT(nvarchar(50), a.PersonId)),
                    StudentNumber = e.StudentNumber,
                    NricFinMasked = p.IdentityNumberMasked,
                    DateOfBirth = p.DateOfBirth,
                    Mobile = COALESCE(p.PreferredMobile, p.OfficialMobile),
                    Address = COALESCE(p.PreferredAddress, p.OfficialAddress),
                    Email = COALESCE(p.PreferredEmail, p.OfficialEmail),
                    SchoolOrganizationId = e.OrganizationId,
                    SchoolName = o.OrganizationName,
                    SubmittedDateSnapshot = CONVERT(date, COALESCE(a.SubmittedAt, SYSUTCDATETIME())),
                    CreatedAt = COALESCE(a.SubmittedAt, SYSUTCDATETIME())
                FROM fas.FASApplication a
                JOIN person.Person p ON p.PersonId = a.PersonId
                OUTER APPLY (
                    SELECT TOP 1 se.StudentNumber, se.OrganizationId
                    FROM person.SchoolEnrollment se
                    WHERE se.PersonId = a.PersonId AND se.SchoolingStatusCode = 'ACTIVE'
                    ORDER BY se.StartDate DESC
                ) e
                LEFT JOIN org.Organization o ON o.OrganizationId = e.OrganizationId;

                INSERT INTO fas.FASApplicationScheme
                    (FasApplicationId, FasSchemeId, StatusCode, IsActive, CreatedAtUtc, CreatedByLoginAccountId)
                SELECT a.FASApplicationId, a.FASSchemeId,
                    CASE a.ApplicationStatusCode WHEN 'APPROVED' THEN 'APPROVED' WHEN 'REJECTED' THEN 'REJECTED' ELSE 'PENDING' END,
                    0, COALESCE(a.SubmittedAt, SYSUTCDATETIME()), 0
                FROM fas.FASApplication a
                WHERE NOT EXISTS (SELECT 1 FROM fas.FASApplicationScheme i WHERE i.FasApplicationId=a.FASApplicationId AND i.FasSchemeId=a.FASSchemeId);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FASActiveScheme",
                schema: "fas");

            migrationBuilder.DropTable(
                name: "FASApplicationScheme",
                schema: "fas");

            migrationBuilder.DropTable(
                name: "FASDeclaration",
                schema: "fas");

            migrationBuilder.DropTable(
                name: "FASDocument",
                schema: "fas");

            migrationBuilder.DropTable(
                name: "FASStatusHistory",
                schema: "fas");

            migrationBuilder.DropColumn(
                name: "AccountHolderPersonId",
                schema: "fas",
                table: "FASApplication");

            migrationBuilder.DropColumn(
                name: "Address",
                schema: "fas",
                table: "FASApplication");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "fas",
                table: "FASApplication");

            migrationBuilder.DropColumn(
                name: "CreatedByLoginAccountId",
                schema: "fas",
                table: "FASApplication");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                schema: "fas",
                table: "FASApplication");

            migrationBuilder.DropColumn(
                name: "Email",
                schema: "fas",
                table: "FASApplication");

            migrationBuilder.DropColumn(
                name: "EmploymentStatusCode",
                schema: "fas",
                table: "FASApplication");

            migrationBuilder.DropColumn(
                name: "IsWelfareHomeResident",
                schema: "fas",
                table: "FASApplication");

            migrationBuilder.DropColumn(
                name: "Mobile",
                schema: "fas",
                table: "FASApplication");

            migrationBuilder.DropColumn(
                name: "NricFinMasked",
                schema: "fas",
                table: "FASApplication");

            migrationBuilder.DropColumn(
                name: "OtherMonthlyIncome",
                schema: "fas",
                table: "FASApplication");

            migrationBuilder.DropColumn(
                name: "SchoolName",
                schema: "fas",
                table: "FASApplication");

            migrationBuilder.DropColumn(
                name: "StudentNameSnapshot",
                schema: "fas",
                table: "FASApplication");

            migrationBuilder.DropColumn(
                name: "StudentNumber",
                schema: "fas",
                table: "FASApplication");

            migrationBuilder.DropColumn(
                name: "StudentNumberSnapshot",
                schema: "fas",
                table: "FASApplication");

            migrationBuilder.DropColumn(
                name: "SubmittedDateSnapshot",
                schema: "fas",
                table: "FASApplication");

            migrationBuilder.DropColumn(name: "SchoolOrganizationId", schema: "fas", table: "FASApplication");
            migrationBuilder.DropColumn(name: "LockedAtUtc", schema: "fas", table: "FASApplication");
            migrationBuilder.DropColumn(name: "UpdatedAt", schema: "fas", table: "FASApplication");
            migrationBuilder.DropColumn(name: "UpdatedByLoginAccountId", schema: "fas", table: "FASApplication");
        }
    }
}
