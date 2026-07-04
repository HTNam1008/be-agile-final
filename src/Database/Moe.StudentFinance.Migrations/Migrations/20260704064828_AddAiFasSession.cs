using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddAiFasSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FasInterviewJson",
                schema: "ai",
                table: "Conversation");

            migrationBuilder.CreateTable(
                name: "FasSession",
                schema: "ai",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    NextQuestion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TurnCount = table.Column<int>(type: "int", nullable: false),
                    CollectedFactsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    FormPatchJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FasSession", x => x.ConversationId);
                    table.ForeignKey(
                        name: "FK_FasSession_Conversation_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "ai",
                        principalTable: "Conversation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FasSession",
                schema: "ai");

            migrationBuilder.AddColumn<string>(
                name: "FasInterviewJson",
                schema: "ai",
                table: "Conversation",
                type: "nvarchar(max)",
                maxLength: 8000,
                nullable: true);
        }
    }
}
