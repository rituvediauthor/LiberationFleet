using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryMaintenanceAndBroken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BrokenPendingConfirmation",
                table: "LibraryUnits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "BrokenReportedAt",
                table: "LibraryUnits",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRetired",
                table: "LibraryUnits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "LibraryMaintenanceRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    ContributorUserId = table.Column<int>(type: "int", nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    HasEncryptedContent = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryMaintenanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LibraryMaintenanceRecords_LibraryUnits_UnitId",
                        column: x => x.UnitId,
                        principalTable: "LibraryUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LibraryMaintenanceRecords_Users_ContributorUserId",
                        column: x => x.ContributorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LibraryMaintenanceRecords_ContributorUserId",
                table: "LibraryMaintenanceRecords",
                column: "ContributorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryMaintenanceRecords_UnitId",
                table: "LibraryMaintenanceRecords",
                column: "UnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LibraryMaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "BrokenPendingConfirmation",
                table: "LibraryUnits");

            migrationBuilder.DropColumn(
                name: "BrokenReportedAt",
                table: "LibraryUnits");

            migrationBuilder.DropColumn(
                name: "IsRetired",
                table: "LibraryUnits");
        }
    }
}
