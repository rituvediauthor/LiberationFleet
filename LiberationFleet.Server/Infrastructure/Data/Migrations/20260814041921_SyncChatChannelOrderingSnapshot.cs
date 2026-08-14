using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Snapshot-only sync for chat channel ordering.
    /// Schema changes already live in <c>20260813180000_AddChatChannelOrdering</c> (hand-written).
    /// Without this snapshot update, EF Core blocks MigrateAsync with PendingModelChangesWarning
    /// and the App Service container never starts listening (503).
    /// </summary>
    public partial class SyncChatChannelOrderingSnapshot : Migration
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
