using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFleetKeyDistribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FleetId",
                table: "EncryptedContentEnvelopes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FleetKeyDistributions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FleetId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    KeyVersion = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    WrappedFleetKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WrapNonce = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WrappedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetKeyDistributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetKeyDistributions_Fleets_FleetId",
                        column: x => x.FleetId,
                        principalTable: "Fleets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetKeyDistributions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetKeyDistributions_Users_WrappedByUserId",
                        column: x => x.WrappedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EncryptedContentEnvelopes_FleetId",
                table: "EncryptedContentEnvelopes",
                column: "FleetId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EncryptedContentEnvelopes_CrewOrFleet",
                table: "EncryptedContentEnvelopes",
                sql: "[CrewId] IS NOT NULL OR [FleetId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FleetKeyDistributions_FleetId_UserId_KeyVersion",
                table: "FleetKeyDistributions",
                columns: new[] { "FleetId", "UserId", "KeyVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FleetKeyDistributions_UserId",
                table: "FleetKeyDistributions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetKeyDistributions_WrappedByUserId",
                table: "FleetKeyDistributions",
                column: "WrappedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_EncryptedContentEnvelopes_Fleets_FleetId",
                table: "EncryptedContentEnvelopes",
                column: "FleetId",
                principalTable: "Fleets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EncryptedContentEnvelopes_Fleets_FleetId",
                table: "EncryptedContentEnvelopes");

            migrationBuilder.DropTable(
                name: "FleetKeyDistributions");

            migrationBuilder.DropIndex(
                name: "IX_EncryptedContentEnvelopes_FleetId",
                table: "EncryptedContentEnvelopes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EncryptedContentEnvelopes_CrewOrFleet",
                table: "EncryptedContentEnvelopes");

            migrationBuilder.DropColumn(
                name: "FleetId",
                table: "EncryptedContentEnvelopes");
        }
    }
}
