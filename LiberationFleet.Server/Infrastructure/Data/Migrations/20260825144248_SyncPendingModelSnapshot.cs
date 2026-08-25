using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Snapshot-only sync. Schema for gift engagement, perf indexes, and related model
    /// updates already lives in earlier hand-written migrations
    /// (<c>20260821170000_AddGiftEngagementAndSeasonLockSupport</c>,
    /// <c>20260824140000_AddPerfIndexesNotificationsGiftsMemberships</c>, etc.).
    /// Without this snapshot update, EF Core blocks MigrateAsync with PendingModelChangesWarning
    /// and the API never marks the database ready (503 / "still applying database updates").
    /// </summary>
    public partial class SyncPendingModelSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
