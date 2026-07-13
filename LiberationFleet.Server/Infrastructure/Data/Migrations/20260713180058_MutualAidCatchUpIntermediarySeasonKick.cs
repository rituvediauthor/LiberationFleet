using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MutualAidCatchUpIntermediarySeasonKick : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CycleCapAtCompletion",
                table: "SeasonCycles",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "NonMemberCycleCapMultiplier",
                table: "Crews",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0.5m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldPrecision: 18,
                oldScale: 4,
                oldDefaultValue: 0.25m);

            migrationBuilder.AddColumn<int>(
                name: "EmergencySacrificesThisSeason",
                table: "CrewMemberships",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IntermediaryFailedCompletions",
                table: "CrewMemberships",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsIntermediary",
                table: "CrewMemberships",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CycleCapAtCompletion",
                table: "SeasonCycles");

            migrationBuilder.DropColumn(
                name: "EmergencySacrificesThisSeason",
                table: "CrewMemberships");

            migrationBuilder.DropColumn(
                name: "IntermediaryFailedCompletions",
                table: "CrewMemberships");

            migrationBuilder.DropColumn(
                name: "IsIntermediary",
                table: "CrewMemberships");

            migrationBuilder.AlterColumn<decimal>(
                name: "NonMemberCycleCapMultiplier",
                table: "Crews",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0.25m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldPrecision: 18,
                oldScale: 4,
                oldDefaultValue: 0.5m);
        }
    }
}
