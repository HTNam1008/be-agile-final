using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddAiKnowledgeDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KnowledgeDocument",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Section = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Domain = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Version = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AlwaysInclude = table.Column<bool>(type: "bit", nullable: false),
                    ReviewOwner = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Synonyms = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AllowedIntents = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FollowUps = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeDocument", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeDocument_Domain_Status",
                schema: "ai",
                table: "KnowledgeDocument",
                columns: new[] { "Domain", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnowledgeDocument",
                schema: "ai");
        }
    }
}
