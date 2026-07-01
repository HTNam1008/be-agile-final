using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddMailDeliveryOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "mail");

            migrationBuilder.CreateTable(
                name: "EmailNotification",
                schema: "mail",
                columns: table => new
                {
                    EmailNotificationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NotificationType = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    PersonId = table.Column<long>(type: "bigint", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PlainTextBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HtmlBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntityType = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    EntityId = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    StatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LockedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastErrorCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    LastErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResolvedToEmailMasked = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    RecipientSourceCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailNotification", x => x.EmailNotificationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailNotification_CreatedAtUtc",
                schema: "mail",
                table: "EmailNotification",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EmailNotification_Dedupe",
                schema: "mail",
                table: "EmailNotification",
                columns: new[] { "NotificationType", "PersonId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailNotification_Queue",
                schema: "mail",
                table: "EmailNotification",
                columns: new[] { "StatusCode", "NextAttemptAtUtc", "Priority", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailNotification",
                schema: "mail");
        }
    }
}
