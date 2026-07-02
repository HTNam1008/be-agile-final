using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationRealtimeDeliveryOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChannelCode",
                schema: "communication",
                table: "Notification",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReferenceTypeCode",
                schema: "communication",
                table: "Notification",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TemplateCode",
                schema: "communication",
                table: "Notification",
                type: "varchar(100)",
                unicode: false,
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "NotificationRealtimeDelivery",
                schema: "communication",
                columns: table => new
                {
                    NotificationRealtimeDeliveryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NotificationId = table.Column<long>(type: "bigint", nullable: false),
                    RecipientUserAccountId = table.Column<long>(type: "bigint", nullable: false),
                    StatusCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LockedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastErrorCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    LastErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationRealtimeDelivery", x => x.NotificationRealtimeDeliveryId);
                    table.ForeignKey(
                        name: "FK_NotificationRealtimeDelivery_Notification_NotificationId",
                        column: x => x.NotificationId,
                        principalSchema: "communication",
                        principalTable: "Notification",
                        principalColumn: "NotificationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRealtimeDelivery_NotificationId",
                schema: "communication",
                table: "NotificationRealtimeDelivery",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRealtimeDelivery_Queue",
                schema: "communication",
                table: "NotificationRealtimeDelivery",
                columns: new[] { "StatusCode", "NextAttemptAtUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRealtimeDelivery_Recipient_CreatedAt",
                schema: "communication",
                table: "NotificationRealtimeDelivery",
                columns: new[] { "RecipientUserAccountId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationRealtimeDelivery",
                schema: "communication");

            migrationBuilder.DropColumn(
                name: "ChannelCode",
                schema: "communication",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "ReferenceTypeCode",
                schema: "communication",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "TemplateCode",
                schema: "communication",
                table: "Notification");
        }
    }
}
