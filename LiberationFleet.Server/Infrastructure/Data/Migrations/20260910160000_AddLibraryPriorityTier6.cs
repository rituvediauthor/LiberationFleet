using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260910160000_AddLibraryPriorityTier6")]
    public partial class AddLibraryPriorityTier6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RemainingStockTier6",
                table: "LibraryOfferings",
                type: "int",
                nullable: true);

            // Existing per-tier stock stays in columns 1–5; tier 6 starts empty.
            // Vendors should rebalance for fleet-mate (1–3) vs crewmate (4–6) bands.
            migrationBuilder.Sql("""
                UPDATE LibraryOfferings
                SET RemainingStockTier6 = 0
                WHERE Kind = 1
                  AND QuantityNotApplicable = 0
                  AND (
                    RemainingStockTier1 IS NOT NULL
                    OR RemainingStockTier2 IS NOT NULL
                    OR RemainingStockTier3 IS NOT NULL
                    OR RemainingStockTier4 IS NOT NULL
                    OR RemainingStockTier5 IS NOT NULL
                  );
                """);

            migrationBuilder.Sql("""
                UPDATE LibraryOfferings
                SET MinimumViewerTier = CASE
                    WHEN MinimumViewerTier < 1 THEN 1
                    WHEN MinimumViewerTier > 6 THEN 6
                    ELSE MinimumViewerTier
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RemainingStockTier6",
                table: "LibraryOfferings");
        }
    }
}
