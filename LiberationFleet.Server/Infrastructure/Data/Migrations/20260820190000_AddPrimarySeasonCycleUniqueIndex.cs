using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260820190000_AddPrimarySeasonCycleUniqueIndex")]
    public partial class AddPrimarySeasonCycleUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Merge duplicate primary rows (keep lowest Id), then delete extras.
            migrationBuilder.Sql("""
                ;WITH Ranked AS (
                    SELECT
                        Id,
                        CrewId,
                        UserId,
                        SeasonStartDate,
                        CycleReceived,
                        TotalReceptionAmount,
                        SurvivalThresholdReceived,
                        CycleCompleted,
                        HasCycleStarted,
                        ROW_NUMBER() OVER (
                            PARTITION BY CrewId, UserId, SeasonStartDate
                            ORDER BY Id
                        ) AS rn
                    FROM SeasonCycles
                    WHERE EmergencyRequestId IS NULL
                      AND EmergencySplitOfferId IS NULL
                )
                UPDATE keeper
                SET
                    CycleReceived = keeper.CycleReceived + agg.ExtraCycleReceived,
                    TotalReceptionAmount = keeper.TotalReceptionAmount + agg.ExtraTotalReception,
                    SurvivalThresholdReceived = keeper.SurvivalThresholdReceived + agg.ExtraSurvival,
                    CycleCompleted = CASE WHEN keeper.CycleCompleted = 1 OR agg.AnyCompleted = 1 THEN 1 ELSE 0 END,
                    HasCycleStarted = CASE WHEN keeper.HasCycleStarted = 1 OR agg.AnyStarted = 1 THEN 1 ELSE 0 END
                FROM SeasonCycles keeper
                INNER JOIN (
                    SELECT
                        MIN(Id) AS KeeperId,
                        SUM(CASE WHEN rn > 1 THEN CycleReceived ELSE 0 END) AS ExtraCycleReceived,
                        SUM(CASE WHEN rn > 1 THEN TotalReceptionAmount ELSE 0 END) AS ExtraTotalReception,
                        SUM(CASE WHEN rn > 1 THEN SurvivalThresholdReceived ELSE 0 END) AS ExtraSurvival,
                        MAX(CASE WHEN rn > 1 AND CycleCompleted = 1 THEN 1 ELSE 0 END) AS AnyCompleted,
                        MAX(CASE WHEN rn > 1 AND HasCycleStarted = 1 THEN 1 ELSE 0 END) AS AnyStarted
                    FROM Ranked
                    GROUP BY CrewId, UserId, SeasonStartDate
                    HAVING COUNT(*) > 1
                ) agg ON agg.KeeperId = keeper.Id;

                ;WITH Ranked AS (
                    SELECT
                        Id,
                        ROW_NUMBER() OVER (
                            PARTITION BY CrewId, UserId, SeasonStartDate
                            ORDER BY Id
                        ) AS rn
                    FROM SeasonCycles
                    WHERE EmergencyRequestId IS NULL
                      AND EmergencySplitOfferId IS NULL
                )
                DELETE FROM SeasonCycles
                WHERE Id IN (SELECT Id FROM Ranked WHERE rn > 1);
                """);

            migrationBuilder.DropIndex(
                name: "IX_SeasonCycles_CrewId_UserId_SeasonStartDate",
                table: "SeasonCycles");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonCycles_CrewId_SeasonStartDate",
                table: "SeasonCycles",
                columns: new[] { "CrewId", "SeasonStartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonCycles_OnePrimaryPerUserSeason",
                table: "SeasonCycles",
                columns: new[] { "CrewId", "UserId", "SeasonStartDate" },
                unique: true,
                filter: "[EmergencyRequestId] IS NULL AND [EmergencySplitOfferId] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SeasonCycles_OnePrimaryPerUserSeason",
                table: "SeasonCycles");

            migrationBuilder.DropIndex(
                name: "IX_SeasonCycles_CrewId_SeasonStartDate",
                table: "SeasonCycles");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonCycles_CrewId_UserId_SeasonStartDate",
                table: "SeasonCycles",
                columns: new[] { "CrewId", "UserId", "SeasonStartDate" });
        }
    }
}
