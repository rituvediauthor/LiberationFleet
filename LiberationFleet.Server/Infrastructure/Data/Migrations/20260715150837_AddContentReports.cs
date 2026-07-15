using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContentReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReporterUserId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    TargetType = table.Column<int>(type: "int", nullable: false),
                    TargetResourceId = table.Column<int>(type: "int", nullable: true),
                    TargetParentId = table.Column<int>(type: "int", nullable: true),
                    TargetAuthorUserId = table.Column<int>(type: "int", nullable: true),
                    CrewId = table.Column<int>(type: "int", nullable: true),
                    FleetId = table.Column<int>(type: "int", nullable: true),
                    ReporterNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EvidenceNonce = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EvidenceCiphertext = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EscalatedToNcmecAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EscalatedToVendorAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VendorLabel = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OpsNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TargetQuarantined = table.Column<bool>(type: "bit", nullable: false),
                    TargetAuthorFrozen = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentReports_Users_ReporterUserId",
                        column: x => x.ReporterUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContentReports_Users_TargetAuthorUserId",
                        column: x => x.TargetAuthorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContentReportAccessLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContentReportId = table.Column<int>(type: "int", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AccessedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentReportAccessLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentReportAccessLogs_ContentReports_ContentReportId",
                        column: x => x.ContentReportId,
                        principalTable: "ContentReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentReportAccessLogs_ContentReportId",
                table: "ContentReportAccessLogs",
                column: "ContentReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_ReporterUserId",
                table: "ContentReports",
                column: "ReporterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_Status_CreatedAt",
                table: "ContentReports",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_TargetAuthorUserId",
                table: "ContentReports",
                column: "TargetAuthorUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentReportAccessLogs");

            migrationBuilder.DropTable(
                name: "ContentReports");
        }
    }
}
