using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moe.StudentFinance.Persistence;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

[Migration("20260629173000_RenameNotificationRecipientPersonIdToUserAccountId")]
[DbContext(typeof(MoeDbContext))]
public partial class RenameNotificationRecipientPersonIdToUserAccountId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Notification_RecipientPersonId",
            schema: "communication",
            table: "Notification");

        migrationBuilder.RenameColumn(
            name: "RecipientPersonId",
            schema: "communication",
            table: "Notification",
            newName: "RecipientUserAccountId");

        migrationBuilder.AddColumn<string>(
            name: "Title",
            schema: "communication",
            table: "Notification",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: string.Empty);

        migrationBuilder.AddColumn<string>(
            name: "Body",
            schema: "communication",
            table: "Notification",
            type: "nvarchar(4000)",
            maxLength: 4000,
            nullable: false,
            defaultValue: string.Empty);

        migrationBuilder.AddColumn<DateTime>(
            name: "ReadAt",
            schema: "communication",
            table: "Notification",
            type: "datetime2",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Notification_RecipientUserAccountId_ReadAtUtc",
            schema: "communication",
            table: "Notification",
            columns: new[] { "RecipientUserAccountId", "ReadAt" });

        migrationBuilder.CreateIndex(
            name: "IX_Notification_RecipientUserAccountId_CreatedAtUtc",
            schema: "communication",
            table: "Notification",
            columns: new[] { "RecipientUserAccountId", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Notification_RecipientUserAccountId_ReadAtUtc",
            schema: "communication",
            table: "Notification");

        migrationBuilder.DropIndex(
            name: "IX_Notification_RecipientUserAccountId_CreatedAtUtc",
            schema: "communication",
            table: "Notification");

        migrationBuilder.DropColumn(
            name: "Title",
            schema: "communication",
            table: "Notification");

        migrationBuilder.DropColumn(
            name: "Body",
            schema: "communication",
            table: "Notification");

        migrationBuilder.DropColumn(
            name: "ReadAt",
            schema: "communication",
            table: "Notification");

        migrationBuilder.RenameColumn(
            name: "RecipientUserAccountId",
            schema: "communication",
            table: "Notification",
            newName: "RecipientPersonId");

        migrationBuilder.CreateIndex(
            name: "IX_Notification_RecipientPersonId",
            schema: "communication",
            table: "Notification",
            column: "RecipientPersonId");
    }
}
