using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moe.StudentFinance.Persistence;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MoeDbContext))]
    [Migration("20260624043000_AddMfaAuditResetMetadata")]
    public partial class AddMfaAuditResetMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This metadata was already included when LoginMfaAuditEvent was introduced
            // in 20260623094246_AddMfaTables. Keep this migration as a no-op so
            // databases that already recorded it remain compatible, and fresh
            // databases do not try to add duplicate columns.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op. The columns/index/foreign key are owned by
            // 20260623094246_AddMfaTables.
        }
    }
}
