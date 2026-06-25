using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddAiCopilotConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ai");

            migrationBuilder.CreateTable(
                name: "AdminCenterCase",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<long>(type: "bigint", nullable: false),
                    DescriptionRedacted = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ContactPreferenceCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    StatusCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminCenterCase", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Conversation",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<long>(type: "bigint", nullable: false),
                    PortalCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    ModeCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    StatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    PageContextJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FasInterviewJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Message",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    ContentRedacted = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CitationsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ToolSummaryJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LatencyMs = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Message", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReviewRecord",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<long>(type: "bigint", nullable: false),
                    ReasonCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    DomainCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    SeverityCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    StatusCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Route = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TranscriptRedacted = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedByUserAccountId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewRecord", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "iam",
                table: "Permission",
                columns: new[] { "PermissionCode", "ActionCode", "ModuleCode", "PermissionName", "ResourceCode", "StatusCode" },
                values: new object[] { "AI_REVIEW_MANAGE", "MANAGE", "AI_COPILOT", "Manage AI review queue", "AI_REVIEWS", "ACTIVE" });

            migrationBuilder.InsertData(
                schema: "iam",
                table: "RolePermission",
                columns: new[] { "RolePermissionId", "EffectiveFromUtc", "EffectiveToUtc", "PermissionCode", "RoleCode", "StatusCode" },
                values: new object[] { 29L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "AI_REVIEW_MANAGE", "HQ_ADMIN", "ACTIVE" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminCenterCase_StatusCode_CreatedAtUtc",
                schema: "ai",
                table: "AdminCenterCase",
                columns: new[] { "StatusCode", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversation_PersonId_UpdatedAtUtc",
                schema: "ai",
                table: "Conversation",
                columns: new[] { "PersonId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Message_ConversationId_CreatedAtUtc",
                schema: "ai",
                table: "Message",
                columns: new[] { "ConversationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewRecord_StatusCode_CreatedAtUtc",
                schema: "ai",
                table: "ReviewRecord",
                columns: new[] { "StatusCode", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminCenterCase",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "Conversation",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "Message",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "ReviewRecord",
                schema: "ai");

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "Permission",
                keyColumn: "PermissionCode",
                keyValue: "AI_REVIEW_MANAGE");

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "RolePermission",
                keyColumn: "RolePermissionId",
                keyValue: 29L);
        }
    }
}
