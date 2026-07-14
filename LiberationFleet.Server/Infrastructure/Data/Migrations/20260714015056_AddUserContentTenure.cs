using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserContentTenure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserCrewContentTenures",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CrewId = table.Column<int>(type: "int", nullable: false),
                    AccruedTicks = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    ClockStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCrewContentTenures", x => new { x.UserId, x.CrewId });
                    table.ForeignKey(
                        name: "FK_UserCrewContentTenures_Crews_CrewId",
                        column: x => x.CrewId,
                        principalTable: "Crews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserCrewContentTenures_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserFleetContentTenures",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    FleetId = table.Column<int>(type: "int", nullable: false),
                    AccruedTicks = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    ClockStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFleetContentTenures", x => new { x.UserId, x.FleetId });
                    table.ForeignKey(
                        name: "FK_UserFleetContentTenures_Fleets_FleetId",
                        column: x => x.FleetId,
                        principalTable: "Fleets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserFleetContentTenures_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCrewContentTenures_CrewId",
                table: "UserCrewContentTenures",
                column: "CrewId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFleetContentTenures_FleetId",
                table: "UserFleetContentTenures",
                column: "FleetId");

            migrationBuilder.Sql("""
                INSERT INTO UserCrewContentTenures (UserId, CrewId, AccruedTicks, ClockStartedAtUtc)
                SELECT cm.UserId, cm.CrewId, 0,
                       CASE WHEN cm.IsBanned = 0 THEN cm.JoinedAt ELSE NULL END
                FROM CrewMemberships cm
                """);

            migrationBuilder.Sql("""
                INSERT INTO UserFleetContentTenures (UserId, FleetId, AccruedTicks, ClockStartedAtUtc)
                SELECT cm.UserId, fc.FleetId, 0,
                       CASE
                           WHEN cm.JoinedAt > fc.JoinedAt THEN cm.JoinedAt
                           ELSE fc.JoinedAt
                       END
                FROM FleetCrews fc
                INNER JOIN CrewMemberships cm ON cm.CrewId = fc.CrewId
                WHERE cm.IsBanned = 0
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserCrewContentTenures");

            migrationBuilder.DropTable(
                name: "UserFleetContentTenures");
        }
    }
}
