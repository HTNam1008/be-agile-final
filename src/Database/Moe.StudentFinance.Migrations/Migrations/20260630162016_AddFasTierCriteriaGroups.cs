using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddFasTierCriteriaGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FASTierCriteriaGroup",
                schema: "fas",
                columns: table => new
                {
                    FASTierCriteriaGroupId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FASTierId = table.Column<long>(type: "bigint", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FASTierCriteriaGroup", x => x.FASTierCriteriaGroupId);
                    table.ForeignKey(
                        name: "FK_FASTierCriteriaGroup_FASTier_FASTierId",
                        column: x => x.FASTierId,
                        principalSchema: "fas",
                        principalTable: "FASTier",
                        principalColumn: "FASTierId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<long>(
                name: "FASTierCriteriaGroupId",
                schema: "fas",
                table: "FASTierCriteria",
                type: "bigint",
                nullable: true);

            migrationBuilder.Sql(
                """
                WITH OrderedCriteria AS
                (
                    SELECT
                        c.[FASTierCriteriaId],
                        c.[FASTierId],
                        c.[DisplayOrder],
                        c.[CreatedAt],
                        LAG(c.[ConnectorToNext]) OVER (PARTITION BY c.[FASTierId] ORDER BY c.[DisplayOrder]) AS [PreviousConnector]
                    FROM [fas].[FASTierCriteria] c
                ),
                GroupedCriteria AS
                (
                    SELECT
                        oc.[FASTierCriteriaId],
                        oc.[FASTierId],
                        oc.[DisplayOrder],
                        oc.[CreatedAt],
                        1 + SUM(CASE WHEN oc.[PreviousConnector] = 'OR' THEN 1 ELSE 0 END)
                            OVER (PARTITION BY oc.[FASTierId] ORDER BY oc.[DisplayOrder] ROWS UNBOUNDED PRECEDING) AS [GroupDisplayOrder]
                    FROM OrderedCriteria oc
                )
                INSERT INTO [fas].[FASTierCriteriaGroup] ([FASTierId], [DisplayOrder], [CreatedAt], [UpdatedAt])
                SELECT
                    grouped.[FASTierId],
                    grouped.[GroupDisplayOrder],
                    MIN(grouped.[CreatedAt]),
                    NULL
                FROM GroupedCriteria grouped
                GROUP BY grouped.[FASTierId], grouped.[GroupDisplayOrder];

                WITH OrderedCriteria AS
                (
                    SELECT
                        c.[FASTierCriteriaId],
                        c.[FASTierId],
                        c.[DisplayOrder],
                        LAG(c.[ConnectorToNext]) OVER (PARTITION BY c.[FASTierId] ORDER BY c.[DisplayOrder]) AS [PreviousConnector]
                    FROM [fas].[FASTierCriteria] c
                ),
                GroupedCriteria AS
                (
                    SELECT
                        oc.[FASTierCriteriaId],
                        oc.[FASTierId],
                        1 + SUM(CASE WHEN oc.[PreviousConnector] = 'OR' THEN 1 ELSE 0 END)
                            OVER (PARTITION BY oc.[FASTierId] ORDER BY oc.[DisplayOrder] ROWS UNBOUNDED PRECEDING) AS [GroupDisplayOrder]
                    FROM OrderedCriteria oc
                )
                UPDATE criteria
                SET [FASTierCriteriaGroupId] = groups.[FASTierCriteriaGroupId]
                FROM [fas].[FASTierCriteria] criteria
                INNER JOIN GroupedCriteria grouped ON grouped.[FASTierCriteriaId] = criteria.[FASTierCriteriaId]
                INNER JOIN [fas].[FASTierCriteriaGroup] groups
                    ON groups.[FASTierId] = grouped.[FASTierId]
                    AND groups.[DisplayOrder] = grouped.[GroupDisplayOrder];
                """);

            migrationBuilder.AlterColumn<long>(
                name: "FASTierCriteriaGroupId",
                schema: "fas",
                table: "FASTierCriteria",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FASTierCriteria_FASTierCriteriaGroupId",
                schema: "fas",
                table: "FASTierCriteria",
                column: "FASTierCriteriaGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_FASTierCriteriaGroup_FASTierId_DisplayOrder",
                schema: "fas",
                table: "FASTierCriteriaGroup",
                columns: new[] { "FASTierId", "DisplayOrder" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FASTierCriteria_FASTierCriteriaGroup_FASTierCriteriaGroupId",
                schema: "fas",
                table: "FASTierCriteria",
                column: "FASTierCriteriaGroupId",
                principalSchema: "fas",
                principalTable: "FASTierCriteriaGroup",
                principalColumn: "FASTierCriteriaGroupId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FASTierCriteria_FASTierCriteriaGroup_FASTierCriteriaGroupId",
                schema: "fas",
                table: "FASTierCriteria");

            migrationBuilder.DropTable(
                name: "FASTierCriteriaGroup",
                schema: "fas");

            migrationBuilder.DropIndex(
                name: "IX_FASTierCriteria_FASTierCriteriaGroupId",
                schema: "fas",
                table: "FASTierCriteria");

            migrationBuilder.DropColumn(
                name: "FASTierCriteriaGroupId",
                schema: "fas",
                table: "FASTierCriteria");
        }
    }
}
