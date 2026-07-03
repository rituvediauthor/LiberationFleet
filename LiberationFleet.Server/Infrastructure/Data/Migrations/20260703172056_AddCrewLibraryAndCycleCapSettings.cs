using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCrewLibraryAndCycleCapSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LibraryOfThingsEnabled",
                table: "Crews",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MemberCycleCapFixedAmount",
                table: "Crews",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MemberCycleCapMode",
                table: "Crews",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "MemberCycleCapMultiplier",
                table: "Crews",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 2m);

            migrationBuilder.AddColumn<decimal>(
                name: "NonMemberCycleCapFixedAmount",
                table: "Crews",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "NonMemberCycleCapMode",
                table: "Crews",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "NonMemberCycleCapMultiplier",
                table: "Crews",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0.25m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LibraryOfThingsEnabled",
                table: "Crews");

            migrationBuilder.DropColumn(
                name: "MemberCycleCapFixedAmount",
                table: "Crews");

            migrationBuilder.DropColumn(
                name: "MemberCycleCapMode",
                table: "Crews");

            migrationBuilder.DropColumn(
                name: "MemberCycleCapMultiplier",
                table: "Crews");

            migrationBuilder.DropColumn(
                name: "NonMemberCycleCapFixedAmount",
                table: "Crews");

            migrationBuilder.DropColumn(
                name: "NonMemberCycleCapMode",
                table: "Crews");

            migrationBuilder.DropColumn(
                name: "NonMemberCycleCapMultiplier",
                table: "Crews");
        }
    }
}
