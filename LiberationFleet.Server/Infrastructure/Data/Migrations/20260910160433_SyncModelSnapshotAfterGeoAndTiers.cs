using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <summary>
    /// No-op migration: brings ApplicationDbContextModelSnapshot in sync with the model
    /// after hand-authored geo/postal and priority-tier migrations.
    /// Schema changes live in 20260910120000–20260910160000.
    /// </summary>
    public partial class SyncModelSnapshotAfterGeoAndTiers : Migration
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
