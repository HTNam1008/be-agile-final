using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddEducationAccountLifecycleRunHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EducationAccountLifecycleRun",
                schema: "account",
                columns: table => new
                {
                    EducationAccountLifecycleRunId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunDateUtc = table.Column<DateOnly>(type: "date", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TriggerTypeCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    StatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    OpenedCount = table.Column<int>(type: "int", nullable: false),
                    ClosedCount = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EducationAccountLifecycleRun", x => x.EducationAccountLifecycleRunId);
                });

            migrationBuilder.CreateTable(
                name: "EducationAccountLifecycleRunItem",
                schema: "account",
                columns: table => new
                {
                    EducationAccountLifecycleRunItemId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EducationAccountLifecycleRunId = table.Column<long>(type: "bigint", nullable: false),
                    PersonId = table.Column<long>(type: "bigint", nullable: false),
                    EducationAccountId = table.Column<long>(type: "bigint", nullable: false),
                    ActionCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EducationAccountLifecycleRunItem", x => x.EducationAccountLifecycleRunItemId);
                    table.ForeignKey(
                        name: "FK_EducationAccountLifecycleRunItem_EducationAccountLifecycleRun_EducationAccountLifecycleRunId",
                        column: x => x.EducationAccountLifecycleRunId,
                        principalSchema: "account",
                        principalTable: "EducationAccountLifecycleRun",
                        principalColumn: "EducationAccountLifecycleRunId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EducationAccountLifecycleRunItem_EducationAccount_EducationAccountId",
                        column: x => x.EducationAccountId,
                        principalSchema: "account",
                        principalTable: "EducationAccount",
                        principalColumn: "EducationAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EducationAccountLifecycleRun_RunDateUtc",
                schema: "account",
                table: "EducationAccountLifecycleRun",
                column: "RunDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EducationAccountLifecycleRun_StartedAtUtc",
                schema: "account",
                table: "EducationAccountLifecycleRun",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EducationAccountLifecycleRunItem_EducationAccountId",
                schema: "account",
                table: "EducationAccountLifecycleRunItem",
                column: "EducationAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_EducationAccountLifecycleRunItem_EducationAccountLifecycleRunId",
                schema: "account",
                table: "EducationAccountLifecycleRunItem",
                column: "EducationAccountLifecycleRunId");

            migrationBuilder.CreateIndex(
                name: "IX_EducationAccountLifecycleRunItem_PersonId",
                schema: "account",
                table: "EducationAccountLifecycleRunItem",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EducationAccountLifecycleRunItem",
                schema: "account");

            migrationBuilder.DropTable(
                name: "EducationAccountLifecycleRun",
                schema: "account");
        }
    }
}
