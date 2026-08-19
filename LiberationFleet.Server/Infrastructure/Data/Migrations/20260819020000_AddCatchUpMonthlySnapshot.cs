using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260819020000_AddCatchUpMonthlySnapshot")]
    public partial class AddCatchUpMonthlySnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CatchUpSnapshotMonth",
                table: "Crews",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CatchUpSnapshotYear",
                table: "Crews",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CatchUpCapAtSnapshot",
                table: "SeasonCycles",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "CatchUpVisible",
                table: "SeasonCycles",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CatchUpSnapshotMonth",
                table: "Crews");

            migrationBuilder.DropColumn(
                name: "CatchUpSnapshotYear",
                table: "Crews");

            migrationBuilder.DropColumn(
                name: "CatchUpCapAtSnapshot",
                table: "SeasonCycles");

            migrationBuilder.DropColumn(
                name: "CatchUpVisible",
                table: "SeasonCycles");
        }
    }
}
