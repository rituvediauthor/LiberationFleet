using System;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260819010000_AddGivingSeasonJoinedAt")]
    public partial class AddGivingSeasonJoinedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "GivingSeasonJoinedAt",
                table: "CrewMemberships",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE m
                SET GivingSeasonJoinedAt = c.CurrentSeasonStartDate
                FROM CrewMemberships AS m
                INNER JOIN Crews AS c ON c.Id = m.CrewId
                WHERE m.IsInSeason = 1
                  AND c.CurrentSeasonStartDate IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GivingSeasonJoinedAt",
                table: "CrewMemberships");
        }
    }
}
