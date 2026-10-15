using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDonationGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DonationGoals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    TargetCents = table.Column<long>(type: "bigint", nullable: false),
                    ProgressCents = table.Column<long>(type: "bigint", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Recurrence = table.Column<int>(type: "int", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CompletionsThisYear = table.Column<int>(type: "int", nullable: false),
                    AnnualYearStart = table.Column<DateOnly>(type: "date", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonationGoals", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "DonationGoals",
                columns: new[] { "Id", "AnnualYearStart", "CompletedAt", "CompletionsThisYear", "Description", "IsCompleted", "Key", "ProgressCents", "Recurrence", "SortOrder", "TargetCents", "Title", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, null, null, 0, "I need to buy my time away from my contract work. I anticipate it will take up to 15 hours to get set up for the app store. If I get it up sooner, excess funds will go towards keeping the app live.", false, "launch-app-store", 0L, 0, 1, 75000L, "Launch on the app store!", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, null, null, 0, "Covers $32 a month hosting fees, $100 a year apple license, $20 Google workspace, and maintinance.", false, "live-six-months", 0L, 0, 2, 100000L, "Live for six months!", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, null, null, 0, "Covers $32 a month hosting fees, $100 a year apple license, $20 a month Google workspace, and maintinance.", false, "live-one-year", 0L, 1, 3, 200000L, "Live for one year!", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, null, null, 0, "We believe in paying all of our employees a livable wage. {completions} teammates funded this year!", false, "fund-teammate", 0L, 2, 4, 13000000L, "Fund a teammate!", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DonationGoals_Key",
                table: "DonationGoals",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DonationGoals_SortOrder",
                table: "DonationGoals",
                column: "SortOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DonationGoals");
        }
    }
}
