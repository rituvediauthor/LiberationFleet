using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Snapshot-only sync after guest donations / push-token hand-written migrations.
    /// Without this, EF Core blocks MigrateAsync with PendingModelChangesWarning
    /// and the API never marks the database ready (503 / donate status fails).
    /// </summary>
    public partial class SyncModelSnapshotAfterGuestDonations : Migration
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
