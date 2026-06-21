using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMutualAidSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PercentBonus",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "CountsTowardReception",
                table: "Gifts",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCustomGift",
                table: "Gifts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSurvivalThreshold",
                table: "Gifts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentSeasonStartDate",
                table: "Crews",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SeasonMemberCycleCap",
                table: "Crews",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SeasonNonMemberCycleCap",
                table: "Crews",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "SeasonStarted",
                table: "Crews",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentPriorityScore",
                table: "CrewMemberships",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedMonthlyContribution",
                table: "CrewMemberships",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHonoraryMember",
                table: "CrewMemberships",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsInSeason",
                table: "CrewMemberships",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOrganizer",
                table: "CrewMemberships",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSeasonReady",
                table: "CrewMemberships",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "MonthlySurvivalThresholds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrewId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    ThresholdAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReceivedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReceptionOrderPosition = table.Column<int>(type: "int", nullable: false),
                    Satisfied = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlySurvivalThresholds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonthlySurvivalThresholds_Crews_CrewId",
                        column: x => x.CrewId,
                        principalTable: "Crews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MonthlySurvivalThresholds_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeasonCycles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrewId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SeasonStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CycleCapAtStart = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalReceptionAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SurvivalThresholdReceived = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CycleReceived = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CycleCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CycleCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PriorityScoreAtSeasonStart = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReceptionOrderPosition = table.Column<int>(type: "int", nullable: false),
                    HasCycleStarted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonCycles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonCycles_Crews_CrewId",
                        column: x => x.CrewId,
                        principalTable: "Crews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeasonCycles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlySurvivalThresholds_CrewId_UserId_Year_Month",
                table: "MonthlySurvivalThresholds",
                columns: new[] { "CrewId", "UserId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlySurvivalThresholds_UserId",
                table: "MonthlySurvivalThresholds",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonCycles_CrewId_UserId_SeasonStartDate",
                table: "SeasonCycles",
                columns: new[] { "CrewId", "UserId", "SeasonStartDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeasonCycles_UserId",
                table: "SeasonCycles",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonthlySurvivalThresholds");

            migrationBuilder.DropTable(
                name: "SeasonCycles");

            migrationBuilder.DropColumn(
                name: "PercentBonus",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CountsTowardReception",
                table: "Gifts");

            migrationBuilder.DropColumn(
                name: "IsCustomGift",
                table: "Gifts");

            migrationBuilder.DropColumn(
                name: "IsSurvivalThreshold",
                table: "Gifts");

            migrationBuilder.DropColumn(
                name: "CurrentSeasonStartDate",
                table: "Crews");

            migrationBuilder.DropColumn(
                name: "SeasonMemberCycleCap",
                table: "Crews");

            migrationBuilder.DropColumn(
                name: "SeasonNonMemberCycleCap",
                table: "Crews");

            migrationBuilder.DropColumn(
                name: "SeasonStarted",
                table: "Crews");

            migrationBuilder.DropColumn(
                name: "CurrentPriorityScore",
                table: "CrewMemberships");

            migrationBuilder.DropColumn(
                name: "EstimatedMonthlyContribution",
                table: "CrewMemberships");

            migrationBuilder.DropColumn(
                name: "IsHonoraryMember",
                table: "CrewMemberships");

            migrationBuilder.DropColumn(
                name: "IsInSeason",
                table: "CrewMemberships");

            migrationBuilder.DropColumn(
                name: "IsOrganizer",
                table: "CrewMemberships");

            migrationBuilder.DropColumn(
                name: "IsSeasonReady",
                table: "CrewMemberships");
        }
    }
}
