using System;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260731150000_AddFollowingSeasonAndProvisionalCaps")]
    public partial class AddFollowingSeasonAndProvisionalCaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FollowingSeasonStartDate",
                table: "Crews",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CapIsProvisional",
                table: "SeasonCycles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "SplitReservedAmount",
                table: "SeasonCycles",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FollowingSeasonStartDate",
                table: "Crews");

            migrationBuilder.DropColumn(
                name: "CapIsProvisional",
                table: "SeasonCycles");

            migrationBuilder.DropColumn(
                name: "SplitReservedAmount",
                table: "SeasonCycles");
        }
    }
}
