using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260908180000_AddLibraryPriorityTierStock")]
    public partial class AddLibraryPriorityTierStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RemainingStockTier1",
                table: "LibraryOfferings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemainingStockTier2",
                table: "LibraryOfferings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemainingStockTier3",
                table: "LibraryOfferings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemainingStockTier4",
                table: "LibraryOfferings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemainingStockTier5",
                table: "LibraryOfferings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumViewerTier",
                table: "LibraryOfferings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // Existing tracked consumable stock becomes Tier 1 pool; other tiers start at 0.
            migrationBuilder.Sql("""
                UPDATE LibraryOfferings
                SET RemainingStockTier1 = RemainingStock,
                    RemainingStockTier2 = 0,
                    RemainingStockTier3 = 0,
                    RemainingStockTier4 = 0,
                    RemainingStockTier5 = 0
                WHERE Kind = 1
                  AND QuantityNotApplicable = 0
                  AND RemainingStock IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RemainingStockTier1",
                table: "LibraryOfferings");

            migrationBuilder.DropColumn(
                name: "RemainingStockTier2",
                table: "LibraryOfferings");

            migrationBuilder.DropColumn(
                name: "RemainingStockTier3",
                table: "LibraryOfferings");

            migrationBuilder.DropColumn(
                name: "RemainingStockTier4",
                table: "LibraryOfferings");

            migrationBuilder.DropColumn(
                name: "RemainingStockTier5",
                table: "LibraryOfferings");

            migrationBuilder.DropColumn(
                name: "MinimumViewerTier",
                table: "LibraryOfferings");
        }
    }
}
