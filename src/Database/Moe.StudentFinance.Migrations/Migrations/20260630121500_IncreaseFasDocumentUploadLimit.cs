using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moe.StudentFinance.Persistence;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations;

/// <inheritdoc />
[DbContext(typeof(MoeDbContext))]
[Migration("20260630121500_IncreaseFasDocumentUploadLimit")]
public partial class IncreaseFasDocumentUploadLimit : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE [fas].[FASDocument] DROP CONSTRAINT [CK_FASDocument_Size];
            ALTER TABLE [fas].[FASDocument]
            ADD CONSTRAINT [CK_FASDocument_Size]
            CHECK ([FileSizeBytes] > 0 AND [FileSizeBytes] <= 20971520);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE [fas].[FASDocument] DROP CONSTRAINT [CK_FASDocument_Size];
            ALTER TABLE [fas].[FASDocument]
            ADD CONSTRAINT [CK_FASDocument_Size]
            CHECK ([FileSizeBytes] > 0 AND [FileSizeBytes] <= 10485760);
            """);
    }
}
