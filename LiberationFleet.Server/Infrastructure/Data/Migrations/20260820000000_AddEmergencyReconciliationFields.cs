using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260820000000_AddEmergencyReconciliationFields")]
    public partial class AddEmergencyReconciliationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AmountFulfilled",
                table: "EmergencyRequests",
                newName: "AmountReceived");

            migrationBuilder.AddColumn<decimal>(
                name: "AmountSplitCommitted",
                table: "EmergencyRequests",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "OffererQueueRole",
                table: "EmergencySplitOffers",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "EmergencySplitOffers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RequesterEmergencyCycleId",
                table: "EmergencySplitOffers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OffererPaybackCycleId",
                table: "EmergencySplitOffers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmergencySplitOffers_OffererPaybackCycleId",
                table: "EmergencySplitOffers",
                column: "OffererPaybackCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencySplitOffers_RequesterEmergencyCycleId",
                table: "EmergencySplitOffers",
                column: "RequesterEmergencyCycleId");

            migrationBuilder.AddForeignKey(
                name: "FK_EmergencySplitOffers_SeasonCycles_OffererPaybackCycleId",
                table: "EmergencySplitOffers",
                column: "OffererPaybackCycleId",
                principalTable: "SeasonCycles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmergencySplitOffers_SeasonCycles_RequesterEmergencyCycleId",
                table: "EmergencySplitOffers",
                column: "RequesterEmergencyCycleId",
                principalTable: "SeasonCycles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Legacy rows counted split amounts in AmountReceived; separate committed splits from received gifts.
            migrationBuilder.Sql("""
                UPDATE er
                SET AmountSplitCommitted = ISNULL(
                    (SELECT SUM(o.Amount)
                     FROM EmergencySplitOffers o
                     WHERE o.EmergencyRequestId = er.Id AND o.IsCancelled = 0),
                    0)
                FROM EmergencyRequests er;
                """);

            migrationBuilder.Sql("""
                UPDATE er
                SET AmountReceived = CASE
                    WHEN er.AmountReceived - er.AmountSplitCommitted < 0 THEN 0
                    ELSE er.AmountReceived - er.AmountSplitCommitted
                END
                FROM EmergencyRequests er
                WHERE er.AmountSplitCommitted > 0;
                """);

            migrationBuilder.Sql("""
                UPDATE er
                SET Status = 0
                FROM EmergencyRequests er
                WHERE er.Status = 1
                  AND er.AmountReceived < er.AmountNeeded;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmergencySplitOffers_SeasonCycles_OffererPaybackCycleId",
                table: "EmergencySplitOffers");

            migrationBuilder.DropForeignKey(
                name: "FK_EmergencySplitOffers_SeasonCycles_RequesterEmergencyCycleId",
                table: "EmergencySplitOffers");

            migrationBuilder.DropIndex(
                name: "IX_EmergencySplitOffers_OffererPaybackCycleId",
                table: "EmergencySplitOffers");

            migrationBuilder.DropIndex(
                name: "IX_EmergencySplitOffers_RequesterEmergencyCycleId",
                table: "EmergencySplitOffers");

            migrationBuilder.DropColumn(
                name: "AmountSplitCommitted",
                table: "EmergencyRequests");

            migrationBuilder.DropColumn(
                name: "OffererQueueRole",
                table: "EmergencySplitOffers");

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "EmergencySplitOffers");

            migrationBuilder.DropColumn(
                name: "RequesterEmergencyCycleId",
                table: "EmergencySplitOffers");

            migrationBuilder.DropColumn(
                name: "OffererPaybackCycleId",
                table: "EmergencySplitOffers");

            migrationBuilder.RenameColumn(
                name: "AmountReceived",
                table: "EmergencyRequests",
                newName: "AmountFulfilled");
        }
    }
}
