using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Idempotent recreation of gift engagement tables. The original CreateTable migration
    /// shared a timestamp with AddGiftLibraryItemTitle and left some environments with
    /// incomplete schema while still recording a history row.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260821200000_RepairGiftLogSchemaIdempotent")]
    public partial class RepairGiftLogSchemaIdempotent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(GiftLogSchemaRepair.EnsureSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Repair-only; do not drop columns/tables that may be in use.
        }
    }
}
