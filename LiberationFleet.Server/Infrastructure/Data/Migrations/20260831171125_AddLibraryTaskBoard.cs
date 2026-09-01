using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryTaskBoard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LibraryTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrewId = table.Column<int>(type: "int", nullable: false),
                    CreatorUserId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    HasEncryptedContent = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    HasDeadline = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsRecurring = table.Column<bool>(type: "bit", nullable: false),
                    Frequency = table.Column<int>(type: "int", nullable: false),
                    TimeSpecific = table.Column<bool>(type: "bit", nullable: false),
                    SpecificTimeMinutes = table.Column<int>(type: "int", nullable: true),
                    IsSpaced = table.Column<bool>(type: "bit", nullable: false),
                    Interval = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    DaySpecific = table.Column<bool>(type: "bit", nullable: false),
                    WeekDays = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    MonthDays = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    YearMonth = table.Column<int>(type: "int", nullable: true),
                    YearDay = table.Column<int>(type: "int", nullable: true),
                    OneShotDueAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LibraryTasks_Crews_CrewId",
                        column: x => x.CrewId,
                        principalTable: "Crews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LibraryTasks_Users_CreatorUserId",
                        column: x => x.CreatorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LibraryTaskInstances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ClaimedByUserId = table.Column<int>(type: "int", nullable: true),
                    ClaimedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContributionGiftId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryTaskInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LibraryTaskInstances_LibraryTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "LibraryTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LibraryTaskInstances_Users_ClaimedByUserId",
                        column: x => x.ClaimedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LibraryTaskInstances_ClaimedByUserId_Status",
                table: "LibraryTaskInstances",
                columns: new[] { "ClaimedByUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LibraryTaskInstances_TaskId_Status_ScheduledAt",
                table: "LibraryTaskInstances",
                columns: new[] { "TaskId", "Status", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LibraryTasks_CreatorUserId",
                table: "LibraryTasks",
                column: "CreatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryTasks_CrewId_IsClosed_CreatedAt",
                table: "LibraryTasks",
                columns: new[] { "CrewId", "IsClosed", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LibraryTaskInstances");

            migrationBuilder.DropTable(
                name: "LibraryTasks");
        }
    }
}
