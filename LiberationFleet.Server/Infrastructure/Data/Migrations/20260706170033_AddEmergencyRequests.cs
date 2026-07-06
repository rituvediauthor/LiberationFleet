using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmergencyRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SeasonCycles_CrewId_UserId_SeasonStartDate",
                table: "SeasonCycles");

            migrationBuilder.AddColumn<int>(
                name: "EmergencyRequestId",
                table: "SeasonCycles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmergencySplitOfferId",
                table: "SeasonCycles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UsesSegmentCap",
                table: "SeasonCycles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EmergencyRequestId",
                table: "Gifts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeasonCycleId",
                table: "Gifts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmergencyRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrewId = table.Column<int>(type: "int", nullable: false),
                    RequesterUserId = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AmountNeeded = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountFulfilled = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmergencyRequests_Crews_CrewId",
                        column: x => x.CrewId,
                        principalTable: "Crews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmergencyRequests_Users_RequesterUserId",
                        column: x => x.RequesterUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmergencyGiftResponses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmergencyRequestId = table.Column<int>(type: "int", nullable: false),
                    GiverUserId = table.Column<int>(type: "int", nullable: false),
                    GiftId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyGiftResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmergencyGiftResponses_EmergencyRequests_EmergencyRequestId",
                        column: x => x.EmergencyRequestId,
                        principalTable: "EmergencyRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmergencyGiftResponses_Gifts_GiftId",
                        column: x => x.GiftId,
                        principalTable: "Gifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmergencyGiftResponses_Users_GiverUserId",
                        column: x => x.GiverUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmergencySplitOffers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmergencyRequestId = table.Column<int>(type: "int", nullable: false),
                    OffererUserId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencySplitOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmergencySplitOffers_EmergencyRequests_EmergencyRequestId",
                        column: x => x.EmergencyRequestId,
                        principalTable: "EmergencyRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmergencySplitOffers_Users_OffererUserId",
                        column: x => x.OffererUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonCycles_CrewId_UserId_SeasonStartDate",
                table: "SeasonCycles",
                columns: new[] { "CrewId", "UserId", "SeasonStartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonCycles_EmergencyRequestId",
                table: "SeasonCycles",
                column: "EmergencyRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonCycles_EmergencySplitOfferId",
                table: "SeasonCycles",
                column: "EmergencySplitOfferId");

            migrationBuilder.CreateIndex(
                name: "IX_Gifts_EmergencyRequestId",
                table: "Gifts",
                column: "EmergencyRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Gifts_SeasonCycleId",
                table: "Gifts",
                column: "SeasonCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyGiftResponses_EmergencyRequestId",
                table: "EmergencyGiftResponses",
                column: "EmergencyRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyGiftResponses_GiftId",
                table: "EmergencyGiftResponses",
                column: "GiftId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyGiftResponses_GiverUserId",
                table: "EmergencyGiftResponses",
                column: "GiverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyRequests_CrewId",
                table: "EmergencyRequests",
                column: "CrewId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyRequests_RequesterUserId",
                table: "EmergencyRequests",
                column: "RequesterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencySplitOffers_EmergencyRequestId",
                table: "EmergencySplitOffers",
                column: "EmergencyRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencySplitOffers_OffererUserId",
                table: "EmergencySplitOffers",
                column: "OffererUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Gifts_EmergencyRequests_EmergencyRequestId",
                table: "Gifts",
                column: "EmergencyRequestId",
                principalTable: "EmergencyRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Gifts_SeasonCycles_SeasonCycleId",
                table: "Gifts",
                column: "SeasonCycleId",
                principalTable: "SeasonCycles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SeasonCycles_EmergencyRequests_EmergencyRequestId",
                table: "SeasonCycles",
                column: "EmergencyRequestId",
                principalTable: "EmergencyRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SeasonCycles_EmergencySplitOffers_EmergencySplitOfferId",
                table: "SeasonCycles",
                column: "EmergencySplitOfferId",
                principalTable: "EmergencySplitOffers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Gifts_EmergencyRequests_EmergencyRequestId",
                table: "Gifts");

            migrationBuilder.DropForeignKey(
                name: "FK_Gifts_SeasonCycles_SeasonCycleId",
                table: "Gifts");

            migrationBuilder.DropForeignKey(
                name: "FK_SeasonCycles_EmergencyRequests_EmergencyRequestId",
                table: "SeasonCycles");

            migrationBuilder.DropForeignKey(
                name: "FK_SeasonCycles_EmergencySplitOffers_EmergencySplitOfferId",
                table: "SeasonCycles");

            migrationBuilder.DropTable(
                name: "EmergencyGiftResponses");

            migrationBuilder.DropTable(
                name: "EmergencySplitOffers");

            migrationBuilder.DropTable(
                name: "EmergencyRequests");

            migrationBuilder.DropIndex(
                name: "IX_SeasonCycles_CrewId_UserId_SeasonStartDate",
                table: "SeasonCycles");

            migrationBuilder.DropIndex(
                name: "IX_SeasonCycles_EmergencyRequestId",
                table: "SeasonCycles");

            migrationBuilder.DropIndex(
                name: "IX_SeasonCycles_EmergencySplitOfferId",
                table: "SeasonCycles");

            migrationBuilder.DropIndex(
                name: "IX_Gifts_EmergencyRequestId",
                table: "Gifts");

            migrationBuilder.DropIndex(
                name: "IX_Gifts_SeasonCycleId",
                table: "Gifts");

            migrationBuilder.DropColumn(
                name: "EmergencyRequestId",
                table: "SeasonCycles");

            migrationBuilder.DropColumn(
                name: "EmergencySplitOfferId",
                table: "SeasonCycles");

            migrationBuilder.DropColumn(
                name: "UsesSegmentCap",
                table: "SeasonCycles");

            migrationBuilder.DropColumn(
                name: "EmergencyRequestId",
                table: "Gifts");

            migrationBuilder.DropColumn(
                name: "SeasonCycleId",
                table: "Gifts");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonCycles_CrewId_UserId_SeasonStartDate",
                table: "SeasonCycles",
                columns: new[] { "CrewId", "UserId", "SeasonStartDate" },
                unique: true);
        }
    }
}
